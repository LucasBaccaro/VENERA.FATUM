using FishNet.Object;
using Genesis.Core.Persistence;
using Genesis.Items;
using Genesis.Data;
using UnityEngine;

namespace Genesis.Simulation.Persistence
{
    /// <summary>
    /// Implements IPersistenceBridge by reading/writing actual player components.
    /// Lives in Simulation assembly so it can access PlayerAttributes, PlayerStats, etc.
    /// Registered in ServiceLocator by Bootstrap.
    /// </summary>
    public class CharacterPersistenceBridge : IPersistenceBridge
    {
        public CharacterData ExtractPlayerData(NetworkObject playerObj)
        {
            if (playerObj == null)
            {
                Debug.LogError("[PersistenceBridge] ExtractPlayerData: playerObj is NULL!");
                return null;
            }

            var classManager = playerObj.GetComponent<PlayerClassManager>();
            var attributes = playerObj.GetComponent<PlayerAttributes>();
            var stats = playerObj.GetComponent<PlayerStats>();
            var equipment = playerObj.GetComponent<EquipmentManager>();
            var inventory = playerObj.GetComponent<PlayerInventory>();
            var quests = playerObj.GetComponent<PlayerQuestManager>();
            var transform = playerObj.transform;

            Debug.Log($"[PersistenceBridge] ExtractPlayerData: ClassMgr={classManager != null} Attr={attributes != null} Stats={stats != null} Equip={equipment != null} Inv={inventory != null} Quests={quests != null}");

            var data = new CharacterData();

            // Identity
            data.playerName = classManager != null ? classManager.PlayerName : "";
            data.classIndex = classManager != null ? classManager.CurrentClassIndex : 0;

            // Level & XP
            if (attributes != null)
            {
                data.level = attributes.Level;
                data.currentXP = attributes.CurrentXP;
                data.unspentPoints = attributes.UnspentPoints;
                data.gold = attributes.Gold;
                data.strength = attributes.BaseStrength;
                data.agility = attributes.BaseAgility;
                data.intelligence = attributes.BaseIntelligence;
                data.wisdom = attributes.BaseWisdom;
                data.constitution = attributes.BaseConstitution;
            }

            // Current Stats
            if (stats != null)
            {
                data.currentHealth = stats.CurrentHealth;
                data.currentMana = stats.CurrentMana;
            }

            // Position
            data.posX = transform.position.x;
            data.posY = transform.position.y;
            data.posZ = transform.position.z;
            data.rotY = transform.rotation.eulerAngles.y;

            // Equipment (11 slots)
            data.equipment = new SerializedItemSlot[11];
            if (equipment != null)
            {
                data.equipment[0] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Head));
                data.equipment[1] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Shoulders));
                data.equipment[2] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Chest));
                data.equipment[3] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Pants));
                data.equipment[4] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Feet));
                data.equipment[5] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Hands));
                data.equipment[6] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Belt));
                data.equipment[7] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Weapon));
                data.equipment[8] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.OffHand));
                data.equipment[9] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Ring1));
                data.equipment[10] = SerializeSlot(equipment.GetEquipmentSlot(EquipmentSlot.Ring2));
            }

            // Inventory
            if (inventory != null)
            {
                var slots = inventory.InventorySlots;
                data.inventory = new SerializedItemSlot[slots.Count];
                for (int i = 0; i < slots.Count; i++)
                {
                    data.inventory[i] = SerializeSlot(slots[i]);
                }
            }

            // Quests
            if (quests != null)
            {
                var questLog = quests.QuestLog;
                data.activeQuests = new SerializedQuestProgress[questLog.Count];
                for (int i = 0; i < questLog.Count; i++)
                {
                    var q = questLog[i];
                    data.activeQuests[i] = new SerializedQuestProgress
                    {
                        questId = q.QuestID,
                        state = (int)q.State,
                        progress0 = q.Progress0,
                        progress1 = q.Progress1,
                        progress2 = q.Progress2
                    };
                }

                var completed = quests.CompletedQuests;
                data.completedQuests = new string[completed.Count];
                for (int i = 0; i < completed.Count; i++)
                {
                    data.completedQuests[i] = completed[i];
                }
            }

            Debug.Log($"[PersistenceBridge] Extracted: {data.playerName} Lv{data.level} gold={data.gold} pos=({data.posX:F1},{data.posY:F1},{data.posZ:F1}) equip={data.equipment?.Length ?? 0} inv={data.inventory?.Length ?? 0}");
            return data;
        }

        public void HydratePlayer(NetworkObject playerObj, CharacterData data)
        {
            if (playerObj == null || data == null)
            {
                Debug.LogError($"[PersistenceBridge] HydratePlayer: playerObj={playerObj != null}, data={data != null} — ABORTING");
                return;
            }

            Debug.Log($"[PersistenceBridge] ═══ HYDRATION START ═══ {data.playerName} Lv{data.level}");

            var attributes = playerObj.GetComponent<PlayerAttributes>();
            var stats = playerObj.GetComponent<PlayerStats>();
            var equipment = playerObj.GetComponent<EquipmentManager>();
            var inventory = playerObj.GetComponent<PlayerInventory>();
            var quests = playerObj.GetComponent<PlayerQuestManager>();

            // 1. Attributes (level, XP, gold, base stats)
            if (attributes != null)
            {
                Debug.Log($"[PersistenceBridge] Hydrate 1/6: Attributes → Lv{data.level} XP={data.currentXP} gold={data.gold} STR={data.strength} AGI={data.agility} INT={data.intelligence} WIS={data.wisdom} CON={data.constitution}");
                attributes.HydrateFromSave(
                    data.level, data.currentXP, data.unspentPoints, data.gold,
                    data.strength, data.agility, data.intelligence, data.wisdom, data.constitution);
            }
            else Debug.LogWarning("[PersistenceBridge] Hydrate 1/6: PlayerAttributes is NULL!");

            // 2. Equipment (before stats so RecalculateStats uses correct gear)
            if (equipment != null)
            {
                Debug.Log($"[PersistenceBridge] Hydrate 2/6: Equipment → {data.equipment?.Length ?? 0} slots");
                equipment.HydrateFromSave(data.equipment);
            }
            else Debug.LogWarning("[PersistenceBridge] Hydrate 2/6: EquipmentManager is NULL!");

            // 3. Inventory
            if (inventory != null)
            {
                Debug.Log($"[PersistenceBridge] Hydrate 3/6: Inventory → {data.inventory?.Length ?? 0} slots");
                inventory.HydrateFromSave(data.inventory);
            }
            else Debug.LogWarning("[PersistenceBridge] Hydrate 3/6: PlayerInventory is NULL!");

            // 4. Stats (health/mana after max values are set by equipment recalc)
            if (stats != null)
            {
                Debug.Log($"[PersistenceBridge] Hydrate 4/6: Stats → HP={data.currentHealth} MP={data.currentMana}");
                stats.HydrateFromSave(data.currentHealth, data.currentMana);
            }
            else Debug.LogWarning("[PersistenceBridge] Hydrate 4/6: PlayerStats is NULL!");

            // 5. Quests
            if (quests != null)
            {
                Debug.Log($"[PersistenceBridge] Hydrate 5/6: Quests → {data.activeQuests?.Length ?? 0} active, {data.completedQuests?.Length ?? 0} completed");
                quests.HydrateFromSave(data.activeQuests, data.completedQuests);
            }
            else Debug.LogWarning("[PersistenceBridge] Hydrate 5/6: PlayerQuestManager is NULL!");

            // 6. Position — set on server and notify client via RPC
            Vector3 targetPos = new Vector3(data.posX, data.posY, data.posZ);

            // Server-side: set position directly
            var cc = playerObj.GetComponent<UnityEngine.CharacterController>();
            if (cc != null) cc.enabled = false;

            // Server ground snap (terrain may be loaded on server)
            if (Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 20f))
            {
                targetPos.y = hit.point.y;
            }

            playerObj.transform.position = targetPos;
            playerObj.transform.rotation = Quaternion.Euler(0f, data.rotY, 0f);

            if (cc != null) cc.enabled = true;

            // Client-side: send RPC to teleport owner and restart grounding routine
            // (client-authoritative movement means server position changes don't propagate to owner)
            var motor = playerObj.GetComponent<PlayerMotorMultiplayer>();
            if (motor != null && playerObj.Owner != null)
            {
                motor.RpcHydrateTeleport(playerObj.Owner, targetPos, data.rotY);
                Debug.Log($"[PersistenceBridge] Hydrate 6/6: Position RPC sent → ({targetPos.x:F1}, {targetPos.y:F1}, {targetPos.z:F1}) rotY={data.rotY:F1}");
            }
            else
            {
                Debug.Log($"[PersistenceBridge] Hydrate 6/6: Position (server only) → ({targetPos.x:F1}, {targetPos.y:F1}, {targetPos.z:F1}) rotY={data.rotY:F1}");
            }

            Debug.Log($"[PersistenceBridge] ═══ HYDRATION COMPLETE ═══ {data.playerName} Lv{data.level}");
        }

        public CharacterData CreateNewPlayerData(string playerName, int classIndex, Vector3 spawnPosition)
        {
            return new CharacterData
            {
                playerName = playerName,
                classIndex = classIndex,
                level = 1,
                currentXP = 0f,
                unspentPoints = 0,
                gold = 0,
                strength = 0,
                agility = 0,
                intelligence = 0,
                wisdom = 0,
                constitution = 0,
                currentHealth = 800f,
                currentMana = 800f,
                posX = spawnPosition.x,
                posY = spawnPosition.y,
                posZ = spawnPosition.z,
                equipment = new SerializedItemSlot[11],
                inventory = new SerializedItemSlot[25],
                activeQuests = new SerializedQuestProgress[0],
                completedQuests = new string[0]
            };
        }

        private SerializedItemSlot SerializeSlot(ItemSlot slot)
        {
            return new SerializedItemSlot
            {
                itemId = slot.ItemID,
                quantity = slot.Quantity,
                tier = (int)slot.Tier,
                rarity = (int)slot.Rarity
            };
        }
    }
}
