using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Core;
using Genesis.Data;

namespace Genesis.Simulation {

    /// <summary>
    /// Primary attributes, leveling, and derived stats system.
    /// Server-authoritative with SyncVars for network synchronization.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerAttributes : NetworkBehaviour {

        [Header("Configuration")]
        [SerializeField] private AttributeConfig _config;

        // ═══════════════════════════════════════════════════════
        // LEVEL & XP (Synced)
        // ═══════════════════════════════════════════════════════

        private readonly SyncVar<int> _level = new SyncVar<int>(1);
        private readonly SyncVar<float> _currentXP = new SyncVar<float>(0f);
        private readonly SyncVar<float> _xpToNextLevel = new SyncVar<float>(100f);
        private readonly SyncVar<int> _unspentPoints = new SyncVar<int>(0);
        private readonly SyncVar<int> _gold = new SyncVar<int>(0);

        // ═══════════════════════════════════════════════════════
        // PRIMARY ATTRIBUTES (Synced)
        // ═══════════════════════════════════════════════════════

        private readonly SyncVar<int> _strength = new SyncVar<int>(0);
        private readonly SyncVar<int> _agility = new SyncVar<int>(0);
        private readonly SyncVar<int> _intelligence = new SyncVar<int>(0);
        private readonly SyncVar<int> _wisdom = new SyncVar<int>(0);
        private readonly SyncVar<int> _constitution = new SyncVar<int>(0);

        // ═══════════════════════════════════════════════════════
        // EQUIPMENT BONUSES (Server-set by EquipmentManager)
        // ═══════════════════════════════════════════════════════

        private int _equipStr, _equipAgi, _equipInt, _equipWis, _equipCon;
        private float _equipHaste, _equipLifeSteal, _equipPenetration, _equipBlock;
        private float _equipLootLuck, _equipLockpicking, _equipPerception, _equipMoveSpeed;
        private float _equipArmor, _equipMagicResistance;

        // ═══════════════════════════════════════════════════════
        // DERIVED STATS (Calculated, Synced for client read)
        // ═══════════════════════════════════════════════════════

        private readonly SyncVar<float> _physicalDamageBonus = new SyncVar<float>(0f);
        private readonly SyncVar<float> _magicDamageBonus = new SyncVar<float>(0f);
        private readonly SyncVar<float> _critChance = new SyncVar<float>(0f);
        private readonly SyncVar<float> _spellCritChance = new SyncVar<float>(0f);
        private readonly SyncVar<float> _healingPowerBonus = new SyncVar<float>(0f);
        private readonly SyncVar<float> _evasionChance = new SyncVar<float>(0f);
        private readonly SyncVar<float> _overpowerChance = new SyncVar<float>(0f);
        private readonly SyncVar<float> _bonusHealth = new SyncVar<float>(0f);
        private readonly SyncVar<float> _bonusManaRegen = new SyncVar<float>(0f);
        private readonly SyncVar<float> _healthRegenOOC = new SyncVar<float>(0f);
        private readonly SyncVar<float> _blockValue = new SyncVar<float>(0f);

        // Sub-stats (synced for UI)
        private readonly SyncVar<float> _haste = new SyncVar<float>(0f);
        private readonly SyncVar<float> _lifeSteal = new SyncVar<float>(0f);
        private readonly SyncVar<float> _penetration = new SyncVar<float>(0f);
        private readonly SyncVar<float> _lootLuck = new SyncVar<float>(0f);
        private readonly SyncVar<float> _lockpicking = new SyncVar<float>(0f);
        private readonly SyncVar<float> _perception = new SyncVar<float>(0f);
        private readonly SyncVar<float> _moveSpeed = new SyncVar<float>(0f);
        private readonly SyncVar<float> _armor = new SyncVar<float>(0f);
        private readonly SyncVar<float> _magicResistance = new SyncVar<float>(0f);

        // ═══════════════════════════════════════════════════════
        // PUBLIC PROPERTIES
        // ═══════════════════════════════════════════════════════

        public int Level => _level.Value;
        public float CurrentXP => _currentXP.Value;
        public float XPToNextLevel => _xpToNextLevel.Value;
        public int UnspentPoints => _unspentPoints.Value;
        public int Gold => _gold.Value;

        public int Strength => _strength.Value + _equipStr;
        public int Agility => _agility.Value + _equipAgi;
        public int Intelligence => _intelligence.Value + _equipInt;
        public int Wisdom => _wisdom.Value + _equipWis;
        public int Constitution => _constitution.Value + _equipCon;

        public int BaseStrength => _strength.Value;
        public int BaseAgility => _agility.Value;
        public int BaseIntelligence => _intelligence.Value;
        public int BaseWisdom => _wisdom.Value;
        public int BaseConstitution => _constitution.Value;

        public float PhysicalDamageBonus => _physicalDamageBonus.Value;
        public float MagicDamageBonus => _magicDamageBonus.Value;
        public float CritChance => _critChance.Value;
        public float SpellCritChance => _spellCritChance.Value;
        public float HealingPowerBonus => _healingPowerBonus.Value;
        public float EvasionChance => _evasionChance.Value;
        public float OverpowerChance => _overpowerChance.Value;
        public float BonusHealth => _bonusHealth.Value;
        public float BonusManaRegen => _bonusManaRegen.Value;
        public float HealthRegenOOC => _healthRegenOOC.Value;
        public float BlockValue => _blockValue.Value;

        public float Haste => _haste.Value;
        public float LifeSteal => _lifeSteal.Value;
        public float Penetration => _penetration.Value;
        public float LootLuck => _lootLuck.Value;
        public float Lockpicking => _lockpicking.Value;
        public float Perception => _perception.Value;
        public float MoveSpeed => _moveSpeed.Value;
        public float Armor => _armor.Value;
        public float MagicResistance => _magicResistance.Value;

        public AttributeConfig Config => _config;

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        public override void OnStartNetwork() {
            base.OnStartNetwork();

            _level.OnChange += OnLevelChanged;
            _currentXP.OnChange += OnXPChanged;
            _unspentPoints.OnChange += OnPointsChanged;
            _strength.OnChange += OnAttributeChanged;
            _agility.OnChange += OnAttributeChanged;
            _intelligence.OnChange += OnAttributeChanged;
            _wisdom.OnChange += OnAttributeChanged;
            _constitution.OnChange += OnAttributeChanged;
            _gold.OnChange += OnGoldChanged;

            // Derived stats changes
            _physicalDamageBonus.OnChange += OnDerivedStatChanged;
            _magicDamageBonus.OnChange += OnDerivedStatChanged;
            _critChance.OnChange += OnDerivedStatChanged;
            _spellCritChance.OnChange += OnDerivedStatChanged;
            _healingPowerBonus.OnChange += OnDerivedStatChanged;
            _evasionChance.OnChange += OnDerivedStatChanged;
            _overpowerChance.OnChange += OnDerivedStatChanged;
            _bonusHealth.OnChange += OnDerivedStatChanged;
        }

        public override void OnStartServer() {
            base.OnStartServer();

            if (_config != null) {
                _xpToNextLevel.Value = _config.GetXPForLevel(_level.Value);
            }
            RecalculateDerivedStats();
        }

        // ═══════════════════════════════════════════════════════
        // XP & LEVELING
        // ═══════════════════════════════════════════════════════

        [Server]
        public void GainXP(float amount) {
            if (_config == null || _level.Value >= _config.MaxLevel) return;

            _currentXP.Value += amount;

            while (_currentXP.Value >= _xpToNextLevel.Value && _level.Value < _config.MaxLevel) {
                _currentXP.Value -= _xpToNextLevel.Value;
                LevelUp();
            }

            // Clamp XP at max level
            if (_level.Value >= _config.MaxLevel) {
                _currentXP.Value = 0f;
            }

            EventBus.Trigger("OnXPChanged", _currentXP.Value, _xpToNextLevel.Value);
        }

        [Server]
        private void LevelUp() {
            _level.Value++;
            _unspentPoints.Value += _config.PointsPerLevel;
            _xpToNextLevel.Value = _config.GetXPForLevel(_level.Value);

            RecalculateDerivedStats();
            NotifyStatsRecalculation();

            RpcOnLevelUp(_level.Value);
            EventBus.Trigger("OnLevelUp", _level.Value);

            Debug.Log($"[PlayerAttributes] {gameObject.name} reached level {_level.Value}! +{_config.PointsPerLevel} points. XP needed: {_xpToNextLevel.Value}");
        }

        [ObserversRpc]
        private void RpcOnLevelUp(int newLevel) {
            if (base.IsOwner) {
                EventBus.Trigger("OnLevelUp", newLevel);
            }
            Debug.Log($"[PlayerAttributes] Level Up! Now level {newLevel}");
        }

        // ═══════════════════════════════════════════════════════
        // ATTRIBUTE ALLOCATION
        // ═══════════════════════════════════════════════════════

        [ServerRpc]
        public void CmdAllocatePoint(int attrIndex) {
            if (_unspentPoints.Value <= 0) {
                Debug.LogWarning("[PlayerAttributes] No unspent points available.");
                return;
            }

            switch (attrIndex) {
                case 0: _strength.Value++; break;
                case 1: _agility.Value++; break;
                case 2: _intelligence.Value++; break;
                case 3: _wisdom.Value++; break;
                case 4: _constitution.Value++; break;
                default:
                    Debug.LogWarning($"[PlayerAttributes] Invalid attribute index: {attrIndex}");
                    return;
            }

            _unspentPoints.Value--;
            RecalculateDerivedStats();
            NotifyStatsRecalculation();

            Debug.Log($"[PlayerAttributes] Allocated point to attr {attrIndex}. Remaining: {_unspentPoints.Value}");
        }

        // ═══════════════════════════════════════════════════════
        // DERIVED STATS CALCULATION
        // ═══════════════════════════════════════════════════════

        [Server]
        public void RecalculateDerivedStats() {
            if (_config == null) {
                Debug.LogWarning("[PlayerAttributes] AttributeConfig is null!");
                return;
            }

            int totalStr = _strength.Value + _equipStr;
            int totalAgi = _agility.Value + _equipAgi;
            int totalInt = _intelligence.Value + _equipInt;
            int totalWis = _wisdom.Value + _equipWis;
            int totalCon = _constitution.Value + _equipCon;

            // STR derived
            _physicalDamageBonus.Value = totalStr * _config.PhysicalDamagePerPoint;
            _blockValue.Value = totalStr * _config.BlockValuePerPoint + _equipBlock;

            // AGI derived
            _critChance.Value = totalAgi * _config.CritChancePerPoint;
            _evasionChance.Value = totalAgi * _config.EvasionPerPoint;

            // INT derived
            _magicDamageBonus.Value = totalInt * _config.MagicDamagePerPoint;
            _spellCritChance.Value = totalInt * _config.SpellCritPerPoint;

            // WIS derived
            _healingPowerBonus.Value = totalWis * _config.HealingPowerPerPoint;
            _bonusManaRegen.Value = totalWis * _config.ManaRegenPerPoint;

            // CON derived
            _bonusHealth.Value = totalCon * _config.HealthPerPoint;
            _healthRegenOOC.Value = totalCon * _config.HPRegenPerPoint;

            // Overpower (STR-based)
            _overpowerChance.Value = _config.OverpowerBaseChance + (totalStr * _config.OverpowerChancePerStr);

            // Equipment sub-stats (pass through)
            _haste.Value = _equipHaste;
            _lifeSteal.Value = _equipLifeSteal;
            _penetration.Value = _equipPenetration;
            _lootLuck.Value = _equipLootLuck;
            _lockpicking.Value = _equipLockpicking;
            _perception.Value = _equipPerception;
            _moveSpeed.Value = _equipMoveSpeed;
            _armor.Value = _equipArmor;
            _magicResistance.Value = _equipMagicResistance;
        }

        /// <summary>
        /// Notifies EquipmentManager to recalculate MaxHP/MaxMana (level/attribute changes).
        /// Separated from RecalculateDerivedStats to avoid circular calls with SetEquipmentBonuses.
        /// </summary>
        [Server]
        private void NotifyStatsRecalculation() {
            var equipmentManager = GetComponent<EquipmentManager>();
            if (equipmentManager != null) {
                equipmentManager.RecalculateStats();
            }
        }

        // ═══════════════════════════════════════════════════════
        // EQUIPMENT BONUSES (Called by EquipmentManager)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Called by EquipmentManager.RecalculateStats() - updates equipment bonuses
        /// and recalculates derived stats. Does NOT call back to EquipmentManager.
        /// </summary>
        [Server]
        public void SetEquipmentBonuses(
            int str, int agi, int intel, int wis, int con,
            float haste, float lifeSteal, float penetration, float block,
            float lootLuck, float lockpicking, float perception, float moveSpeed,
            float armor, float magicResistance) {

            _equipStr = str;
            _equipAgi = agi;
            _equipInt = intel;
            _equipWis = wis;
            _equipCon = con;
            _equipHaste = haste;
            _equipLifeSteal = lifeSteal;
            _equipPenetration = penetration;
            _equipBlock = block;
            _equipLootLuck = lootLuck;
            _equipLockpicking = lockpicking;
            _equipPerception = perception;
            _equipMoveSpeed = moveSpeed;
            _equipArmor = armor;
            _equipMagicResistance = magicResistance;

            // Recalculate derived stats with new equipment bonuses
            RecalculateDerivedStats();
        }

        // ═══════════════════════════════════════════════════════
        // PERSISTENCE HYDRATION
        // ═══════════════════════════════════════════════════════

        [Server]
        public void HydrateFromSave(int level, float xp, int unspentPoints, int gold,
            int str, int agi, int intel, int wis, int con) {
            _level.Value = level;
            _currentXP.Value = xp;
            _xpToNextLevel.Value = _config != null ? _config.GetXPForLevel(level) : 100f;
            _unspentPoints.Value = unspentPoints;
            _gold.Value = gold;
            _strength.Value = str;
            _agility.Value = agi;
            _intelligence.Value = intel;
            _wisdom.Value = wis;
            _constitution.Value = con;
            RecalculateDerivedStats();
        }

        [Server]
        public void GrantBonusPoints(int points) {
            _unspentPoints.Value += points;
            Debug.Log($"[PlayerAttributes] Granted {points} bonus points. Total unspent: {_unspentPoints.Value}");
        }

        [Server]
        public void ResetAttributes() {
            int totalSpent = _strength.Value + _agility.Value + _intelligence.Value + _wisdom.Value + _constitution.Value;
            _strength.Value = 0;
            _agility.Value = 0;
            _intelligence.Value = 0;
            _wisdom.Value = 0;
            _constitution.Value = 0;
            _unspentPoints.Value += totalSpent;

            RecalculateDerivedStats();
            Debug.Log($"[PlayerAttributes] Attributes reset. Returned {totalSpent} points.");
        }

        // ═══════════════════════════════════════════════════════
        // GOLD
        // ═══════════════════════════════════════════════════════

        [Server]
        public void GainGold(int amount) {
            if (amount <= 0) return;
            _gold.Value += amount;
        }

        [Server]
        public bool SpendGold(int amount) {
            if (amount <= 0 || _gold.Value < amount) return false;
            _gold.Value -= amount;
            return true;
        }

        // ═══════════════════════════════════════════════════════
        // SYNCVAR CALLBACKS
        // ═══════════════════════════════════════════════════════

        private void OnLevelChanged(int oldVal, int newVal, bool asServer) {
            if (base.IsOwner) {
                EventBus.Trigger("OnLevelChanged", newVal);
            }
        }

        private void OnXPChanged(float oldVal, float newVal, bool asServer) {
            if (base.IsOwner) {
                EventBus.Trigger("OnXPChanged", newVal, _xpToNextLevel.Value);
            }
        }

        private void OnPointsChanged(int oldVal, int newVal, bool asServer) {
            if (base.IsOwner) {
                EventBus.Trigger("OnUnspentPointsChanged", newVal);
            }
        }

        private void OnAttributeChanged(int oldVal, int newVal, bool asServer) {
            if (base.IsOwner) {
                EventBus.Trigger("OnAttributesChanged");
            }
        }

        private void OnDerivedStatChanged(float oldVal, float newVal, bool asServer) {
            if (base.IsOwner) {
                EventBus.Trigger("OnDerivedStatsChanged");
            }
        }

        private void OnGoldChanged(int oldVal, int newVal, bool asServer) {
            if (base.IsOwner) {
                EventBus.Trigger("OnGoldChanged", newVal);

                int gained = newVal - oldVal;
                if (gained > 0) {
                    var data = new FloatingTextData(
                        transform.position + Vector3.up * 2f,
                        $"+{gained}",
                        "gold"
                    );
                    EventBus.Trigger("OnShowFloatingText", data);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // DEBUG
        // ═══════════════════════════════════════════════════════

#if UNITY_EDITOR
        [ContextMenu("Debug: Grant 500 XP")]
        private void DebugGrantXP() {
            if (base.IsServer) GainXP(500f);
        }

        [ContextMenu("Debug: Grant 5 Points")]
        private void DebugGrantPoints() {
            if (base.IsServer) _unspentPoints.Value += 5;
        }

        [ContextMenu("Debug: Reset Attributes")]
        private void DebugResetAttributes() {
            if (base.IsServer) ResetAttributes();
        }
#endif
    }
}
