namespace KoG.MiniMvp.App
{
    /// <summary>
    /// Represents all distinct gameplay interaction states to prevent conflicts.
    /// </summary>
    public enum GameState
    {
        Loading,
        Idle,
        Selecting,
        Building,
        Relocating,
        Raiding,
        Busy
    }
}
