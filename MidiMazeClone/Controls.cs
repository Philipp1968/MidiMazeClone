namespace MidiMazeClone
{
    /// <summary>
    /// What a player wants to do this tick. Filled by the keyboard for the local player,
    /// by a bot brain for computer players, later by the network for remote players.
    /// </summary>
    public sealed class Controls
    {
        public bool TurnLeft;
        public bool TurnRight;
        public bool Forward;
        public bool Backward;
        public bool Fire;

        public void Clear()
        {
            TurnLeft = false;
            TurnRight = false;
            Forward = false;
            Backward = false;
            Fire = false;
        }
    }
}
