namespace MidiMazeClone
{
    /// <summary>
    /// The game itself, independent of WPF: owns the frame buffer and the simulation,
    /// advances the simulation in fixed steps and renders frames into the buffer.
    /// </summary>
    public sealed class Game
    {
        public const int ViewWidth = 320;
        public const int ViewHeight = 200;
        public const double StepSeconds = 1.0 / 60.0;

        public readonly FrameBuffer Screen;
        public readonly Controls LocalControls;
        public bool ShowMap;

        private double _time;

        private static readonly int[] TestPatternColors =
        {
            0x000000, 0xE03030, 0x30E030, 0xE0E030,
            0x3030E0, 0xE030E0, 0x30E0E0, 0xFFFFFF,
        };

        public Game(StartupOptions options)
        {
            Screen = new FrameBuffer(ViewWidth, ViewHeight);
            LocalControls = new Controls();
            ShowMap = options.ShowMap;
        }

        public void Update(double dt)
        {
            _time += dt;
        }

        public void Render(int fps)
        {
            DrawTestPattern();

            string title = "MIDIMAZE CLONE";
            int titleX = (ViewWidth - FrameBuffer.TextWidth(title, 2)) / 2;
            Screen.DrawTextShadowed(titleX, 80, title, Palette.White, 2);

            string subtitle = "MILESTONE 1: WINDOW, BUFFER, LOOP";
            int subtitleX = (ViewWidth - FrameBuffer.TextWidth(subtitle, 1)) / 2;
            Screen.DrawTextShadowed(subtitleX, 100, subtitle, Palette.White, 1);

            string controlsHint = "LEFT " + OnOff(LocalControls.TurnLeft)
                + "  RIGHT " + OnOff(LocalControls.TurnRight)
                + "  FWD " + OnOff(LocalControls.Forward)
                + "  BACK " + OnOff(LocalControls.Backward)
                + "  FIRE " + OnOff(LocalControls.Fire);
            int hintX = (ViewWidth - FrameBuffer.TextWidth(controlsHint, 1)) / 2;
            Screen.DrawTextShadowed(hintX, 180, controlsHint, Palette.White, 1);

            string fpsText = fps + " FPS";
            Screen.DrawTextShadowed(ViewWidth - FrameBuffer.TextWidth(fpsText, 1) - 2, 2, fpsText, Palette.White, 1);
        }

        private static string OnOff(bool value)
        {
            if (value)
            {
                return "1";
            }
            return "0";
        }

        /// <summary>Moving diagonal colour bands, so a frozen loop is obvious at a glance.</summary>
        private void DrawTestPattern()
        {
            int offset = (int)(_time * 60.0);
            int[] pixels = Screen.Pixels;

            for (int y = 0; y < ViewHeight; y++)
            {
                int rowStart = y * ViewWidth;
                for (int x = 0; x < ViewWidth; x++)
                {
                    int band = ((x + y + offset) / 16) % TestPatternColors.Length;
                    pixels[rowStart + x] = TestPatternColors[band];
                }
            }
        }
    }
}
