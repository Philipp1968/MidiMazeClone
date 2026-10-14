namespace MidiMazeClone
{
    /// <summary>
    /// Colours as 0x00RRGGBB ints. The frame buffer is Bgr32, so the top byte is ignored.
    /// Everything here is a starting point and meant to be tuned by eye.
    /// </summary>
    public static class Palette
    {
        public const int Black = 0x000000;
        public const int White = 0xFFFFFF;
        public const int Red = 0xE03030;
        public const int DarkGrey = 0x404040;
        public const int Grey = 0x808080;

        // 3D view
        public const int Ceiling = 0x202028;
        public const int Floor = 0x585860;
        public const int WallLight = 0x4888E8;   // walls running north-south (hit on an x boundary)
        public const int WallDark = 0x2C5CB0;    // walls running east-west (hit on a y boundary)

        // Smileys
        public const int SmileyOutline = 0x201800;
        public const int SmileyFeature = 0x101010;

        // Player colours (body of the smiley, map dot, HUD name)
        public static readonly int[] PlayerColors =
        {
            0xF0E020,   // yellow
            0x40E040,   // green
            0x40E0E0,   // cyan
            0xF060C0,   // pink
        };

        // Top-down map
        public const int MapWall = 0x8090B0;
        public const int MapFloor = 0x101014;
        public const int MapBorder = 0xC0C0C0;

        public static int FromRgb(int red, int green, int blue)
        {
            return (red << 16) | (green << 8) | blue;
        }

        /// <summary>Blend two colours. amount 0.0 = a, 1.0 = b.</summary>
        public static int Blend(int a, int b, double amount)
        {
            if (amount <= 0.0)
            {
                return a;
            }
            if (amount >= 1.0)
            {
                return b;
            }

            int redA = (a >> 16) & 0xFF;
            int greenA = (a >> 8) & 0xFF;
            int blueA = a & 0xFF;
            int redB = (b >> 16) & 0xFF;
            int greenB = (b >> 8) & 0xFF;
            int blueB = b & 0xFF;

            int red = (int)(redA + (redB - redA) * amount);
            int green = (int)(greenA + (greenB - greenA) * amount);
            int blue = (int)(blueA + (blueB - blueA) * amount);

            return FromRgb(red, green, blue);
        }
    }
}
