namespace KoG.MiniMvp.Troops
{
    public enum TroopFaction : byte
    {
        Player = 0,
        Enemy = 1
    }

    public enum TroopAnimState : byte
    {
        Idle = 0,
        Move = 1,
        Attack = 2,
        Death = 3
    }
}
