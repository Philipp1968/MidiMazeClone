namespace MidiMazeClone
{
    /// <summary>
    /// Draws the world into a FrameBuffer.
    ///
    /// The first-person view is a raycaster: one ray per screen column is stepped through
    /// the grid (DDA) until it hits a wall. The wall slice height is focal length / distance.
    /// The distance of every column is kept in a z-buffer so sprites (smileys, shots) drawn
    /// afterwards can be hidden column by column behind nearer walls.
    /// </summary>
    public sealed class Renderer
    {
        public const double FieldOfViewDegrees = 66.0;
        public const double WallHeight = 1.0;        // in cells; the corridor is 1.0 wide
        public const int MaxRaySteps = 256;
        public const double SmileyRadius = 0.32;     // drawn radius in cells (collision radius is Player.Radius)

        /// <summary>One sprite to draw this frame, already projected to the screen.</summary>
        private sealed class SpriteItem
        {
            public double Depth;        // distance along the view direction, in cells
            public double ScreenX;      // centre column in pixels (may be off screen)
            public double RadiusPixels;
            public Player? Player;
        }

        private readonly int _width;
        private readonly int _height;

        /// <summary>Half width of the camera plane at distance 1, i.e. tan(fov / 2).</summary>
        private readonly double _planeScale;

        /// <summary>Distance from the eye to the screen in pixels; makes square pixels come out square.</summary>
        private readonly double _focalLength;

        /// <summary>Perpendicular wall distance per screen column, filled by DrawWalls.</summary>
        private readonly double[] _zBuffer;

        public Renderer(int width, int height)
        {
            _width = width;
            _height = height;
            _planeScale = Math.Tan(FieldOfViewDegrees * Math.PI / 360.0);
            _focalLength = (width / 2.0) / _planeScale;
            _zBuffer = new double[width];
        }

        /// <summary>First-person view from the viewer's eyes into the whole frame buffer.</summary>
        public void DrawView(World world, Player viewer, FrameBuffer fb)
        {
            int horizon = _height / 2;

            fb.FillRect(0, 0, _width, horizon, Palette.Ceiling);
            fb.FillRect(0, horizon, _width, _height - horizon, Palette.Floor);

            DrawWalls(world.Maze, viewer, fb, horizon);
            DrawSprites(world, viewer, fb, horizon);
        }

        /// <summary>
        /// Project every other player onto the screen, sort far to near, and draw them
        /// column by column wherever they are closer than the wall in the z-buffer.
        /// </summary>
        private void DrawSprites(World world, Player viewer, FrameBuffer fb, int horizon)
        {
            double dirX = viewer.DirectionX();
            double dirY = viewer.DirectionY();
            double rightX = -dirY;
            double rightY = dirX;

            List<SpriteItem> items = new List<SpriteItem>();

            for (int i = 0; i < world.Players.Count; i++)
            {
                Player player = world.Players[i];
                if (player == viewer)
                {
                    continue;
                }

                // A freshly respawned player blinks while the shield is up.
                if (player.Shield > 0.0)
                {
                    int blinkPhase = (int)(world.Time * 8.0) % 2;
                    if (blinkPhase == 0)
                    {
                        continue;
                    }
                }

                double relX = player.X - viewer.X;
                double relY = player.Y - viewer.Y;
                double depth = relX * dirX + relY * dirY;
                if (depth <= 0.05)
                {
                    continue;   // behind the viewer or on top of them
                }

                double lateral = relX * rightX + relY * rightY;
                double screenX = _width / 2.0 + (lateral / depth) * _focalLength;
                double radiusPixels = SmileyRadius * _focalLength / depth;

                if (screenX + radiusPixels < 0.0 || screenX - radiusPixels >= _width)
                {
                    continue;   // completely off screen
                }

                SpriteItem item = new SpriteItem();
                item.Depth = depth;
                item.ScreenX = screenX;
                item.RadiusPixels = radiusPixels;
                item.Player = player;
                items.Add(item);
            }

            SortFarToNear(items);

            for (int i = 0; i < items.Count; i++)
            {
                SpriteItem item = items[i];
                if (item.Player != null)
                {
                    DrawSmiley(fb, item, item.Player, viewer, rightX, rightY, horizon);
                }
            }
        }

        /// <summary>Insertion sort by depth, largest first. The list is tiny, so this is plenty.</summary>
        private static void SortFarToNear(List<SpriteItem> items)
        {
            for (int i = 1; i < items.Count; i++)
            {
                SpriteItem current = items[i];
                int j = i - 1;
                while (j >= 0 && items[j].Depth < current.Depth)
                {
                    items[j + 1] = items[j];
                    j--;
                }
                items[j + 1] = current;
            }
        }

        /// <summary>
        /// A smiley is a sphere at eye height. Its face is drawn in sphere coordinates
        /// (u, v in -1..1) and shifted sideways according to where the player is looking,
        /// so you can tell whether an opponent faces you, looks past you, or shows their back.
        /// </summary>
        private void DrawSmiley(FrameBuffer fb, SpriteItem item, Player player, Player viewer,
            double rightX, double rightY, int horizon)
        {
            double toViewerX = viewer.X - player.X;
            double toViewerY = viewer.Y - player.Y;
            double toViewerLength = Math.Sqrt(toViewerX * toViewerX + toViewerY * toViewerY);
            if (toViewerLength < 0.001)
            {
                toViewerLength = 0.001;
            }
            toViewerX /= toViewerLength;
            toViewerY /= toViewerLength;

            double headX = player.DirectionX();
            double headY = player.DirectionY();

            // 1.0 = looking straight at us, 0 = profile, -1.0 = looking away.
            double facing = headX * toViewerX + headY * toViewerY;
            // > 0 = looking towards our screen right, so the face slides to the right.
            double sideways = headX * rightX + headY * rightY;

            bool showFace = facing > -0.15;
            double faceShift = sideways * 0.75;
            double faceSquash = facing;
            if (faceSquash < 0.25)
            {
                faceSquash = 0.25;
            }

            DrawBall(fb, item.ScreenX, horizon, item.RadiusPixels, item.Depth,
                player.Color, true, showFace, faceShift, faceSquash);
        }

        /// <summary>
        /// Draw a sphere of the given screen radius, clipped per column against the z-buffer.
        /// With showFace the eyes and mouth are added, shifted by faceShift and squashed by faceSquash.
        /// </summary>
        private void DrawBall(FrameBuffer fb, double centerX, double centerY, double radius, double depth,
            int bodyColor, bool outline, bool showFace, double faceShift, double faceSquash)
        {
            if (radius < 0.5)
            {
                return;
            }

            // Outline about 1.5 pixels wide, but never more than 40 percent of the radius.
            double outlineStart = 1.0 - 1.5 / radius;
            if (outlineStart < 0.6)
            {
                outlineStart = 0.6;
            }
            if (!outline)
            {
                outlineStart = 2.0;
            }

            int firstColumn = MathUtil.FloorToInt(centerX - radius);
            int lastColumn = (int)Math.Ceiling(centerX + radius);
            if (firstColumn < 0)
            {
                firstColumn = 0;
            }
            if (lastColumn > _width - 1)
            {
                lastColumn = _width - 1;
            }

            int[] pixels = fb.Pixels;

            for (int column = firstColumn; column <= lastColumn; column++)
            {
                if (depth >= _zBuffer[column])
                {
                    continue;   // a wall is in front of this column
                }

                double u = (column + 0.5 - centerX) / radius;
                if (u <= -1.0 || u >= 1.0)
                {
                    continue;
                }

                double halfChord = Math.Sqrt(1.0 - u * u);
                int firstRow = MathUtil.FloorToInt(centerY - halfChord * radius);
                int lastRow = (int)Math.Ceiling(centerY + halfChord * radius);
                if (firstRow < 0)
                {
                    firstRow = 0;
                }
                if (lastRow > _height - 1)
                {
                    lastRow = _height - 1;
                }

                for (int row = firstRow; row <= lastRow; row++)
                {
                    double v = (row + 0.5 - centerY) / radius;
                    double distanceSquared = u * u + v * v;
                    if (distanceSquared > 1.0)
                    {
                        continue;
                    }

                    int color = bodyColor;
                    double distance = Math.Sqrt(distanceSquared);
                    if (distance > outlineStart)
                    {
                        color = Palette.SmileyOutline;
                    }
                    else if (showFace)
                    {
                        color = FaceColor(u, v, faceShift, faceSquash, bodyColor);
                    }

                    pixels[row * _width + column] = color;
                }
            }
        }

        /// <summary>Eyes and mouth in sphere coordinates. v grows downwards like screen rows.</summary>
        private static int FaceColor(double u, double v, double faceShift, double faceSquash, int bodyColor)
        {
            double fu = (u - faceShift) / faceSquash;

            // Two eyes, centred at (+-0.36, -0.30), radius 0.13.
            double eyeDx = Math.Abs(fu) - 0.36;
            double eyeDy = v + 0.30;
            if (eyeDx * eyeDx + eyeDy * eyeDy < 0.13 * 0.13)
            {
                return Palette.SmileyFeature;
            }

            // Smile: the lower part of a ring between radius 0.52 and 0.66.
            double distance = Math.Sqrt(fu * fu + v * v);
            if (distance > 0.52 && distance < 0.66 && v > 0.22)
            {
                return Palette.SmileyFeature;
            }

            return bodyColor;
        }

        private void DrawWalls(Maze maze, Player viewer, FrameBuffer fb, int horizon)
        {
            double dirX = viewer.DirectionX();
            double dirY = viewer.DirectionY();

            // The camera plane is perpendicular to the view direction; +plane is screen right.
            double planeX = -dirY * _planeScale;
            double planeY = dirX * _planeScale;

            for (int column = 0; column < _width; column++)
            {
                // -1.0 at the left edge of the screen, +1.0 at the right edge, sampled at pixel centres.
                double cameraX = (2.0 * (column + 0.5) / _width) - 1.0;
                double rayDirX = dirX + planeX * cameraX;
                double rayDirY = dirY + planeY * cameraX;

                // DDA setup: which cell we are in, and how far along the ray one cell step is per axis.
                int mapX = MathUtil.FloorToInt(viewer.X);
                int mapY = MathUtil.FloorToInt(viewer.Y);

                double deltaDistX = 1e30;
                if (rayDirX != 0.0)
                {
                    deltaDistX = Math.Abs(1.0 / rayDirX);
                }
                double deltaDistY = 1e30;
                if (rayDirY != 0.0)
                {
                    deltaDistY = Math.Abs(1.0 / rayDirY);
                }

                int stepX;
                double sideDistX;
                if (rayDirX < 0.0)
                {
                    stepX = -1;
                    sideDistX = (viewer.X - mapX) * deltaDistX;
                }
                else
                {
                    stepX = 1;
                    sideDistX = (mapX + 1.0 - viewer.X) * deltaDistX;
                }

                int stepY;
                double sideDistY;
                if (rayDirY < 0.0)
                {
                    stepY = -1;
                    sideDistY = (viewer.Y - mapY) * deltaDistY;
                }
                else
                {
                    stepY = 1;
                    sideDistY = (mapY + 1.0 - viewer.Y) * deltaDistY;
                }

                // Step cell by cell until a wall is hit. Crossing an x boundary means we hit a
                // wall face that runs north-south; crossing a y boundary one that runs east-west.
                bool hitNorthSouthFace = false;
                int steps = 0;
                while (steps < MaxRaySteps)
                {
                    if (sideDistX < sideDistY)
                    {
                        sideDistX += deltaDistX;
                        mapX += stepX;
                        hitNorthSouthFace = true;
                    }
                    else
                    {
                        sideDistY += deltaDistY;
                        mapY += stepY;
                        hitNorthSouthFace = false;
                    }

                    if (maze.IsWall(mapX, mapY))
                    {
                        break;
                    }
                    steps++;
                }

                // Distance measured along the view direction (not along the ray) avoids the fisheye look.
                double perpendicularDistance;
                if (hitNorthSouthFace)
                {
                    perpendicularDistance = sideDistX - deltaDistX;
                }
                else
                {
                    perpendicularDistance = sideDistY - deltaDistY;
                }
                if (perpendicularDistance < 0.01)
                {
                    perpendicularDistance = 0.01;
                }
                _zBuffer[column] = perpendicularDistance;

                int lineHeight = (int)(WallHeight * _focalLength / perpendicularDistance);
                int top = horizon - lineHeight / 2;
                int bottom = top + lineHeight - 1;

                int color = Palette.WallDark;
                if (hitNorthSouthFace)
                {
                    color = Palette.WallLight;
                }

                fb.DrawVerticalLine(column, top, bottom, color);
            }
        }

        /// <summary>
        /// Top-down map with the given pixel size per cell, drawn at (originX, originY).
        /// Used large as a debug view and small as the mini map.
        /// </summary>
        public void DrawMap(World world, FrameBuffer fb, int originX, int originY, int cellSize)
        {
            Maze maze = world.Maze;

            for (int y = 0; y < maze.Height; y++)
            {
                for (int x = 0; x < maze.Width; x++)
                {
                    int color = Palette.MapFloor;
                    if (maze.IsWall(x, y))
                    {
                        color = Palette.MapWall;
                    }
                    fb.FillRect(originX + x * cellSize, originY + y * cellSize, cellSize, cellSize, color);
                }
            }

            for (int i = 0; i < world.Players.Count; i++)
            {
                Player player = world.Players[i];

                int px = originX + (int)(player.X * cellSize);
                int py = originY + (int)(player.Y * cellSize);
                int radius = (int)(Player.Radius * cellSize);
                if (radius < 1)
                {
                    radius = 1;
                }
                fb.FillCircle(px, py, radius, player.Color);

                int headingX = originX + (int)((player.X + player.DirectionX() * 0.7) * cellSize);
                int headingY = originY + (int)((player.Y + player.DirectionY() * 0.7) * cellSize);
                fb.DrawLine(px, py, headingX, headingY, Palette.White);
            }
        }
    }
}
