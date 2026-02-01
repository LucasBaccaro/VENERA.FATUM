namespace Genesis.Items
{
    [System.Flags]
    public enum BodyPartFlags
    {
        None = 0,
        Head = 1 << 0,          // 1
        Shoulders = 1 << 1,     // 2
        Chest = 1 << 2,         // 4
        Arms = 1 << 3,          // 8
        Hands = 1 << 4,         // 16
        Belt = 1 << 5,          // 32
        Pants = 1 << 6,         // 64
        Feet = 1 << 7,          // 128
        Weapon = 1 << 8,        // 256
        OffHand = 1 << 9,       // 512
        All = ~0                // All parts
    }
}
