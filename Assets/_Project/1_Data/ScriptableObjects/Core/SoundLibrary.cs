using UnityEngine;
using System;

namespace Genesis.Data {

    public enum SoundType {
        // UI  [0-4]
        UI_Click,            // 0
        UI_Open,             // 1
        UI_Close,            // 2
        UI_Error,            // 3
        UI_Success,          // 4

        // Combat  [5-9]
        Combat_Hit,          // 5
        Combat_CriticalHit,  // 6
        Combat_Miss,         // 7
        Combat_Death,        // 8
        Combat_LevelUp,      // 9

        // Loot  [10-12]
        Loot_Pickup,         // 10
        Loot_Gold,           // 11
        Loot_Drop,           // 12

        // Vendor  [13-14]
        Vendor_Buy,          // 13
        Vendor_Sell,         // 14

        // Quests  [15-16]
        Quest_Accept,        // 15
        Quest_Complete,      // 16

        // World  [17-18]
        Portal_Enter,        // 17
        Portal_Exit,         // 18

        // Chest & Loot  [19-20]
        Loot_ChestOpen,      // 19
        Loot_BagOpen,        // 20

        // Equipment  [21-22]
        Equipment_Equip,     // 21
        Equipment_Unequip,   // 22

        // Consumables  [23]
        Consumable_Potion,   // 23

        // Player  [24]
        Player_Respawn,      // 24

        // Zones  [25-26]
        Zone_SafeEnter,      // 25
        Zone_UnsafeEnter,    // 26

        // === NUEVOS — siempre al final para no romper serializacion ===
        Loot_ChestOpen_Epic,     // 27 — Cofre con item Epic o mejor
        Quest_ObjectiveComplete, // 28 — Un objetivo individual se completa
        Quest_TurnIn,            // 29 — Se entrega la mision al NPC
        Trade_Incoming,          // 30 — Llega una solicitud de trade
        Loot_ChestOpening,       // 31 — Sonido durante el casteo de apertura del cofre
    }

    [Serializable]
    public class SoundEntry {
        public SoundType Type;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
        [Range(0.5f, 1.5f)] public float PitchMin = 1f;
        [Range(0.5f, 1.5f)] public float PitchMax = 1f;
        public bool Is3D;
    }

    [CreateAssetMenu(menuName = "Genesis/Audio/Sound Library")]
    public class SoundLibrary : ScriptableObject {

        [Header("UI Sounds")]
        [SerializeField] private SoundEntry[] uiSounds;

        [Header("Combat Sounds")]
        [SerializeField] private SoundEntry[] combatSounds;

        [Header("Footstep Sounds")]
        [SerializeField] private SoundEntry[] footstepSounds;

        [Header("Ambient Sounds")]
        [SerializeField] private SoundEntry[] ambientSounds;

        [Header("Interaction Sounds")]
        [SerializeField] private SoundEntry[] interactionSounds;

        private System.Collections.Generic.Dictionary<SoundType, SoundEntry> _lookup;

        private void BuildLookup() {
            _lookup = new System.Collections.Generic.Dictionary<SoundType, SoundEntry>();
            AddEntries(uiSounds);
            AddEntries(combatSounds);
            AddEntries(footstepSounds);
            AddEntries(ambientSounds);
            AddEntries(interactionSounds);
        }

        private void AddEntries(SoundEntry[] entries) {
            if (entries == null) return;
            foreach (var entry in entries) {
                if (entry.Clip != null && !_lookup.ContainsKey(entry.Type)) {
                    _lookup[entry.Type] = entry;
                }
            }
        }

        public SoundEntry GetEntry(SoundType type) {
            if (_lookup == null) BuildLookup();
            _lookup.TryGetValue(type, out SoundEntry entry);
            return entry;
        }

        public SoundEntry GetEntryByClip(AudioClip clip) {
            if (clip == null) return null;
            
            // Search in categories (Ambient is most common for raw clip triggers)
            if (ambientSounds != null) foreach (var e in ambientSounds) if (e.Clip == clip) return e;
            if (uiSounds != null) foreach (var e in uiSounds) if (e.Clip == clip) return e;
            if (combatSounds != null) foreach (var e in combatSounds) if (e.Clip == clip) return e;
            if (interactionSounds != null) foreach (var e in interactionSounds) if (e.Clip == clip) return e;
            if (footstepSounds != null) foreach (var e in footstepSounds) if (e.Clip == clip) return e;

            return null;
        }

        public SoundEntry GetFootstepEntry() {
            if (footstepSounds == null || footstepSounds.Length == 0) return null;
            return footstepSounds[UnityEngine.Random.Range(0, footstepSounds.Length)];
        }
    }
}
