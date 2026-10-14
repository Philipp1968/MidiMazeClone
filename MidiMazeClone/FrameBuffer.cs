namespace MidiMazeClone
{
    /// <summary>
    /// Software frame buffer: one int per pixel (0x00RRGGBB), row-major, top-left origin.
    /// All drawing helpers clip to the buffer, so callers can be sloppy with coordinates.
    /// </summary>
    public sealed class FrameBuffer
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int[] Pixels;

        public FrameBuffer(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new int[width * height];
        }

        public void Clear(int color)
        {
            Array.Fill(Pixels, color);
        }

        public void SetPixel(int x, int y, int color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }
            Pixels[y * Width + x] = color;
        }

        /// <summary>Fill a rectangle given by top-left corner and size.</summary>
        public void FillRect(int x, int y, int width, int height, int color)
        {
            int x0 = x;
            int y0 = y;
            int x1 = x + width;     // exclusive
            int y1 = y + height;    // exclusive

            if (x0 < 0)
            {
                x0 = 0;
            }
            if (y0 < 0)
            {
                y0 = 0;
            }
            if (x1 > Width)
            {
                x1 = Width;
            }
            if (y1 > Height)
            {
                y1 = Height;
            }

            for (int py = y0; py < y1; py++)
            {
                int rowStart = py * Width;
                for (int px = x0; px < x1; px++)
                {
                    Pixels[rowStart + px] = color;
                }
            }
        }

        /// <summary>Vertical line from y0 to y1 inclusive.</summary>
        public void DrawVerticalLine(int x, int y0, int y1, int color)
        {
            if (x < 0 || x >= Width)
            {
                return;
            }
            if (y0 > y1)
            {
                int swap = y0;
                y0 = y1;
                y1 = swap;
            }
            if (y0 < 0)
            {
                y0 = 0;
            }
            if (y1 >= Height)
            {
                y1 = Height - 1;
            }

            int index = y0 * Width + x;
            for (int py = y0; py <= y1; py++)
            {
                Pixels[index] = color;
                index += Width;
            }
        }

        /// <summary>Horizontal line from x0 to x1 inclusive.</summary>
        public void DrawHorizontalLine(int x0, int x1, int y, int color)
        {
            if (y < 0 || y >= Height)
            {
                return;
            }
            if (x0 > x1)
            {
                int swap = x0;
                x0 = x1;
                x1 = swap;
            }
            if (x0 < 0)
            {
                x0 = 0;
            }
            if (x1 >= Width)
            {
                x1 = Width - 1;
            }

            int rowStart = y * Width;
            for (int px = x0; px <= x1; px++)
            {
                Pixels[rowStart + px] = color;
            }
        }

        /// <summary>Bresenham line, used for the map view (headings, shots).</summary>
        public void DrawLine(int x0, int y0, int x1, int y1, int color)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int stepX = x0 < x1 ? 1 : -1;
            int stepY = y0 < y1 ? 1 : -1;
            int error = dx + dy;

            int x = x0;
            int y = y0;
            int guard = 0;
            while (guard < 10000)
            {
                SetPixel(x, y, color);
                if (x == x1 && y == y1)
                {
                    break;
                }

                int doubledError = 2 * error;
                if (doubledError >= dy)
                {
                    error += dy;
                    x += stepX;
                }
                if (doubledError <= dx)
                {
                    error += dx;
                    y += stepY;
                }
                guard++;
            }
        }

        public void FillCircle(int centerX, int centerY, int radius, int color)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int py = centerY + dy;
                if (py < 0 || py >= Height)
                {
                    continue;
                }

                int halfChord = (int)Math.Sqrt(radius * radius - dy * dy);
                DrawHorizontalLine(centerX - halfChord, centerX + halfChord, py, color);
            }
        }

        public static int TextWidth(string text, int scale)
        {
            return text.Length * TinyFont.Advance * scale;
        }

        public void DrawText(int x, int y, string text, int color, int scale)
        {
            int penX = x;
            for (int i = 0; i < text.Length; i++)
            {
                string[]? glyph = TinyFont.GetGlyph(text[i]);
                if (glyph != null)
                {
                    DrawGlyph(penX, y, glyph, color, scale);
                }
                penX += TinyFont.Advance * scale;
            }
        }

        /// <summary>Text with a one pixel black drop shadow, readable on any background.</summary>
        public void DrawTextShadowed(int x, int y, string text, int color, int scale)
        {
            DrawText(x + scale, y + scale, text, Palette.Black, scale);
            DrawText(x, y, text, color, scale);
        }

        private void DrawGlyph(int x, int y, string[] rows, int color, int scale)
        {
            for (int row = 0; row < rows.Length; row++)
            {
                string bits = rows[row];
                for (int column = 0; column < bits.Length; column++)
                {
                    if (bits[column] != '#')
                    {
                        continue;
                    }

                    if (scale == 1)
                    {
                        SetPixel(x + column, y + row, color);
                    }
                    else
                    {
                        FillRect(x + column * scale, y + row * scale, scale, scale, color);
                    }
                }
            }
        }
    }
}
