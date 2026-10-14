namespace MidiMazeClone
{
    /// <summary>
    /// A simple computer opponent. It only fills in the player's Controls, exactly like
    /// a keyboard would, so bots and humans go through the same movement and firing code.
    ///
    /// Behaviour: wander the maze from cell centre to cell centre, picking an open
    /// neighbour cell at every centre (straight on preferred, turning back last).
    /// When another player is in view and in line of sight, turn towards them, close in
    /// a bit, and fire once the aim is good enough.
    /// </summary>
    public sealed class BotBrain
    {
        public const double SightRange = 10.0;          // cells
        public const double SightHalfAngle = 1.2;       // radians (about 70 degrees to each side)
        public const double AimTolerance = 0.08;        // radians: fire when the target is within this
        public const double TurnTolerance = 0.03;       // radians: stop turning when this close
        public const double KeepDistance = 3.0;         // cells: advance on the target while further away
        public const double ArriveTolerance = 0.04;     // cells: counts as "at the cell centre"
        public const double StraightOnChance = 0.7;     // keep direction at a junction with this probability
        public const double StuckSeconds = 0.5;         // no progress for this long: re-align
        public const double AimJitter = 0.12;           // radians: largest random aim error (about 7 degrees)
        public const double AimJitterSeconds = 0.6;     // how long one aim error is kept before re-rolling

        private enum WanderState
        {
            Aligning,   // walk to the centre of the current cell (after a chase or a spawn)
            Choosing,   // at a cell centre: pick the next cell
            Turning,    // turn on the spot towards the chosen direction
            Moving,     // walk to the centre of the chosen cell
        }

        private WanderState _state = WanderState.Aligning;
        private double _targetAngle;
        private double _targetX;
        private double _targetY;
        private double _lastRemaining;
        private double _stuckTimer;
        private double _aimError;
        private double _aimErrorTimer;

        public void Think(World world, Player me, double dt)
        {
            Controls controls = me.Controls;
            controls.Clear();

            Player? target = FindVisibleEnemy(world, me);
            if (target != null)
            {
                Engage(world, me, target, dt);
                _state = WanderState.Aligning;   // after the fight, get back on the grid
                return;
            }

            Wander(world, me, dt);
        }

        /// <summary>The nearest other player that is in range, in front of us, and not behind a wall.</summary>
        private static Player? FindVisibleEnemy(World world, Player me)
        {
            Player? best = null;
            double bestDistanceSquared = SightRange * SightRange;

            for (int i = 0; i < world.Players.Count; i++)
            {
                Player other = world.Players[i];
                if (other == me)
                {
                    continue;
                }
                if (other.Shield > 0.0)
                {
                    continue;   // cannot be hit anyway
                }

                double dx = other.X - me.X;
                double dy = other.Y - me.Y;
                double distanceSquared = dx * dx + dy * dy;
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                double angleToOther = Math.Atan2(dy, dx);
                double angleDifference = MathUtil.NormalizeAngle(angleToOther - me.Angle);
                if (Math.Abs(angleDifference) > SightHalfAngle)
                {
                    continue;   // not in our field of view
                }

                if (!world.HasLineOfSight(me.X, me.Y, other.X, other.Y))
                {
                    continue;
                }

                best = other;
                bestDistanceSquared = distanceSquared;
            }

            return best;
        }

        private void Engage(World world, Player me, Player target, double dt)
        {
            Controls controls = me.Controls;

            // Bots are not perfect shots: aim a little off, and change that error now and then.
            // A fixed error over a few tenths of a second means the bot really turns to the wrong
            // spot; a new error every tick would just average out to perfect aim.
            _aimErrorTimer -= dt;
            if (_aimErrorTimer <= 0.0)
            {
                _aimError = (world.Random.NextDouble() * 2.0 - 1.0) * AimJitter;
                _aimErrorTimer = AimJitterSeconds;
            }

            double dx = target.X - me.X;
            double dy = target.Y - me.Y;
            double angleToTarget = Math.Atan2(dy, dx) + _aimError;
            double angleDifference = MathUtil.NormalizeAngle(angleToTarget - me.Angle);

            TurnTowards(controls, angleDifference);

            if (Math.Abs(angleDifference) < AimTolerance)
            {
                controls.Fire = true;
            }

            double distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > KeepDistance * KeepDistance && Math.Abs(angleDifference) < 0.5)
            {
                controls.Forward = true;
            }
        }

        private static void TurnTowards(Controls controls, double angleDifference)
        {
            if (angleDifference > TurnTolerance)
            {
                controls.TurnRight = true;
            }
            else if (angleDifference < -TurnTolerance)
            {
                controls.TurnLeft = true;
            }
        }

        private void Wander(World world, Player me, double dt)
        {
            Controls controls = me.Controls;

            if (_state == WanderState.Aligning)
            {
                double centreX = MathUtil.FloorToInt(me.X) + 0.5;
                double centreY = MathUtil.FloorToInt(me.Y) + 0.5;
                double dx = centreX - me.X;
                double dy = centreY - me.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < ArriveTolerance * 2.0)
                {
                    _state = WanderState.Choosing;
                    return;
                }

                double angleToCentre = Math.Atan2(dy, dx);
                double angleDifference = MathUtil.NormalizeAngle(angleToCentre - me.Angle);
                TurnTowards(controls, angleDifference);
                if (Math.Abs(angleDifference) < 0.3)
                {
                    controls.Forward = true;
                }
                return;
            }

            if (_state == WanderState.Choosing)
            {
                ChooseNextCell(world, me);
                return;
            }

            if (_state == WanderState.Turning)
            {
                double angleDifference = MathUtil.NormalizeAngle(_targetAngle - me.Angle);
                if (Math.Abs(angleDifference) < TurnTolerance)
                {
                    _state = WanderState.Moving;
                    _lastRemaining = 1e9;
                    _stuckTimer = 0.0;
                    return;
                }
                TurnTowards(controls, angleDifference);
                return;
            }

            // Moving: walk along the chosen axis until the target cell centre is reached.
            double dirX = Math.Cos(_targetAngle);
            double dirY = Math.Sin(_targetAngle);
            double remaining = (_targetX - me.X) * dirX + (_targetY - me.Y) * dirY;

            if (remaining <= ArriveTolerance)
            {
                _state = WanderState.Choosing;
                return;
            }

            // Progress watchdog: pushed off course by a fight or another player? Re-align.
            if (remaining > _lastRemaining - 0.001)
            {
                _stuckTimer += dt;
                if (_stuckTimer > StuckSeconds)
                {
                    _state = WanderState.Aligning;
                    return;
                }
            }
            else
            {
                _stuckTimer = 0.0;
            }
            _lastRemaining = remaining;

            // Keep the heading corrected while walking, so small errors do not add up.
            double headingError = MathUtil.NormalizeAngle(_targetAngle - me.Angle);
            TurnTowards(controls, headingError);
            controls.Forward = true;
        }

        /// <summary>
        /// At a cell centre: look at the four neighbour cells, keep going straight most of the
        /// time, otherwise pick a random open side. Turning back only when nothing else is open.
        /// </summary>
        private void ChooseNextCell(World world, Player me)
        {
            int cellX = MathUtil.FloorToInt(me.X);
            int cellY = MathUtil.FloorToInt(me.Y);

            double currentAngle = MathUtil.NormalizeAngle(Math.Round(me.Angle / MathUtil.HalfPi) * MathUtil.HalfPi);
            double reverseAngle = MathUtil.NormalizeAngle(currentAngle + Math.PI);

            List<double> openAngles = new List<double>();
            bool straightIsOpen = false;

            for (int k = 0; k < 4; k++)
            {
                double angle = MathUtil.NormalizeAngle(k * MathUtil.HalfPi);
                int neighbourX = cellX + (int)Math.Round(Math.Cos(angle));
                int neighbourY = cellY + (int)Math.Round(Math.Sin(angle));
                if (world.Maze.IsWall(neighbourX, neighbourY))
                {
                    continue;
                }

                openAngles.Add(angle);
                if (SameAngle(angle, currentAngle))
                {
                    straightIsOpen = true;
                }
            }

            if (openAngles.Count == 0)
            {
                _state = WanderState.Aligning;   // boxed in; should not happen in a connected maze
                return;
            }

            double chosen;
            if (straightIsOpen && world.Random.NextDouble() < StraightOnChance)
            {
                chosen = currentAngle;
            }
            else
            {
                List<double> candidates = new List<double>();
                for (int i = 0; i < openAngles.Count; i++)
                {
                    if (!SameAngle(openAngles[i], reverseAngle))
                    {
                        candidates.Add(openAngles[i]);
                    }
                }
                if (candidates.Count == 0)
                {
                    candidates = openAngles;
                }
                chosen = candidates[world.Random.Next(candidates.Count)];
            }

            _targetAngle = chosen;
            _targetX = cellX + (int)Math.Round(Math.Cos(chosen)) + 0.5;
            _targetY = cellY + (int)Math.Round(Math.Sin(chosen)) + 0.5;
            _state = WanderState.Turning;
        }

        private static bool SameAngle(double a, double b)
        {
            return Math.Abs(MathUtil.NormalizeAngle(a - b)) < 0.01;
        }
    }
}
