using UnityEngine;
using System;

namespace Genesis.Data {

    public enum SoundType {
        // UI
        UI_Click,
        UI_Open,
        UI_Close,
        UI_Error,
        UI_Success,

        // Combat
        Combat_Hit,
        Combat_CriticalHit,
        Combat_Miss,
        Combat_Death,
        Combat_LevelUp,

        // Loot
        Loot_Pickup,
        Loot_Gold,
        Loot_Drop,

        // Vendor
        Vendor_Buy,
        Vendor_Sell,

        // Quests
        Quest_Accept,
        Quest_Complete,

        // World
        Portal_Enter,
        Portal_Exit,

        // Chest & Loot
        Loot_ChestOpen,
        Loot_BagOpen,

        // Equipment
        Equipment_Equip,
        Equipment_Unequip,

        // Consumables
        Consumable_Potion,

        // Player
        Player_Respawn,

        // Zones
        Zone_SafeEnter,
        Zone_UnsafeEnter
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
