namespace Genesis.Core.Persistence
{
    [System.Serializable]
    public class CharacterData
    {
        // Identity
        public string playerName;
        public int classIndex;

        // Level & XP
        public int level = 1;
        public float currentXP;
        public int unspentPoints;
        public int gold;

        // Base Attributes (allocated points only)
        public int strength;
        public int agility;
        public int intelligence;
        public int wisdom;
        public int constitution;

        // Current Stats
        public float currentHealth;
        public float currentMana;

        // Position
        public float posX, posY, posZ;
        public float rotY;

        // Equipment (11 slots)
        public SerializedItemSlot[] equipment;

        // Inventory (25 slots)
        public SerializedItemSlot[] inventory;

        // Quests
        public SerializedQuestProgress[] activeQuests;
        public string[] completedQuests;

        // Metadata
        public long lastSaveTimestamp;
    }

    [System.Serializable]
    public struct SerializedItemSlot
    {
        public int itemId;
        public int quantity;
        public int tier;   // cast from ItemTier enum
        public int rarity; // cast from ItemRarity enum
    }

    [System.Serializable]
    public struct SerializedQuestProgress
    {
        public string questId;
        public int state; // cast from QuestState enum
        public int progress0, progress1, progress2;
    }
}
