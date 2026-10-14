namespace MidiMazeClone
{
    /// <summary>
    /// Things the simulation reports to the presentation layer (sounds, effects).
    /// The World itself stays free of audio and graphics, so it runs headless and deterministic.
    /// </summary>
    public interface IWorldEvents
    {
        void OnShotFired(Player shooter);
        void OnPlayerHit(Player shooter, Player victim);
    }
}
