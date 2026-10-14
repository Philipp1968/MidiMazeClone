namespace MidiMazeClone
{
    /// <summary>
    /// The simulation: maze, players and shots. Advanced in fixed time steps.
    /// Everything random goes through the seeded Random, so a replay or a lockstep
    /// network game can reproduce the same world.
    /// </summary>
    public sealed class World
    {
        public const double TurnSpeed = 3.0;          // radians per second for humans (2.6 felt a bit slow)
        public const double BotTurnSpeed = 2.6;       // bots keep the old, slower rate
        public const double MoveSpeed = 2.4;          // cells per second
        public const double FireInterval = 0.5;       // seconds between two shots of one player
        public const double ShieldSeconds = 2.0;      // invulnerability after a respawn
        public const double HitFlashSeconds = 0.5;    // red flash in the victim's view
        public const double MinSpawnDistance = 4.0;   // cells away from other players, if possible
        public const double ShotSubStep = 0.1;        // max cells a shot moves per collision check

        public readonly Maze Maze;
        public readonly Random Random;
        public readonly List<Player> Players = new List<Player>();
        public readonly List<Shot> Shots = new List<Shot>();

        public int WinScore = 10;
        public Player? Winner;
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

        /// <summary>New round: scores to zero, shots gone, everybody respawns.</summary>
        public void Restart()
        {
            Shots.Clear();
            Winner = null;

            for (int i = 0; i < Players.Count; i++)
            {
                Player player = Players[i];
                player.Score = 0;
                player.Hits = 0;
                player.FireCooldown = 0.0;
                player.HitFlash = 0.0;
                player.Controls.Clear();
            }

            for (int i = 0; i < Players.Count; i++)
            {
                Respawn(Players[i]);
            }
        }

        public void Update(double dt)
        {
            Time += dt;

            if (Winner != null)
            {
                return;   // round is over, wait for a restart
            }

            for (int i = 0; i < Players.Count; i++)
            {
                UpdatePlayer(Players[i], dt);
            }

            UpdateShots(dt);
        }

        private void UpdatePlayer(Player player, double dt)
        {
            if (player.Brain != null)
            {
                player.Brain.Think(this, player, dt);
            }

            Controls controls = player.Controls;

            double turnSpeed = TurnSpeed;
            if (player.IsBot)
            {
                turnSpeed = BotTurnSpeed;
            }

            if (controls.TurnLeft)
            {
                player.Angle -= turnSpeed * dt;
            }
            if (controls.TurnRight)
            {
                player.Angle += turnSpeed * dt;
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

            if (controls.Fire && player.FireCooldown <= 0.0)
            {
                Fire(player);
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

        /// <summary>Spawn a shot just outside the player's own collision radius.</summary>
        private void Fire(Player player)
        {
            double dirX = player.DirectionX();
            double dirY = player.DirectionY();
            double startDistance = Player.Radius + Shot.Radius + 0.02;

            Shot shot = new Shot(player, player.X + dirX * startDistance, player.Y + dirY * startDistance, dirX, dirY);
            Shots.Add(shot);

            player.FireCooldown = FireInterval;
            if (player.IsBot)
            {
                player.FireCooldown = FireInterval * 2.0;   // bots shoot slower, humans need a chance
            }
        }

        private void UpdateShots(double dt)
        {
            for (int i = 0; i < Shots.Count; i++)
            {
                MoveShot(Shots[i], dt);
            }

            // Remove dead shots, walking backwards so removal does not skip entries.
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                if (!Shots[i].Alive)
                {
                    Shots.RemoveAt(i);
                }
            }
        }

        /// <summary>Move a shot in small sub-steps so it cannot tunnel through a wall or a player.</summary>
        private void MoveShot(Shot shot, double dt)
        {
            if (!shot.Alive)
            {
                return;
            }

            double distance = Shot.Speed * dt;
            int subSteps = (int)Math.Ceiling(distance / ShotSubStep);
            if (subSteps < 1)
            {
                subSteps = 1;
            }
            double stepLength = distance / subSteps;

            for (int step = 0; step < subSteps; step++)
            {
                shot.X += shot.DirectionX * stepLength;
                shot.Y += shot.DirectionY * stepLength;

                int cellX = MathUtil.FloorToInt(shot.X);
                int cellY = MathUtil.FloorToInt(shot.Y);
                if (Maze.IsWall(cellX, cellY))
                {
                    shot.Alive = false;
                    return;
                }

                Player? victim = FindShotVictim(shot);
                if (victim != null)
                {
                    shot.Alive = false;
                    RegisterHit(shot.Owner, victim);
                    return;
                }
            }
        }

        private Player? FindShotVictim(Shot shot)
        {
            double hitDistance = Player.Radius + Shot.Radius;
            double hitDistanceSquared = hitDistance * hitDistance;

            for (int i = 0; i < Players.Count; i++)
            {
                Player player = Players[i];
                if (player == shot.Owner)
                {
                    continue;
                }
                if (player.Shield > 0.0)
                {
                    continue;
                }

                double dx = player.X - shot.X;
                double dy = player.Y - shot.Y;
                if (dx * dx + dy * dy < hitDistanceSquared)
                {
                    return player;
                }
            }

            return null;
        }

        private void RegisterHit(Player shooter, Player victim)
        {
            shooter.Score++;
            victim.Hits++;
            victim.HitFlash = HitFlashSeconds;
            Respawn(victim);

            if (shooter.Score >= WinScore)
            {
                Winner = shooter;
            }
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

        /// <summary>
        /// True if nothing but floor lies on the straight line between two points.
        /// Used by bots to decide whether they can see (and shoot) somebody.
        /// </summary>
        public bool HasLineOfSight(double fromX, double fromY, double toX, double toY)
        {
            double dx = toX - fromX;
            double dy = toY - fromY;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.001)
            {
                return true;
            }

            int steps = (int)Math.Ceiling(length / 0.1);
            double stepX = dx / steps;
            double stepY = dy / steps;

            double x = fromX;
            double y = fromY;
            for (int i = 0; i < steps; i++)
            {
                x += stepX;
                y += stepY;
                if (Maze.IsWall(MathUtil.FloorToInt(x), MathUtil.FloorToInt(y)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
