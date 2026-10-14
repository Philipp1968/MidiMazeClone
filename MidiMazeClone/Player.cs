namespace MidiMazeClone
{
    /// <summary>
    /// A player: human (keyboard or later network) or bot. Plain data, the World moves it.
    /// </summary>
    public sealed class Player
    {
        /// <summary>Collision radius in cells. The corridor is 1.0 wide.</summary>
        public const double Radius = 0.3;

        public readonly string Name;
        public readonly int Color;
        public readonly bool IsBot;
        public readonly Controls Controls = new Controls();

        public double X;
        public double Y;

        /// <summary>Heading in radians. 0 = +X (east on the map), grows clockwise because map Y points down.</summary>
        public double Angle;

        public int Score;              // hits dealt
        public int Hits;               // hits taken
        public double FireCooldown;    // seconds until the next shot is allowed
        public double Shield;          // seconds of invulnerability after a respawn
        public double HitFlash;        // seconds the own view flashes after being hit

        /// <summary>Fills Controls each tick for computer players; null for humans.</summary>
        public BotBrain? Brain;

        public Player(string name, int color, bool isBot)
        {
            Name = name;
            Color = color;
            IsBot = isBot;
        }

        public double DirectionX()
        {
            return Math.Cos(Angle);
        }

        public double DirectionY()
        {
            return Math.Sin(Angle);
        }
    }
}
