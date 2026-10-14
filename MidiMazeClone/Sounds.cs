using System.IO;
using System.Media;

namespace MidiMazeClone
{
    /// <summary>
    /// Retro sound effects, synthesised in code at startup (square waves and noise, like a
    /// sound chip) and played with System.Media.SoundPlayer. No files, no libraries.
    ///
    /// Limitation of SoundPlayer: only one sound plays at a time and starting a new one
    /// cuts the current one. A priority with a "busy until" time keeps important sounds
    /// (being hit) from being cut by unimportant ones (a bot shooting somewhere).
    /// </summary>
    public sealed class Sounds
    {
        private const int SampleRate = 22050;

        public const int PriorityRemoteShot = 0;
        public const int PriorityOwnShot = 1;
        public const int PriorityHitDealt = 2;
        public const int PriorityHitTaken = 3;

        private readonly SoundPlayer _shot;
        private readonly SoundPlayer _hitDealt;
        private readonly SoundPlayer _hitTaken;

        private readonly double _shotSeconds;
        private readonly double _hitDealtSeconds;
        private readonly double _hitTakenSeconds;

        private double _busyUntil;
        private int _busyPriority;

        public Sounds()
        {
            short[] shot = SynthShot();
            short[] hitDealt = SynthHitDealt();
            short[] hitTaken = SynthHitTaken();

            _shot = CreatePlayer(shot);
            _hitDealt = CreatePlayer(hitDealt);
            _hitTaken = CreatePlayer(hitTaken);

            _shotSeconds = shot.Length / (double)SampleRate;
            _hitDealtSeconds = hitDealt.Length / (double)SampleRate;
            _hitTakenSeconds = hitTaken.Length / (double)SampleRate;
        }

        /// <param name="now">Simulation time in seconds, used for the priority bookkeeping.</param>
        public void PlayShot(double now, int priority)
        {
            Play(_shot, _shotSeconds, now, priority);
        }

        public void PlayHitDealt(double now)
        {
            Play(_hitDealt, _hitDealtSeconds, now, PriorityHitDealt);
        }

        public void PlayHitTaken(double now)
        {
            Play(_hitTaken, _hitTakenSeconds, now, PriorityHitTaken);
        }

        private void Play(SoundPlayer player, double seconds, double now, int priority)
        {
            if (now < _busyUntil && priority < _busyPriority)
            {
                return;   // something more important is still playing
            }

            player.Play();
            _busyUntil = now + seconds;
            _busyPriority = priority;
        }

        // ---- synthesis -------------------------------------------------------------------

        /// <summary>Short square wave sweeping down from 900 to 300 Hz, 90 ms.</summary>
        private static short[] SynthShot()
        {
            int length = SampleRate * 90 / 1000;
            short[] samples = new short[length];
            double phase = 0.0;

            for (int i = 0; i < length; i++)
            {
                double t = i / (double)length;
                double frequency = 900.0 - 600.0 * t;
                phase += frequency / SampleRate;
                double envelope = 1.0 - t;
                samples[i] = ToSample(Square(phase) * envelope * 0.4);
            }

            return samples;
        }

        /// <summary>Two rising square tones, 660 Hz then 990 Hz, 70 ms each: "ding ding".</summary>
        private static short[] SynthHitDealt()
        {
            int toneLength = SampleRate * 70 / 1000;
            short[] samples = new short[toneLength * 2];
            double phase = 0.0;

            for (int i = 0; i < samples.Length; i++)
            {
                double frequency = 660.0;
                int indexInTone = i;
                if (i >= toneLength)
                {
                    frequency = 990.0;
                    indexInTone = i - toneLength;
                }

                phase += frequency / SampleRate;
                double envelope = 1.0 - indexInTone / (double)toneLength;
                samples[i] = ToSample(Square(phase) * envelope * 0.35);
            }

            return samples;
        }

        /// <summary>Noise burst over a low 90 Hz square, 350 ms, dying away: "crunch".</summary>
        private static short[] SynthHitTaken()
        {
            int length = SampleRate * 350 / 1000;
            short[] samples = new short[length];
            Random noise = new Random(1);   // fixed seed: the same sound every run
            double phase = 0.0;

            for (int i = 0; i < length; i++)
            {
                double t = i / (double)length;
                phase += 90.0 / SampleRate;
                double envelope = (1.0 - t) * (1.0 - t);
                double value = (noise.NextDouble() * 2.0 - 1.0) * 0.5 + Square(phase) * 0.5;
                samples[i] = ToSample(value * envelope * 0.6);
            }

            return samples;
        }

        private static double Square(double phase)
        {
            double fraction = phase - Math.Floor(phase);
            if (fraction < 0.5)
            {
                return 1.0;
            }
            return -1.0;
        }

        private static short ToSample(double value)
        {
            if (value > 1.0)
            {
                value = 1.0;
            }
            if (value < -1.0)
            {
                value = -1.0;
            }
            return (short)(value * short.MaxValue);
        }

        // ---- WAV plumbing ----------------------------------------------------------------

        private static SoundPlayer CreatePlayer(short[] samples)
        {
            byte[] wav = BuildWav(samples);
            MemoryStream stream = new MemoryStream(wav);
            SoundPlayer player = new SoundPlayer(stream);
            player.Load();   // decode now, so the first Play() has no delay
            return player;
        }

        /// <summary>16-bit mono PCM WAV: 44 byte header followed by the samples.</summary>
        private static byte[] BuildWav(short[] samples)
        {
            int dataBytes = samples.Length * 2;
            MemoryStream stream = new MemoryStream(44 + dataBytes);
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write((byte)'R');
            writer.Write((byte)'I');
            writer.Write((byte)'F');
            writer.Write((byte)'F');
            writer.Write(36 + dataBytes);
            writer.Write((byte)'W');
            writer.Write((byte)'A');
            writer.Write((byte)'V');
            writer.Write((byte)'E');

            writer.Write((byte)'f');
            writer.Write((byte)'m');
            writer.Write((byte)'t');
            writer.Write((byte)' ');
            writer.Write(16);                    // fmt chunk size
            writer.Write((short)1);              // PCM
            writer.Write((short)1);              // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);        // bytes per second
            writer.Write((short)2);              // block align
            writer.Write((short)16);             // bits per sample

            writer.Write((byte)'d');
            writer.Write((byte)'a');
            writer.Write((byte)'t');
            writer.Write((byte)'a');
            writer.Write(dataBytes);
            for (int i = 0; i < samples.Length; i++)
            {
                writer.Write(samples[i]);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
