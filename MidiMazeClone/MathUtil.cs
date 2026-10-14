namespace MidiMazeClone
{
    public static class MathUtil
    {
        public const double TwoPi = Math.PI * 2.0;
        public const double HalfPi = Math.PI / 2.0;

        /// <summary>Wrap an angle into the range (-PI, PI].</summary>
        public static double NormalizeAngle(double angle)
        {
            while (angle <= -Math.PI)
            {
                angle += TwoPi;
            }
            while (angle > Math.PI)
            {
                angle -= TwoPi;
            }
            return angle;
        }

        public static int FloorToInt(double value)
        {
            return (int)Math.Floor(value);
        }

        public static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }
}
