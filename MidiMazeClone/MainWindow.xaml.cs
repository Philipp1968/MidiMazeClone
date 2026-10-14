using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MidiMazeClone
{
    /// <summary>
    /// WPF plumbing only: vsync-paced game loop, keyboard state, and the blit of the
    /// software frame buffer into a WriteableBitmap. All game logic lives in Game.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Game _game;
        private readonly WriteableBitmap _bitmap;
        private readonly Int32Rect _wholeScreen;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private double _lastFrameTime;
        private double _accumulator;
        private TimeSpan _lastRenderingTime;

        private int _framesThisSecond;
        private double _fpsWindowStart;
        private int _fps;

        private bool _fullscreen;
        private WindowState _savedWindowState;

        public MainWindow(Game game)
        {
            InitializeComponent();

            _game = game;
            _bitmap = new WriteableBitmap(Game.ViewWidth, Game.ViewHeight, 96, 96, PixelFormats.Bgr32, null);
            _wholeScreen = new Int32Rect(0, 0, Game.ViewWidth, Game.ViewHeight);
            ScreenImage.Source = _bitmap;

            Loaded += OnLoaded;
            Closed += OnClosed;
            Deactivated += OnDeactivated;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Focus();
            _lastFrameTime = _clock.Elapsed.TotalSeconds;
            _fpsWindowStart = _lastFrameTime;
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            _game.Shutdown();
        }

        private void OnDeactivated(object? sender, EventArgs e)
        {
            // Keys released while another window had focus would otherwise stay "pressed".
            _game.LocalControls.Clear();
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            // WPF may raise Rendering more than once per vsync; only act on a new frame time.
            RenderingEventArgs? renderingArgs = e as RenderingEventArgs;
            if (renderingArgs != null)
            {
                if (renderingArgs.RenderingTime == _lastRenderingTime)
                {
                    return;
                }
                _lastRenderingTime = renderingArgs.RenderingTime;
            }

            double now = _clock.Elapsed.TotalSeconds;
            double elapsed = now - _lastFrameTime;
            _lastFrameTime = now;

            // After a breakpoint or a window drag, do not try to catch up for seconds.
            if (elapsed > 0.25)
            {
                elapsed = 0.25;
            }

            // Fixed time step simulation, variable frame rate rendering.
            _accumulator += elapsed;
            while (_accumulator >= Game.StepSeconds)
            {
                _game.Update(Game.StepSeconds);
                _accumulator -= Game.StepSeconds;
            }

            _framesThisSecond++;
            if (now - _fpsWindowStart >= 1.0)
            {
                _fps = _framesThisSecond;
                _framesThisSecond = 0;
                _fpsWindowStart = now;
            }

            _game.Render(_fps);
            _bitmap.WritePixels(_wholeScreen, _game.Screen.Pixels, Game.ViewWidth * 4, 0);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.IsRepeat)
            {
                HandleOneShotKey(e.Key);
            }

            SetControl(e.Key, true);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            SetControl(e.Key, false);
        }

        private void HandleOneShotKey(Key key)
        {
            if (key == Key.Escape)
            {
                Close();
                return;
            }

            if (key == Key.F11)
            {
                ToggleFullscreen();
                return;
            }

            if (key == Key.M)
            {
                _game.ShowMap = !_game.ShowMap;
                return;
            }

            if (key == Key.R)
            {
                _game.Restart();
                return;
            }
        }

        private void SetControl(Key key, bool pressed)
        {
            Controls controls = _game.LocalControls;

            switch (key)
            {
                case Key.Left:
                case Key.A:
                    controls.TurnLeft = pressed;
                    break;

                case Key.Right:
                case Key.D:
                    controls.TurnRight = pressed;
                    break;

                case Key.Up:
                case Key.W:
                    controls.Forward = pressed;
                    break;

                case Key.Down:
                case Key.S:
                    controls.Backward = pressed;
                    break;

                case Key.Space:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    controls.Fire = pressed;
                    break;
            }
        }

        private void ToggleFullscreen()
        {
            if (!_fullscreen)
            {
                _savedWindowState = WindowState;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
                _fullscreen = true;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                WindowState = _savedWindowState;
                _fullscreen = false;
            }
        }
    }
}
