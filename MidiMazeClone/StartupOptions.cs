using System.Globalization;

namespace MidiMazeClone
{
    /// <summary>
    /// Command line options.
    /// Example: MidiMazeClone.exe --maze Mazes\classic.txt --map
    /// Example: MidiMazeClone.exe --screenshot frame.png --seed 1 --warmup 2
    /// </summary>
    public sealed class StartupOptions
    {
        public string? MazePath;
        public string? ScreenshotPath;
        public bool ShowMap;
        public bool DriveForward;        // screenshot mode: hold "forward" during the warmup
        public bool HoldFire;            // screenshot mode: hold "fire" during the warmup
        public string? Scene;            // fixed test scene instead of a normal game, e.g. "lineup"
        public int Seed;                 // 0 = pick a random seed
        public double WarmupSeconds;     // simulation time to run before taking a screenshot

        public static StartupOptions Parse(string[] args)
        {
            StartupOptions options = new StartupOptions();

            int i = 0;
            while (i < args.Length)
            {
                string arg = args[i];
                string? next = null;
                if (i + 1 < args.Length)
                {
                    next = args[i + 1];
                }

                if (arg == "--maze" && next != null)
                {
                    options.MazePath = next;
                    i += 2;
                    continue;
                }

                if (arg == "--screenshot" && next != null)
                {
                    options.ScreenshotPath = next;
                    i += 2;
                    continue;
                }

                if (arg == "--seed" && next != null)
                {
                    options.Seed = int.Parse(next, CultureInfo.InvariantCulture);
                    i += 2;
                    continue;
                }

                if (arg == "--warmup" && next != null)
                {
                    options.WarmupSeconds = double.Parse(next, CultureInfo.InvariantCulture);
                    i += 2;
                    continue;
                }

                if (arg == "--scene" && next != null)
                {
                    options.Scene = next;
                    i += 2;
                    continue;
                }

                if (arg == "--forward")
                {
                    options.DriveForward = true;
                    i += 1;
                    continue;
                }

                if (arg == "--fire")
                {
                    options.HoldFire = true;
                    i += 1;
                    continue;
                }

                if (arg == "--map")
                {
                    options.ShowMap = true;
                    i += 1;
                    continue;
                }

                // Unknown argument: ignore it.
                i += 1;
            }

            return options;
        }
    }
}
