using System.Runtime.InteropServices;
using System.Threading;

namespace MidiMazeClone
{
    /// <summary>
    /// Tiny software mixer on top of the Win32 waveOut API (winmm.dll, no libraries).
    /// Mono 16-bit PCM. A background thread keeps a few small buffers queued at the device
    /// and mixes all active voices into each one, so several sounds can play at the same
    /// time, each with its own volume.
    ///
    /// Memory handed to the driver (headers and sample buffers) is allocated unmanaged, so it
    /// never moves while the driver owns it.
    /// </summary>
    public sealed class AudioMixer : IDisposable
    {
        public const int BufferCount = 4;
        public const int SamplesPerBuffer = 256;    // 11.6 ms at 22050 Hz; 4 buffers = about 46 ms of latency
        public const int MaxVoices = 8;

        private const uint WaveMapper = 0xFFFFFFFF;     // "the default output device"
        private const uint CallbackEvent = 0x00050000;  // dwCallback is an event handle
        private const int HeaderInQueue = 0x00000010;   // WHDR_INQUEUE

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct WaveFormat
        {
            public short FormatTag;
            public short Channels;
            public int SamplesPerSecond;
            public int AverageBytesPerSecond;
            public short BlockAlign;
            public short BitsPerSample;
            public short ExtraSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public int BufferLength;
            public int BytesRecorded;
            public IntPtr User;
            public int Flags;
            public int Loops;
            public IntPtr Next;
            public IntPtr Reserved;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr handle, uint deviceId, ref WaveFormat format,
            IntPtr callback, IntPtr instance, uint flags);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr handle, IntPtr header, int headerSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr handle, IntPtr header, int headerSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr handle, IntPtr header, int headerSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr handle);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr handle);

        /// <summary>One sound currently playing: which samples, how far along, how loud.</summary>
        private sealed class Voice
        {
            public short[] Samples = Array.Empty<short>();
            public int Position;
            public double Volume;
        }

        public readonly int SampleRate;

        /// <summary>Buffers handed to the device so far; grows while the thread is healthy.</summary>
        public long BuffersWritten;

        /// <summary>Last non-zero waveOutWrite result, 0 if none.</summary>
        public int LastWriteError;

        private IntPtr _handle;
        private readonly IntPtr[] _headers = new IntPtr[BufferCount];
        private readonly IntPtr[] _data = new IntPtr[BufferCount];
        private readonly int _headerSize;
        private readonly int _flagsOffset;

        private readonly short[] _mixBuffer = new short[SamplesPerBuffer];
        private readonly int[] _accumulator = new int[SamplesPerBuffer];
        private readonly List<Voice> _voices = new List<Voice>();
        private readonly object _voicesLock = new object();

        private readonly AutoResetEvent _bufferDone = new AutoResetEvent(false);
        private Thread? _thread;
        private volatile bool _stopRequested;
        private bool _disposed;

        private AudioMixer(int sampleRate)
        {
            SampleRate = sampleRate;
            _headerSize = Marshal.SizeOf<WaveHeader>();
            _flagsOffset = (int)Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags));
        }

        /// <summary>Open the default output device. Returns null (and the MMRESULT code) when that fails.</summary>
        public static AudioMixer? TryCreate(int sampleRate, out int errorCode)
        {
            AudioMixer mixer = new AudioMixer(sampleRate);
            errorCode = mixer.Open();
            if (errorCode != 0)
            {
                mixer.Dispose();
                return null;
            }

            mixer.StartThread();
            return mixer;
        }

        private int Open()
        {
            WaveFormat format = new WaveFormat();
            format.FormatTag = 1;               // PCM
            format.Channels = 1;
            format.SamplesPerSecond = SampleRate;
            format.BitsPerSample = 16;
            format.BlockAlign = 2;
            format.AverageBytesPerSecond = SampleRate * 2;
            format.ExtraSize = 0;

            IntPtr eventHandle = _bufferDone.SafeWaitHandle.DangerousGetHandle();
            int result = waveOutOpen(out _handle, WaveMapper, ref format, eventHandle, IntPtr.Zero, CallbackEvent);
            if (result != 0)
            {
                _handle = IntPtr.Zero;
                return result;
            }

            int bytesPerBuffer = SamplesPerBuffer * 2;
            short[] silence = new short[SamplesPerBuffer];

            for (int i = 0; i < BufferCount; i++)
            {
                _data[i] = Marshal.AllocHGlobal(bytesPerBuffer);
                Marshal.Copy(silence, 0, _data[i], SamplesPerBuffer);

                WaveHeader header = new WaveHeader();
                header.Data = _data[i];
                header.BufferLength = bytesPerBuffer;

                _headers[i] = Marshal.AllocHGlobal(_headerSize);
                Marshal.StructureToPtr(header, _headers[i], false);

                result = waveOutPrepareHeader(_handle, _headers[i], _headerSize);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        private void StartThread()
        {
            _thread = new Thread(Run);
            _thread.Name = "AudioMixer";
            _thread.IsBackground = true;
            _thread.Priority = ThreadPriority.AboveNormal;
            _thread.Start();
        }

        /// <summary>Start playing a sound. Thread-safe. volume 0.0 .. 1.0.</summary>
        public void Play(short[] samples, double volume)
        {
            if (volume <= 0.0 || samples.Length == 0)
            {
                return;
            }
            if (volume > 1.0)
            {
                volume = 1.0;
            }

            Voice voice = new Voice();
            voice.Samples = samples;
            voice.Position = 0;
            voice.Volume = volume;

            lock (_voicesLock)
            {
                if (_voices.Count >= MaxVoices)
                {
                    _voices.RemoveAt(0);   // drop the oldest
                }
                _voices.Add(voice);
            }
        }

        public int ActiveVoices()
        {
            lock (_voicesLock)
            {
                return _voices.Count;
            }
        }

        /// <summary>Mixer thread: whenever a buffer is not queued at the device, fill and queue it.</summary>
        private void Run()
        {
            while (!_stopRequested)
            {
                for (int i = 0; i < BufferCount; i++)
                {
                    int flags = Marshal.ReadInt32(_headers[i], _flagsOffset);
                    if ((flags & HeaderInQueue) != 0)
                    {
                        continue;   // the device is still playing this one
                    }

                    MixNextBuffer();
                    Marshal.Copy(_mixBuffer, 0, _data[i], SamplesPerBuffer);

                    int result = waveOutWrite(_handle, _headers[i], _headerSize);
                    if (result == 0)
                    {
                        BuffersWritten++;
                    }
                    else
                    {
                        LastWriteError = result;
                    }
                }

                // Woken by the device when a buffer finishes; the timeout is only a safety net.
                _bufferDone.WaitOne(20);
            }
        }

        /// <summary>Sum all active voices into the mix buffer, clamp, drop finished voices.</summary>
        private void MixNextBuffer()
        {
            Array.Clear(_accumulator, 0, _accumulator.Length);

            lock (_voicesLock)
            {
                for (int v = _voices.Count - 1; v >= 0; v--)
                {
                    Voice voice = _voices[v];
                    int remaining = voice.Samples.Length - voice.Position;
                    int count = remaining;
                    if (count > SamplesPerBuffer)
                    {
                        count = SamplesPerBuffer;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        _accumulator[i] += (int)(voice.Samples[voice.Position + i] * voice.Volume);
                    }

                    voice.Position += count;
                    if (voice.Position >= voice.Samples.Length)
                    {
                        _voices.RemoveAt(v);
                    }
                }
            }

            for (int i = 0; i < SamplesPerBuffer; i++)
            {
                int value = _accumulator[i];
                if (value > short.MaxValue)
                {
                    value = short.MaxValue;
                }
                if (value < short.MinValue)
                {
                    value = short.MinValue;
                }
                _mixBuffer[i] = (short)value;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            _stopRequested = true;
            _bufferDone.Set();
            if (_thread != null)
            {
                _thread.Join(1000);
            }

            if (_handle != IntPtr.Zero)
            {
                waveOutReset(_handle);   // returns all queued buffers at once
                for (int i = 0; i < BufferCount; i++)
                {
                    if (_headers[i] != IntPtr.Zero)
                    {
                        waveOutUnprepareHeader(_handle, _headers[i], _headerSize);
                    }
                }
                waveOutClose(_handle);
                _handle = IntPtr.Zero;
            }

            for (int i = 0; i < BufferCount; i++)
            {
                if (_headers[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_headers[i]);
                    _headers[i] = IntPtr.Zero;
                }
                if (_data[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_data[i]);
                    _data[i] = IntPtr.Zero;
                }
            }

            _bufferDone.Dispose();
        }
    }
}
