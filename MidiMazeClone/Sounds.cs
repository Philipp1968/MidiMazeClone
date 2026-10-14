namespace MidiMazeClone
{
    /// <summary>
    /// Retro sound effects, synthesised in code at startup (square waves and noise, like a
    /// sound chip) and played through the AudioMixer. No files, no libraries.
    /// If the audio device cannot be opened, everything here silently does nothing.
    /// </summary>
    public sealed class Sounds : IDisposable
    {
        private const int SampleRate = 22050;

        private readonly AudioMixer? _mixer;
        private readonly short[] _shot;
        private readonly short[] _hitDealt;
        private readonly short[] _hitTaken;

        /// <summary>waveOutOpen result; 0 means the mixer is running.</summary>
        public readonly int OpenError;

        public Sounds()
        {
            _shot = SynthShot();
            _hitDealt = SynthHitDealt();
            _hitTaken = SynthHitTaken();

            int error;
            _mixer = AudioMixer.TryCreate(SampleRate, out error);
            OpenError = error;
        }

        public bool IsAvailable()
        {
            return _mixer != null;
        }

        /// <param name="volume">0.0 (silent) to 1.0 (full), e.g. by distance.</param>
        public void PlayShot(double volume)
        {
            if (_mixer != null)
            {
                _mixer.Play(_shot, volume);
            }
        }

        public void PlayHitDealt(double volume)
        {
            if (_mixer != null)
            {
                _mixer.Play(_hitDealt, volume);
            }
        }

        public void PlayHitTaken(double volume)
        {
            if (_mixer != null)
            {
                _mixer.Play(_hitTaken, volume);
            }
        }

        /// <summary>One line for the debug HUD and the --audiotest mode.</summary>
        public string DebugStatus()
        {
            if (_mixer == null)
            {
                return "AUDIO OFF, WAVEOUT ERROR " + OpenError;
            }
            return "AUDIO OK, BUFFERS " + _mixer.BuffersWritten
                + ", VOICES " + _mixer.ActiveVoices()
                + ", WRITE ERROR " + _mixer.LastWriteError;
        }

        public void Dispose()
        {
            if (_mixer != null)
            {
                _mixer.Dispose();
            }
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
    }
}
