using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Object;
using Genesis.Data;
using Genesis.Simulation;
using Genesis.Simulation.Combat;

namespace Genesis.EditorTools {

    public static class BanditAnimationSetup {

        private const string FBX_PATH = "Assets/_Project/5_Content/Models/Enemies/Bandits/Bandits_Base.fbx";
        private const string ANIM_OUTPUT = "Assets/_Project/5_Content/Animations/Enemies/Bandits/";
        private const string PREFAB_OUTPUT = "Assets/_Project/5_Content/Prefabs/Enemies/Bandits/";
        private const string LOOT_BAG_PATH = "Assets/_Project/5_Content/Prefabs/Items/LootBag.prefab";
        private const string ENEMY_DATA_PATH = "Assets/_Project/1_Data/ScriptableObjects/Enemies/Data/";

        // ═══════════════════════════════════════════════════════
        // 1. DISCOVER
        // ═══════════════════════════════════════════════════════

        [MenuItem("Genesis/Tools/Bandit - 1. Discover Animations")]
        public static void DiscoverAnimations() {
            var importer = AssetImporter.GetAtPath(FBX_PATH) as ModelImporter;
            if (importer == null) {
                Debug.LogError($"[BanditSetup] ModelImporter not found at: {FBX_PATH}");
                return;
            }

            // Log animation takes
            var clips = importer.defaultClipAnimations;
            Debug.Log($"[BanditSetup] Found {clips.Length} default clip animations:");
            foreach (var clip in clips) {
                Debug.Log($"  Take: \"{clip.name}\"  frames: {clip.firstFrame}-{clip.lastFrame}  loop: {clip.loopTime}");
            }

            // Also log clipAnimations (user-configured)
            var userClips = importer.clipAnimations;
            if (userClips != null && userClips.Length > 0) {
                Debug.Log($"[BanditSetup] Found {userClips.Length} user-configured clip animations:");
                foreach (var clip in userClips) {
                    Debug.Log($"  Clip: \"{clip.name}\"  frames: {clip.firstFrame}-{clip.lastFrame}  loop: {clip.loopTime}");
                }
            }

            // Log FBX hierarchy
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
            if (fbxAsset != null) {
                Debug.Log("[BanditSetup] FBX Hierarchy:");
                LogHierarchy(fbxAsset.transform, 0);
            }
        }

        private static void LogHierarchy(Transform t, int depth) {
            string indent = new string(' ', depth * 2);
            Debug.Log($"{indent}{t.name}");
            for (int i = 0; i < t.childCount; i++) {
                LogHierarchy(t.GetChild(i), depth + 1);
            }
        }

        // ═══════════════════════════════════════════════════════
        // 2. SETUP CLIPS
        // ═══════════════════════════════════════════════════════

        [MenuItem("Genesis/Tools/Bandit - 2. Setup Clips")]
        public static void SetupClips() {
            var importer = AssetImporter.GetAtPath(FBX_PATH) as ModelImporter;
            if (importer == null) {
                Debug.LogError($"[BanditSetup] ModelImporter not found at: {FBX_PATH}");
                return;
            }

            // If clips are already configured, only update loopTime — don't recreate (preserves internalIDs)
            var existing = importer.clipAnimations;
            if (existing != null && existing.Length > 0) {
                bool changed = false;
                for (int i = 0; i < existing.Length; i++) {
                    string lower = existing[i].name.ToLower();
                    bool shouldLoop = lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run") || lower.Contains("sitting");
                    if (existing[i].loopTime != shouldLoop) {
                        existing[i].loopTime = shouldLoop;
                        existing[i].wrapMode = shouldLoop ? WrapMode.Loop : WrapMode.Default;
                        changed = true;
                    }
                }
                if (changed) {
                    importer.clipAnimations = existing;
                    importer.SaveAndReimport();
                    Debug.Log("[BanditSetup] Updated loop settings on existing clips (internalIDs preserved).");
                } else {
                    Debug.Log("[BanditSetup] Clips already configured correctly. No changes needed.");
                }
                return;
            }

            // First-time setup: copy defaults into clipAnimations
            var defaults = importer.defaultClipAnimations;
            if (defaults.Length == 0) {
                Debug.LogError("[BanditSetup] No default clip animations found in FBX.");
                return;
            }

            // Clips that should loop
            var loopNames = new HashSet<string>();
            foreach (var clip in defaults) {
                string lower = clip.name.ToLower();
                if (lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run") || lower.Contains("sitting")) {
                    loopNames.Add(clip.name);
                }
            }

            var newClips = new ModelImporterClipAnimation[defaults.Length];
            for (int i = 0; i < defaults.Length; i++) {
                newClips[i] = new ModelImporterClipAnimation {
                    name = defaults[i].name,
                    takeName = defaults[i].takeName,
                    firstFrame = defaults[i].firstFrame,
                    lastFrame = defaults[i].lastFrame,
                    loopTime = loopNames.Contains(defaults[i].name),
                    wrapMode = loopNames.Contains(defaults[i].name) ? WrapMode.Loop : WrapMode.Default
                };
            }

            importer.clipAnimations = newClips;
            importer.SaveAndReimport();

            Debug.Log($"[BanditSetup] Configured {newClips.Length} clips. Looping: {string.Join(", ", loopNames)}");
        }

        // ═══════════════════════════════════════════════════════
        // 3. CREATE ANIMATOR CONTROLLERS
        // ═══════════════════════════════════════════════════════

        [MenuItem("Genesis/Tools/Bandit - 3. Create AnimatorControllers")]
        public static void CreateAnimatorControllers() {
            if (!Directory.Exists(ANIM_OUTPUT)) {
                Directory.CreateDirectory(ANIM_OUTPUT);
                AssetDatabase.Refresh();
            }

            // Load all clips from the FBX (includes Bandit_Base_Rig| prefixed duplicates)
            var allObjects = AssetDatabase.LoadAllAssetsAtPath(FBX_PATH);
            var allClips = new Dictionary<string, AnimationClip>();
            foreach (var obj in allObjects) {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__")) {
                    allClips[clip.name] = clip;
                }
            }

            if (allClips.Count == 0) {
                Debug.LogError("[BanditSetup] No animation clips found in FBX. Run 'Setup Clips' first.");
                return;
            }

            Debug.Log($"[BanditSetup] Found {allClips.Count} clips: {string.Join(", ", allClips.Keys)}");

            // Helper: find clip by name (tries exact, then with Bandit_Base_Rig| prefix)
            AnimationClip FindClip(string name) {
                if (allClips.TryGetValue(name, out var c)) return c;
                if (allClips.TryGetValue("Bandit_Base_Rig|" + name, out var c2)) return c2;
                return null;
            }

            // ── AC_Bandit_Fighter (alternates Attack01 / Attack02) ──
            CreateController("AC_Bandit_Fighter", allClips, new ControllerConfig {
                idle = FindClip("Fighter_Standby_Idle"),
                walk = FindClip("Fighter_Combat_Walk"),
                attack = FindClip("Fighter_Combat_Attack01"),
                attack2 = FindClip("Fighter_Combat_Attack02"),
                death = FindClip("Fighter_Death01")
            });

            // ── AC_Bandit_CutThroat (borrows fighter idle/walk/death) ──
            CreateController("AC_Bandit_CutThroat", allClips, new ControllerConfig {
                idle = FindClip("Fighter_Standby_Idle"),
                walk = FindClip("Fighter_Combat_Walk"),
                attack = FindClip("CutThroat_Combat_Attack01"),
                death = FindClip("Fighter_Death01")
            });

            // ── AC_Bandit_Crossbow ──
            CreateController("AC_Bandit_Crossbow", allClips, new ControllerConfig {
                idle = FindClip("Crossbow_Combat_Idle"),
                walk = FindClip("Crossbow_Combat_Walk"),
                attack = FindClip("Crossbow_Combat_Attack01"),
                death = FindClip("Crossbow_Combat_Death")
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BanditSetup] AnimatorControllers created successfully.");
        }

        private struct ControllerConfig {
            public AnimationClip idle;
            public AnimationClip walk;
            public AnimationClip attack;
            public AnimationClip attack2; // optional second attack
            public AnimationClip death;
        }

        private static void CreateController(string controllerName, Dictionary<string, AnimationClip> allClips, ControllerConfig config) {
            string path = ANIM_OUTPUT + controllerName + ".controller";
            string metaPath = path + ".meta";

            // Preserve GUID by saving .meta before deleting
            string savedMeta = null;
            if (File.Exists(metaPath)) {
                savedMeta = File.ReadAllText(metaPath);
            }

            // Clean delete of existing controller
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) {
                AssetDatabase.DeleteAsset(path);
            }
            // Also delete raw file in case AssetDatabase.DeleteAsset didn't fully clean up
            if (File.Exists(path)) {
                File.Delete(path);
            }

            // Restore .meta to preserve GUID
            if (savedMeta != null) {
                File.WriteAllText(metaPath, savedMeta);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var rootStateMachine = controller.layers[0].stateMachine;

            // Parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Anticipation", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            // Locomotion BlendTree
            var locomotionState = controller.CreateBlendTreeInController("Locomotion", out BlendTree blendTree, 0);
            blendTree.blendParameter = "Speed";
            blendTree.blendType = BlendTreeType.Simple1D;

            if (config.idle != null) blendTree.AddChild(config.idle, 0f);
            if (config.walk != null) blendTree.AddChild(config.walk, 1f);

            rootStateMachine.defaultState = locomotionState;

            // Attack state
            if (config.attack != null) {
                var attackState = rootStateMachine.AddState("Attack");
                attackState.motion = config.attack;

                var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
                anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                anyToAttack.duration = 0.1f;
                anyToAttack.hasExitTime = false;

                var attackToLocomotion = attackState.AddTransition(locomotionState);
                attackToLocomotion.hasExitTime = true;
                attackToLocomotion.exitTime = 0.9f;
                attackToLocomotion.duration = 0.1f;
            }

            // Attack2 state (optional second attack)
            if (config.attack2 != null) {
                var attack2State = rootStateMachine.AddState("Attack2");
                attack2State.motion = config.attack2;

                var anyToAttack2 = rootStateMachine.AddAnyStateTransition(attack2State);
                anyToAttack2.AddCondition(AnimatorConditionMode.If, 0, "Attack2");
                anyToAttack2.duration = 0.1f;
                anyToAttack2.hasExitTime = false;

                var attack2ToLocomotion = attack2State.AddTransition(locomotionState);
                attack2ToLocomotion.hasExitTime = true;
                attack2ToLocomotion.exitTime = 0.9f;
                attack2ToLocomotion.duration = 0.1f;
            }

            // Anticipation state (reuse attack clip as visual cue — telegraphed wind-up)
            {
                var anticipationState = rootStateMachine.AddState("Anticipation");
                // Use attack clip slowed down as anticipation if no dedicated clip
                if (config.attack != null) {
                    anticipationState.motion = config.attack;
                    anticipationState.speed = 0.3f;
                }

                var anyToAnticipation = rootStateMachine.AddAnyStateTransition(anticipationState);
                anyToAnticipation.AddCondition(AnimatorConditionMode.If, 0, "Anticipation");
                anyToAnticipation.duration = 0.1f;
                anyToAnticipation.hasExitTime = false;

                var anticipationToLocomotion = anticipationState.AddTransition(locomotionState);
                anticipationToLocomotion.hasExitTime = true;
                anticipationToLocomotion.exitTime = 0.9f;
                anticipationToLocomotion.duration = 0.1f;
            }

            // Die state (no exit)
            if (config.death != null) {
                var dieState = rootStateMachine.AddState("Die");
                dieState.motion = config.death;

                var anyToDie = rootStateMachine.AddAnyStateTransition(dieState);
                anyToDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
                anyToDie.duration = 0.1f;
                anyToDie.hasExitTime = false;
            }

            EditorUtility.SetDirty(controller);
            Debug.Log($"[BanditSetup] Created {controllerName} — idle={config.idle?.name}, walk={config.walk?.name}, attack={config.attack?.name}, attack2={config.attack2?.name}, death={config.death?.name}");
        }

        // ═══════════════════════════════════════════════════════
        // 4. CREATE PREFABS
        // ═══════════════════════════════════════════════════════

        [MenuItem("Genesis/Tools/Bandit - 4. Create Prefabs")]
        public static void CreatePrefabs() {
            if (!Directory.Exists(PREFAB_OUTPUT)) {
                Directory.CreateDirectory(PREFAB_OUTPUT);
                AssetDatabase.Refresh();
            }

            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
            if (fbxAsset == null) {
                Debug.LogError("[BanditSetup] FBX not found at: " + FBX_PATH);
                return;
            }

            var lootBag = AssetDatabase.LoadAssetAtPath<GameObject>(LOOT_BAG_PATH);
            var meleeData = AssetDatabase.LoadAssetAtPath<EnemyData>(ENEMY_DATA_PATH + "Enemy_GruntMelee.asset");
            var rangedData = AssetDatabase.LoadAssetAtPath<EnemyData>(ENEMY_DATA_PATH + "Enemy_GruntRanged.asset");

            var fighterCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIM_OUTPUT + "AC_Bandit_Fighter.controller");
            var cutThroatCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIM_OUTPUT + "AC_Bandit_CutThroat.controller");
            var crossbowCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIM_OUTPUT + "AC_Bandit_Crossbow.controller");

            // Weapons config: which children to activate per prefab
            // Fighter: Cimitar_Hand active | R_Dagger, L_Dagger, CrossBow inactive
            // CutThroat: R_Dagger + L_Dagger active | Cimitar_Hand, CrossBow inactive
            // Crossbow: CrossBow active | R_Dagger, L_Dagger, Cimitar_Hand inactive

            CreateBanditPrefab("Bandit_Fighter", fbxAsset, fighterCtrl, meleeData, lootBag,
                activeWeapons: new[] { "Cimitar_Hand" },
                inactiveWeapons: new[] { "R_Dagger", "L_Dagger", "Cimitar_Belt", "CrossBow" });

            CreateBanditPrefab("Bandit_CutThroat", fbxAsset, cutThroatCtrl, meleeData, lootBag,
                activeWeapons: new[] { "R_Dagger", "L_Dagger" },
                inactiveWeapons: new[] { "Cimitar_Hand", "Cimitar_Belt", "CrossBow" });

            CreateBanditPrefab("Bandit_Crossbow", fbxAsset, crossbowCtrl, rangedData, lootBag,
                activeWeapons: new[] { "CrossBow" },
                inactiveWeapons: new[] { "R_Dagger", "L_Dagger", "Cimitar_Hand", "Cimitar_Belt" });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BanditSetup] All 3 Bandit prefabs created successfully.");
        }

        private static void CreateBanditPrefab(string prefabName, GameObject fbxAsset, RuntimeAnimatorController animCtrl,
            EnemyData enemyData, GameObject lootBag, string[] activeWeapons, string[] inactiveWeapons) {

            string prefabPath = PREFAB_OUTPUT + prefabName + ".prefab";

            // Create root GO
            var root = new GameObject(prefabName);
            root.layer = LayerMask.NameToLayer("Enemy");

            // Add NetworkObject first (required by EnemyMob)
            root.AddComponent<NetworkObject>();

            // Add EnemyMob and set data via SerializedObject
            var mob = root.AddComponent<EnemyMob>();

            // Add NavMeshAgent
            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = enemyData != null ? enemyData.MoveSpeed : 3.5f;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 1.5f;
            agent.radius = 0.4f;
            agent.height = 2f;

            // Add NetworkTransform
            root.AddComponent<FishNet.Component.Transforming.NetworkTransform>();

            // Add TargetingSystem
            root.AddComponent<TargetingSystem>();

            // Add EnemyVisualRandomizer
            var randomizer = root.AddComponent<EnemyVisualRandomizer>();

            // Add CapsuleCollider on root
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.37f;
            col.height = 2f;
            col.center = new Vector3(0, 1f, 0);

            // Instantiate FBX as child
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            model.transform.SetParent(root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            model.name = "Bandits_Base";

            // Set Animator controller
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = animCtrl;

            // Configure weapons visibility
            var activeSet = new HashSet<string>(activeWeapons);
            var inactiveSet = new HashSet<string>(inactiveWeapons);
            foreach (Transform child in model.transform) {
                if (activeSet.Contains(child.name)) child.gameObject.SetActive(true);
                if (inactiveSet.Contains(child.name)) child.gameObject.SetActive(false);
            }

            // Wire EnemyVisualRandomizer references to FBX children
            WireRandomizerRefs(randomizer, model.transform);

            // Wire EnemyMob serialized fields via SerializedObject
            // First save as prefab so we can modify it
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            // Use SerializedObject to set private [SerializeField] fields
            var so = new SerializedObject(prefab.GetComponent<EnemyMob>());
            var dataProp = so.FindProperty("_data");
            if (dataProp != null && enemyData != null) dataProp.objectReferenceValue = enemyData;
            var lootProp = so.FindProperty("_lootBagPrefab");
            if (lootProp != null && lootBag != null) lootProp.objectReferenceValue = lootBag;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Wire randomizer on the prefab too
            WireRandomizerRefsSerialized(prefab, prefab.transform.Find("Bandits_Base"));

            // Set FishNet AssetPathHash manually (matching DefaultPrefabObjects algorithm)
            var nob = prefab.GetComponent<NetworkObject>();
            if (nob != null) {
                string pathAndName = $"{prefabPath}{prefabName}".Trim().ToLowerInvariant();
                var sb = new System.Text.StringBuilder();
                foreach (char c in pathAndName) {
                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                        sb.Append(c);
                }
                ulong hash = Fnv1a64(sb.ToString());
                nob.SetAssetPathHash(hash);
                EditorUtility.SetDirty(nob);
            }

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[BanditSetup] Created prefab: {prefabPath}");
        }

        // FNV-1a 64-bit hash (same as FishNet's GetStableHashU64)
        private static ulong Fnv1a64(string txt) {
            unchecked {
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < txt.Length; i++) {
                    hash = hash * 1099511628211UL;
                    hash = hash ^ txt[i];
                }
                return hash;
            }
        }

        private static void WireRandomizerRefs(EnemyVisualRandomizer randomizer, Transform model) {
            // This wires during scene instance — for prefab we'll use SerializedObject
        }

        private static void WireRandomizerRefsSerialized(GameObject prefab, Transform model) {
            if (model == null) {
                Debug.LogWarning("[BanditSetup] Model transform not found in prefab for randomizer wiring.");
                return;
            }

            var randomizer = prefab.GetComponent<EnemyVisualRandomizer>();
            if (randomizer == null) return;

            var so = new SerializedObject(randomizer);

            SetChildRef(so, "_hood", model, "Hood");
            SetChildRef(so, "_chestpads", model, "ChestPads");
            SetChildRef(so, "_handsAcc", model, "Hands_ACC");
            SetChildRef(so, "_skirt2", model, "Skirt2");
            SetChildRef(so, "_crossbelt", model, "CrossBelt");
            SetChildRef(so, "_shoulderpads", model, "ShoulderPads");
            SetChildRef(so, "_hairstyle1", model, "Hairstyle_1");
            SetChildRef(so, "_hairstyle2", model, "Hairstyle_2");
            SetChildRef(so, "_hairstyle3", model, "Hairstyle_3");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetChildRef(SerializedObject so, string propertyName, Transform model, string childName) {
            var prop = so.FindProperty(propertyName);
            if (prop == null) {
                Debug.LogWarning($"[BanditSetup] Property '{propertyName}' not found on EnemyVisualRandomizer.");
                return;
            }

            var child = model.Find(childName);
            if (child != null) {
                prop.objectReferenceValue = child.gameObject;
            } else {
                Debug.LogWarning($"[BanditSetup] Child '{childName}' not found in model hierarchy.");
            }
        }
    }
}
