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
