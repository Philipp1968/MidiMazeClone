namespace MidiMazeClone
{
    /// <summary>A ball flying in a straight line until it hits a wall or a player.</summary>
    public sealed class Shot
    {
        public const double Speed = 7.0;        // cells per second
        public const double Radius = 0.08;      // collision radius in cells
        public const double DrawRadius = 0.10;  // drawn slightly bigger so it stays visible at a distance

        public readonly Player Owner;
        public double X;
        public double Y;
        public double DirectionX;
        public double DirectionY;
        public bool Alive = true;

        public Shot(Player owner, double x, double y, double directionX, double directionY)
        {
            Owner = owner;
            X = x;
            Y = y;
            DirectionX = directionX;
            DirectionY = directionY;
        }
    }
}
