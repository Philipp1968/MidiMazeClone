using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MidiMazeClone
{
    /// <summary>
    /// Application entry. Normally opens the game window; with "--screenshot file.png"
    /// it renders one frame headlessly to a PNG and exits (used for visual checks).
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            StartupOptions options = StartupOptions.Parse(e.Args);

            if (options.AudioTestPath != null)
            {
                RunAudioTest(options.AudioTestPath);
                Shutdown();
                return;
            }

            Game game = new Game(options);

            if (options.ScreenshotPath != null)
            {
                RenderScreenshot(game, options);
                Shutdown();
                return;
            }

            MainWindow window = new MainWindow(game);
            window.Show();
        }

        /// <summary>Play shot, hit dealt and hit taken in a row, then write the mixer status to a file.</summary>
        private static void RunAudioTest(string path)
        {
            Sounds sounds = new Sounds();
            string statusAfterOpen = sounds.DebugStatus();

            sounds.PlayShot(1.0);
            System.Threading.Thread.Sleep(300);
            sounds.PlayHitDealt(1.0);
            System.Threading.Thread.Sleep(300);
            sounds.PlayHitTaken(0.8);
            sounds.PlayShot(0.3);   // overlaps the crunch on purpose
            string statusWhilePlaying = sounds.DebugStatus();
            System.Threading.Thread.Sleep(600);
            string statusAtEnd = sounds.DebugStatus();

            sounds.Dispose();

            string report = "after open:    " + statusAfterOpen + Environment.NewLine
                + "while playing: " + statusWhilePlaying + Environment.NewLine
                + "at end:        " + statusAtEnd + Environment.NewLine;
            File.WriteAllText(path, report);
        }

        private static void RenderScreenshot(Game game, StartupOptions options)
        {
            // Let the simulation run for a while so the screenshot is not just the first frame.
            game.LocalControls.Forward = options.DriveForward;
            game.LocalControls.Fire = options.HoldFire;
            double simulated = 0.0;
            while (simulated < options.WarmupSeconds)
            {
                game.Update(Game.StepSeconds);
                simulated += Game.StepSeconds;
            }

            game.Render(60);

            FrameBuffer screen = game.Screen;
            WriteableBitmap bitmap = new WriteableBitmap(screen.Width, screen.Height, 96, 96, PixelFormats.Bgr32, null);
            Int32Rect whole = new Int32Rect(0, 0, screen.Width, screen.Height);
            bitmap.WritePixels(whole, screen.Pixels, screen.Width * 4, 0);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string path = options.ScreenshotPath!;
            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }
    }
}
