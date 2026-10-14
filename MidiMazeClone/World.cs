namespace MidiMazeClone
{
    /// <summary>
    /// The simulation: maze, players and (later) shots. Advanced in fixed time steps.
    /// Everything random goes through the seeded Random, so a replay or a lockstep
    /// network game can reproduce the same world.
    /// </summary>
    public sealed class World
    {
        public const double TurnSpeed = 2.6;          // radians per second
        public const double MoveSpeed = 2.4;          // cells per second
        public const double ShieldSeconds = 2.0;      // invulnerability after a respawn
        public const double MinSpawnDistance = 4.0;   // cells away from other players, if possible

        public readonly Maze Maze;
        public readonly Random Random;
        public readonly List<Player> Players = new List<Player>();

        public double Time;

        public World(Maze maze, int seed)
        {
            Maze = maze;
            Random = new Random(seed);
        }

        public Player AddPlayer(string name, int color, bool isBot)
        {
            Player player = new Player(name, color, isBot);
            Players.Add(player);
            Respawn(player);
            return player;
        }

        public void Update(double dt)
        {
            Time += dt;

            for (int i = 0; i < Players.Count; i++)
            {
                UpdatePlayer(Players[i], dt);
            }
        }

        private void UpdatePlayer(Player player, double dt)
        {
            Controls controls = player.Controls;

            if (controls.TurnLeft)
            {
                player.Angle -= TurnSpeed * dt;
            }
            if (controls.TurnRight)
            {
                player.Angle += TurnSpeed * dt;
            }
            player.Angle = MathUtil.NormalizeAngle(player.Angle);

            double move = 0.0;
            if (controls.Forward)
            {
                move += 1.0;
            }
            if (controls.Backward)
            {
                move -= 1.0;
            }

            if (move != 0.0)
            {
                double distance = move * MoveSpeed * dt;
                double dx = player.DirectionX() * distance;
                double dy = player.DirectionY() * distance;
                MoveWithCollision(player, dx, dy);
            }

            if (player.Shield > 0.0)
            {
                player.Shield -= dt;
            }
            if (player.HitFlash > 0.0)
            {
                player.HitFlash -= dt;
            }
            if (player.FireCooldown > 0.0)
            {
                player.FireCooldown -= dt;
            }
        }

        /// <summary>
        /// Move one axis at a time, so a blocked axis does not stop the other one.
        /// That is what lets the player slide along a wall instead of sticking to it.
        /// </summary>
        private void MoveWithCollision(Player player, double dx, double dy)
        {
            double newX = player.X + dx;
            if (IsFreeForPlayer(newX, player.Y))
            {
                player.X = newX;
            }

            double newY = player.Y + dy;
            if (IsFreeForPlayer(player.X, newY))
            {
                player.Y = newY;
            }
        }

        /// <summary>
        /// True if a player centred at (x, y) touches no wall cell. The round player is
        /// treated as a square of the same size, which is good enough in a grid maze.
        /// </summary>
        public bool IsFreeForPlayer(double x, double y)
        {
            int minCellX = MathUtil.FloorToInt(x - Player.Radius);
            int maxCellX = MathUtil.FloorToInt(x + Player.Radius);
            int minCellY = MathUtil.FloorToInt(y - Player.Radius);
            int maxCellY = MathUtil.FloorToInt(y + Player.Radius);

            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    if (Maze.IsWall(cellX, cellY))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Put the player on a random free cell, away from the others when possible.</summary>
        public void Respawn(Player player)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                int cellX = Random.Next(Maze.Width);
                int cellY = Random.Next(Maze.Height);
                if (Maze.IsWall(cellX, cellY))
                {
                    continue;
                }

                double x = cellX + 0.5;
                double y = cellY + 0.5;

                // Insist on distance for the first attempts, then take whatever is free.
                if (attempt < 150 && !IsFarFromOtherPlayers(player, x, y))
                {
                    continue;
                }

                player.X = x;
                player.Y = y;
                player.Angle = Random.Next(4) * MathUtil.HalfPi;
                player.Shield = ShieldSeconds;
                return;
            }

            // Degenerate maze: fall back to the first free cell.
            for (int cellY = 0; cellY < Maze.Height; cellY++)
            {
                for (int cellX = 0; cellX < Maze.Width; cellX++)
                {
                    if (!Maze.IsWall(cellX, cellY))
                    {
                        player.X = cellX + 0.5;
                        player.Y = cellY + 0.5;
                        player.Shield = ShieldSeconds;
                        return;
                    }
                }
            }
        }

        private bool IsFarFromOtherPlayers(Player player, double x, double y)
        {
            double minDistanceSquared = MinSpawnDistance * MinSpawnDistance;

            for (int i = 0; i < Players.Count; i++)
            {
                Player other = Players[i];
                if (other == player)
                {
                    continue;
                }

                double dx = other.X - x;
                double dy = other.Y - y;
                if (dx * dx + dy * dy < minDistanceSquared)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
