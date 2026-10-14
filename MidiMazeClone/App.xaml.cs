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
