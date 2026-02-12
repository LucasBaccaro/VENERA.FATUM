Plan: Sistema de Quests — Tutorial Niveles 1-5                                                                                                                                                               │
│                                                                                                                                                                                                              │
│ Contexto                                                                                                                                                                                                     │
│                                                                                                                                                                                                              │
│ No existe sistema de quests, NPCs ni enemigos en el proyecto. El GDD define un tutorial de ~15 min (niveles 1-5) que enseña las mecánicas core: movimiento, inventario, stats, sub-stats, combate y rareza   │
│ de items. Necesitamos construir la infraestructura completa: data, simulación, UI y contenido.                                                                                                               │
│                                                                                                                                                                                                              │
│ Decisiones del usuario:                                                                                                                                                                                      │
│ - Enemigos con IA básica (detectar, acercarse, atacar)                                                                                                                                                       │
│ - Los 5 puntos de atributo por nivel vienen del level-up natural, no como bonus extra de quests                                                                                                              │
│                                                                                                                                                                                                              │
│ Sistemas Existentes que Reutilizamos                                                                                                                                                                         │
│ ┌──────────────────┬──────────────────────────────────────────────────┬─────────────────────────────────────────────┐                                                                                        │
│ │     Sistema      │                     Archivo                      │                     Uso                     │                                                                                        │
│ ├──────────────────┼──────────────────────────────────────────────────┼─────────────────────────────────────────────┤                                                                                        │
│ │ EventBus         │ 0_Core/Architecture/EventBus.cs                  │ Comunicación entre quest/UI/XP              │                                                                                        │
│ ├──────────────────┼──────────────────────────────────────────────────┼─────────────────────────────────────────────┤                                                                                        │
│ │ XPRewardSystem   │ 2_Simulation/World/XPRewardSystem.cs             │ Ya escucha OnEnemyKilled y OnQuestCompleted │                                                                                        │
│ ├──────────────────┼──────────────────────────────────────────────────┼─────────────────────────────────────────────┤                                                                                        │
│ │ IInteractable    │ 2_Simulation/Entities/Shared/IInteractable.cs    │ NPCs implementan esta interfaz              │                                                                                        │
│ ├──────────────────┼──────────────────────────────────────────────────┼─────────────────────────────────────────────┤                                                                                        │
│ │ IDamageable      │ 2_Simulation/Combat/Interfaces/IDamageable.cs    │ Enemigos implementan esta interfaz          │                                                                                        │
│ ├──────────────────┼──────────────────────────────────────────────────┼─────────────────────────────────────────────┤                                                                                        │
│ │ PlayerInventory  │ 2_Simulation/Items/PlayerInventory.cs            │ AddItem(id, qty, tier, rarity) para rewards │                                                                                        │
│ ├──────────────────┼──────────────────────────────────────────────────┼─────────────────────────────────────────────┤                                                                                        │
│ │ PlayerAttributes │ 2_Simulation/Entities/Player/PlayerAttributes.cs │ GainXP(), nivel, puntos                     │                                                                                        │
│ ├──────────────────┼──────────────────────────────────────────────────┼─────────────────────────────────────────────┤                                                                                        │
│ │ ItemDatabase     │ 1_Data/Databases/ItemDatabase.cs                 │ Patrón singleton para QuestDatabase         │                                                                                        │
│ ├──────────────────┼──────────────────────────────────────────────────┼─────────────────────────────────────────────┤                                                                                        │
│ │ HUDController    │ 3_Presentation/UI/Controllers/HUDController.cs   │ ShowNotification() para avisos              │                                                                                        │
│ ├──────────────────┼──────────────────────────────────────────────────┼─────────────────────────────────────────────┤                                                                                        │
│ │ Ability Logic    │ Todas en 2_Simulation/Combat/Abilities/Logic/    │ Fallback IDamageable.TakeDamage()           │                                                                                        │
│ └──────────────────┴──────────────────────────────────────────────────┴─────────────────────────────────────────────┘                                                                                        │
│ Patrón clave de abilities: Todas las lógicas (Targeted, AOE, Cone, Channel, Dash, Projectile) hacen: 1) try PlayerStats → CombatCalculator, 2) fallback IDamageable.TakeDamage(baseDamage). EnemyMob solo    │
│ necesita IDamageable y funciona con TODAS las abilities existentes.                                                                                                                                          │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Archivos Nuevos (17)                                                                                                                                                                                         │
│                                                                                                                                                                                                              │
│ Capa 1: Data (5 archivos)                                                                                                                                                                                    │
│                                                                                                                                                                                                              │
│ 1. Assets/_Project/1_Data/ScriptableObjects/Quests/QuestEnums.cs                                                                                                                                             │
│ - enum QuestState { NotStarted, Active, ObjectivesComplete, Completed }                                                                                                                                      │
│ - enum QuestObjectiveType { TalkToNPC, Kill, Collect }                                                                                                                                                       │
│ - [Serializable] struct QuestObjective { Type, Description, TargetID (string), RequiredCount }                                                                                                               │
│ - [Serializable] struct QuestReward { ItemID, ItemQuantity, Tier, Rarity, XPType (string) }                                                                                                                  │
│                                                                                                                                                                                                              │
│ 2. Assets/_Project/1_Data/ScriptableObjects/Quests/QuestData.cs                                                                                                                                              │
│ - [CreateAssetMenu] ScriptableObject                                                                                                                                                                         │
│ - QuestID (string), QuestName, RequiredLevel                                                                                                                                                                 │
│ - PrerequisiteQuest (QuestData ref → cadena implícita)                                                                                                                                                       │
│ - GiverNpcID, TurnInNpcID (strings que matchean NpcData.NpcID)                                                                                                                                               │
│ - DialogueOffer, DialogueInProgress, DialogueComplete, DialogueAfterComplete                                                                                                                                 │
│ - QuestObjective[] Objectives, QuestReward[] Rewards                                                                                                                                                         │
│                                                                                                                                                                                                              │
│ 3. Assets/_Project/1_Data/ScriptableObjects/Quests/NpcData.cs                                                                                                                                                │
│ - NpcID, DisplayName, IdleDialogue                                                                                                                                                                           │
│                                                                                                                                                                                                              │
│ 4. Assets/_Project/1_Data/ScriptableObjects/Enemies/EnemyData.cs                                                                                                                                             │
│ - EnemyTag (string), DisplayName, MaxHealth                                                                                                                                                                  │
│ - Damage, AttackRange (2f), AttackCooldown (2f)                                                                                                                                                              │
│ - DetectionRange (8f), MoveSpeed (3f)                                                                                                                                                                        │
│                                                                                                                                                                                                              │
│ 5. Assets/_Project/1_Data/Databases/QuestDatabase.cs                                                                                                                                                         │
│ - Singleton ScriptableObject (mismo patrón que ItemDatabase)                                                                                                                                                 │
│ - Dictionary por QuestID                                                                                                                                                                                     │
│ - GetQuest(id), GetQuestsForNpc(npcId)                                                                                                                                                                       │
│                                                                                                                                                                                                              │
│ Capa 2: Simulación (7 archivos)                                                                                                                                                                              │
│                                                                                                                                                                                                              │
│ 6. Assets/_Project/2_Simulation/Quests/PlayerQuestManager.cs — CORE                                                                                                                                          │
│ - NetworkBehaviour en prefab del jugador                                                                                                                                                                     │
│ - SyncList<QuestProgress> _questLog (activas)                                                                                                                                                                │
│ - SyncList<string> _completedQuests (IDs terminadas)                                                                                                                                                         │
│ - QuestProgress struct: QuestID, State, Progress0/1/2 (campos fijos, no array)                                                                                                                               │
│ - Server: AcceptQuest, AdvanceObjective, CompleteQuest, OnEnemyKilled handler                                                                                                                                │
│ - ServerRpc: CmdAcceptQuest, CmdTurnInQuest                                                                                                                                                                  │
│ - TargetRpc: notificaciones al cliente (quest accepted, progress, complete)                                                                                                                                  │
│ - SyncList callbacks → EventBus("OnQuestLogChanged")                                                                                                                                                         │
│                                                                                                                                                                                                              │
│ 7. Assets/_Project/2_Simulation/Quests/NpcController.cs                                                                                                                                                      │
│ - NetworkBehaviour + IInteractable                                                                                                                                                                           │
│ - Interact → TargetRpc → EventBus("OnNpcDialogueOpen", npcId)                                                                                                                                                │
│                                                                                                                                                                                                              │
│ 8. Assets/_Project/2_Simulation/Entities/Enemy/EnemyMob.cs                                                                                                                                                   │
│ - NetworkBehaviour + IDamageable                                                                                                                                                                             │
│ - SyncVar health, isDead                                                                                                                                                                                     │
│ - TakeDamage → Die → EventBus("OnEnemyKilled", killer, nob)                                                                                                                                                  │
│ - IA básica (server-only Update): Idle/Aggro/Attack/Return states                                                                                                                                            │
│ - NavMeshAgent para movimiento (o transform.position manual)                                                                                                                                                 │
│                                                                                                                                                                                                              │
│ 9. Assets/_Project/2_Simulation/Entities/Enemy/EnemySpawner.cs                                                                                                                                               │
│ - Server-only, controla población en zona                                                                                                                                                                    │
│ - Spawn inicial + respawn con delay                                                                                                                                                                          │
│                                                                                                                                                                                                              │
│ 10. Assets/_Project/2_Simulation/Quests/QuestItemPickup.cs                                                                                                                                                   │
│ - NetworkBehaviour + IInteractable (para quest "El Hallazgo")                                                                                                                                                │
│ - Interact → AddItem al inventario → Despawn → Respawn con timer                                                                                                                                             │
│                                                                                                                                                                                                              │
│ 11. Assets/_Project/2_Simulation/Entities/Player/InteractionDetector.cs                                                                                                                                      │
│ - NetworkBehaviour en prefab del jugador (IsOwner only)                                                                                                                                                      │
│ - E key → OverlapSphere → encuentra IInteractable más cercano                                                                                                                                                │
│ - Rutea a: ILootSource.CmdTryInteract / NpcController / QuestItemPickup                                                                                                                                      │
│ - Reemplaza la lógica E-key de LootBagController                                                                                                                                                             │
│                                                                                                                                                                                                              │
│ 12. Assets/_Project/2_Simulation/Entities/Enemy/EnemyHealthBar.cs                                                                                                                                            │
│ - World-space Canvas con barra de vida sobre la cabeza                                                                                                                                                       │
│ - Billboard hacia cámara, visible cuando dañado                                                                                                                                                              │
│                                                                                                                                                                                                              │
│ Capa 3: Presentación (5 archivos)                                                                                                                                                                            │
│                                                                                                                                                                                                              │
│ 13. Assets/_Project/3_Presentation/UI/Views/QuestTracker.uxml                                                                                                                                                │
│ - Esquina superior derecha                                                                                                                                                                                   │
│ - QuestTitle + lista de objetivos con checkboxes y progreso                                                                                                                                                  │
│                                                                                                                                                                                                              │
│ 14. Assets/_Project/3_Presentation/UI/Controllers/QuestTrackerController.cs                                                                                                                                  │
│ - Escucha EventBus("OnQuestLogChanged")                                                                                                                                                                      │
│ - Muestra quest activa con progreso de cada objetivo                                                                                                                                                         │
│                                                                                                                                                                                                              │
│ 15. Assets/_Project/3_Presentation/UI/Views/DialoguePanel.uxml                                                                                                                                               │
│ - Panel centrado modal                                                                                                                                                                                       │
│ - NpcName, DialogueText, RewardsContainer                                                                                                                                                                    │
│ - AcceptButton / CompleteButton / CloseButton                                                                                                                                                                │
│                                                                                                                                                                                                              │
│ 16. Assets/_Project/3_Presentation/UI/Controllers/DialoguePanelController.cs                                                                                                                                 │
│ - Escucha EventBus("OnNpcDialogueOpen", npcId)                                                                                                                                                               │
│ - Determina estado: quest para entregar / para aceptar / en progreso / idle                                                                                                                                  │
│ - Botones llaman CmdAcceptQuest / CmdTurnInQuest                                                                                                                                                             │
│                                                                                                                                                                                                              │
│ 17. Assets/_Project/3_Presentation/UI/Controllers/InteractionPromptController.cs                                                                                                                             │
│ - Label flotante "Press E — Talk to Captain"                                                                                                                                                                 │
│ - OverlapSphere cada frame para mostrar/ocultar prompt                                                                                                                                                       │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Archivos Modificados (3)                                                                                                                                                                                     │
│                                                                                                                                                                                                              │
│ 1. PlayerAttributes.cs — Agregar [Server] GrantBonusPoints(int) (4 líneas)                                                                                                                                   │
│                                                                                                                                                                                                              │
│ 2. StarterItemGranter.cs — Remover loop de _starterEquipmentIDs (el T0 set viene de quest "El Fuerte")                                                                                                       │
│                                                                                                                                                                                                              │
│ 3. LootBagController.cs — Remover E-key en Update() y TryOpenNearestLootSource() (se mueve a InteractionDetector)                                                                                            │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Las 6 Quests del Tutorial                                                                                                                                                                                    │
│ ┌─────┬─────────────┬─────────────┬─────────┬─────────┬────────────────────────────┬────────────────────┬────────────────┐                                                                                   │
│ │  #  │   QuestID   │   Nombre    │  Giver  │ TurnIn  │          Objetivo          │  Rewards (items)   │       XP       │                                                                                   │
│ ├─────┼─────────────┼─────────────┼─────────┼─────────┼────────────────────────────┼────────────────────┼────────────────┤                                                                                   │
│ │ 1   │ tutorial_01 │ El Arribo   │ captain │ general │ TalkToNPC("general")       │ —                  │ simple (200)   │                                                                                   │
│ ├─────┼─────────────┼─────────────┼─────────┼─────────┼────────────────────────────┼────────────────────┼────────────────┤                                                                                   │
│ │ 2   │ tutorial_02 │ El Fuerte   │ general │ general │ TalkToNPC("general")       │ 8x T0 Common equip │ standard (500) │                                                                                   │
│ ├─────┼─────────────┼─────────────┼─────────┼─────────┼────────────────────────────┼────────────────────┼────────────────┤                                                                                   │
│ │ 3   │ tutorial_03 │ Bautismo    │ general │ general │ Kill("coastal_pest", 5)    │ —                  │ standard (500) │                                                                                   │
│ ├─────┼─────────────┼─────────────┼─────────┼─────────┼────────────────────────────┼────────────────────┼────────────────┤                                                                                   │
│ │ 4   │ tutorial_04 │ El Hallazgo │ general │ general │ Collect("relic", 1)        │ Ring T0 Uncommon   │ standard (500) │                                                                                   │
│ ├─────┼─────────────┼─────────────┼─────────┼─────────┼────────────────────────────┼────────────────────┼────────────────┤                                                                                   │
│ │ 5   │ tutorial_05 │ El Faro     │ general │ general │ Kill("path_enemy", 8)      │ Gloves+Boots T0    │ standard (500) │                                                                                   │
│ ├─────┼─────────────┼─────────────┼─────────┼─────────┼────────────────────────────┼────────────────────┼────────────────┤                                                                                   │
│ │ 6   │ tutorial_06 │ El Jefe     │ general │ general │ Kill("renegade_leader", 1) │ Weapon T1 Uncommon │ epic (1000)    │                                                                                   │
│ └─────┴─────────────┴─────────────┴─────────┴─────────┴────────────────────────────┴────────────────────┴────────────────┘                                                                                   │
│ Nota sobre quest 1 y 2: Quest 1 (El Arribo) se acepta del Captain en el puerto. El objetivo es hablar con el General en el fuerte. Al llegar al General y entregar quest 1, el General ofrece quest 2 (El    │
│ Fuerte) automáticamente — que es una quest de "hablar" inmediata que da el set T0 completo.                                                                                                                  │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Orden de Implementación                                                                                                                                                                                      │
│                                                                                                                                                                                                              │
│ Fase 1: Data                                                                                                                                                                                                 │
│                                                                                                                                                                                                              │
│ 1. QuestEnums.cs, QuestData.cs, NpcData.cs, EnemyData.cs, QuestDatabase.cs                                                                                                                                   │
│ → read_console → 0 errores                                                                                                                                                                                   │
│                                                                                                                                                                                                              │
│ Fase 2: Enemigos                                                                                                                                                                                             │
│                                                                                                                                                                                                              │
│ 2. EnemyMob.cs, EnemySpawner.cs, EnemyHealthBar.cs                                                                                                                                                           │
│ → read_console → 0 errores                                                                                                                                                                                   │
│                                                                                                                                                                                                              │
│ Fase 3: Quests Core                                                                                                                                                                                          │
│                                                                                                                                                                                                              │
│ 3. PlayerQuestManager.cs, NpcController.cs, QuestItemPickup.cs, InteractionDetector.cs                                                                                                                       │
│ 4. Modificar: PlayerAttributes, StarterItemGranter, LootBagController                                                                                                                                        │
│ → read_console → 0 errores                                                                                                                                                                                   │
│                                                                                                                                                                                                              │
│ Fase 4: UI                                                                                                                                                                                                   │
│                                                                                                                                                                                                              │
│ 5. QuestTracker (UXML + Controller)                                                                                                                                                                          │
│ 6. DialoguePanel (UXML + Controller)                                                                                                                                                                         │
│ 7. InteractionPromptController                                                                                                                                                                               │
│ → read_console → 0 errores                                                                                                                                                                                   │
│                                                                                                                                                                                                              │
│ Fase 5: Contenido (SOs en Unity Editor)                                                                                                                                                                      │
│                                                                                                                                                                                                              │
│ 8. QuestDatabase, NpcData SOs, EnemyData SOs, QuestData SOs                                                                                                                                                  │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Verificación                                                                                                                                                                                                 │
│                                                                                                                                                                                                              │
│ 1. read_console → 0 errores tras cada fase                                                                                                                                                                   │
│ 2. NPC → acercarse → prompt → E → diálogo → aceptar quest                                                                                                                                                    │
│ 3. Quest tracker aparece en HUD con objetivos                                                                                                                                                                │
│ 4. Matar enemigos → progreso se actualiza                                                                                                                                                                    │
│ 5. Volver a NPC → entregar → rewards en inventario                                                                                                                                                           │
│ 6. Cadena 6 quests → jugador sube de nivel 1 a 5                                                                                                                                                             │
│ 7. Enemigos: detectan, se acercan, atacan, mueren                                                                                                                                                            │
│                                                                                                                                                                                                              │
│ Limitación Conocida                                                                                                                                                                                          │
│                                                                                                                                                                                                              │
│ Enemigos usan ruta IDamageable (daño base, sin crit/overpower/evasion). CombatCalculator full requiere componente tipo PlayerStats en el enemy — es enhancement futuro. 



⏺ All 5 phases complete with 0 compilation errors. Let me do a final summary of everything that was implemented.                                                                                                
                                                                                                                                                                                                                
  Implementation Complete                                                                                                                                                                                       
                                                                                                                                                                                                                
  Files Created (17)                                                                                                                                                                                            
                                                                                                                                                                                                                
  Data Layer (5):                                                                                                                                                                                               
  1. 1_Data/ScriptableObjects/Quests/QuestEnums.cs — QuestState, QuestObjectiveType, QuestObjective, QuestReward                                                                                                
  2. 1_Data/ScriptableObjects/Quests/QuestData.cs — ScriptableObject for quest definitions                                                                                                                      
  3. 1_Data/ScriptableObjects/Quests/NpcData.cs — ScriptableObject for NPC identity                                                                                                                             
  4. 1_Data/ScriptableObjects/Enemies/EnemyData.cs — ScriptableObject for enemy stats/AI config                                                                                                                 
  5. 1_Data/Databases/QuestDatabase.cs — Singleton database (same pattern as ItemDatabase)

  Enemies (3):
  6. 2_Simulation/Entities/Enemy/EnemyMob.cs — NetworkBehaviour + IDamageable, AI states (Idle/Aggro/Attack/Return), NavMeshAgent support, leash range, death events
  7. 2_Simulation/Entities/Enemy/EnemySpawner.cs — Server-only spawner with configurable population, respawn delay
  8. 2_Simulation/Entities/Enemy/EnemyHealthBar.cs — World-space billboard health bar, shows on damage

  Quest Core (4):
  9. 2_Simulation/Quests/PlayerQuestManager.cs — SyncList quest log, accept/advance/complete flow, kill/collect/talk handlers, EventBus integration with XPRewardSystem
  10. 2_Simulation/Quests/NpcController.cs — IInteractable NPC, talk objective completion, dialogue TargetRpc
  11. 2_Simulation/Quests/QuestItemPickup.cs — IInteractable collectible, respawning, quest notification
  12. 2_Simulation/Entities/Player/InteractionDetector.cs — OverlapSphere E-key system, skips ILootSource (handled by LootBagController)

  UI (5):
  13. 3_Presentation/UI/Views/QuestTracker.uxml — Top-right quest objectives panel
  14. 3_Presentation/UI/Controllers/QuestTrackerController.cs — Listens OnQuestLogChanged, renders objectives with checkmarks
  15. 3_Presentation/UI/Views/DialoguePanel.uxml — Modal NPC dialogue with accept/complete/close buttons
  16. 3_Presentation/UI/Controllers/DialoguePanelController.cs — Prioritizes turn-in > offer > in-progress > idle dialogue
  17. 3_Presentation/UI/Controllers/InteractionPromptController.cs — "[E] Talk to NPC" floating prompt

  Files Modified (3)

  1. PlayerAttributes.cs — Added GrantBonusPoints(int) server method
  2. StarterItemGranter.cs — Removed equipment loop (T0 set now comes from quest "El Fuerte")
  3. LootBagController.cs — Removed E-key Update() and TryOpenNearestLootSource() (moved to InteractionDetector)

  ScriptableObject Assets Created

  - 2 NPC SOs (Captain Aldric, General Voss)
  - 3 Enemy SOs (Coastal Pest, Path Marauder, Renegade Leader)
  - 6 Quest SOs (tutorial_01 through tutorial_06) with full prerequisite chain
  - 1 QuestDatabase SO (in Resources/Databases, populated with all 6 quests)

  Next Steps (Manual in Unity Editor)

  - Add PlayerQuestManager and InteractionDetector components to the player prefab
  - Create NPC GameObjects with NpcController + NpcData SO assigned
  - Create enemy prefabs with EnemyMob + EnemyData SO + NavMeshAgent + Collider
  - Place EnemySpawner GameObjects in scene zones
  - Create QuestItemPickup GameObject for the relic
  - Add UI GameObjects with QuestTrackerController, DialoguePanelController, InteractionPromptController + their UIDocuments
  - Assign quest reward item IDs (2009-2012) in the ItemDatabase once those items exist