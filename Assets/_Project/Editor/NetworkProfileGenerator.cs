using UnityEngine;
using UnityEditor;
using System.IO;
using Genesis.Core.Networking;

namespace Genesis.Editor
{
    public class NetworkProfileGenerator : EditorWindow
    {
        [MenuItem("Tools/Network/Generate Visibility Profiles")]
        public static void GenerateProfiles()
        {
            string folder = "Assets/_Project/1_Data/Resources/NetworkProfiles";

            // Create folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder("Assets/_Project/1_Data/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/1_Data", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/1_Data/Resources", "NetworkProfiles");
            }

            int created = 0;
            int skipped = 0;

            // Player Profile
            created += CreateProfile(folder, "PlayerVisibilityProfile", new ProfileConfig
            {
                profileName = "Player",
                maxDistance = 120f,
                updateInterval = 1f,
                alwaysVisibleToOwner = true
            });

            // Boss Profile
            created += CreateProfile(folder, "BossVisibilityProfile", new ProfileConfig
            {
                profileName = "Boss",
                maxDistance = 200f,
                updateInterval = 0.5f,
                alwaysVisibleToOwner = false
            });

            // NPC Generic Profile
            created += CreateProfile(folder, "NPCVisibilityProfile", new ProfileConfig
            {
                profileName = "NPC Generic",
                maxDistance = 80f,
                updateInterval = 1.5f,
                alwaysVisibleToOwner = false
            });

            // Item Profile
            created += CreateProfile(folder, "ItemVisibilityProfile", new ProfileConfig
            {
                profileName = "Item",
                maxDistance = 30f,
                updateInterval = 2f,
                alwaysVisibleToOwner = false
            });

            // Quest NPC Profile (visible desde lejos)
            created += CreateProfile(folder, "QuestNPCVisibilityProfile", new ProfileConfig
            {
                profileName = "Quest NPC",
                maxDistance = 150f,
                updateInterval = 2f,
                alwaysVisibleToOwner = false
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[NetworkProfileGenerator] ✅ Created {created} visibility profiles in {folder}</color>");

            EditorUtility.DisplayDialog(
                "Network Visibility Profiles Generated",
                $"Created {created} profiles:\n\n" +
                "- PlayerVisibilityProfile (120m)\n" +
                "- BossVisibilityProfile (200m)\n" +
                "- NPCVisibilityProfile (80m)\n" +
                "- ItemVisibilityProfile (30m)\n" +
                "- QuestNPCVisibilityProfile (150m)\n\n" +
                "Location: " + folder,
                "OK"
            );

            // Select folder
            Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(folder);
            Selection.activeObject = folderObj;
            EditorGUIUtility.PingObject(folderObj);
        }

        private static int CreateProfile(string folder, string fileName, ProfileConfig config)
        {
            string path = $"{folder}/{fileName}.asset";

            // Check if already exists
            if (File.Exists(path))
            {
                Debug.Log($"[NetworkProfileGenerator] Profile already exists, skipping: {fileName}");
                return 0;
            }

            // Create profile
            NetworkVisibilityProfile profile = ScriptableObject.CreateInstance<NetworkVisibilityProfile>();
            profile.profileName = config.profileName;
            profile.maxDistance = config.maxDistance;
            profile.updateInterval = config.updateInterval;
            profile.useDistanceSquared = true;
            profile.alwaysVisibleToOwner = config.alwaysVisibleToOwner;

            AssetDatabase.CreateAsset(profile, path);
            Debug.Log($"[NetworkProfileGenerator] Created profile: {fileName} | Distance: {config.maxDistance}m");

            return 1;
        }

        private struct ProfileConfig
        {
            public string profileName;
            public float maxDistance;
            public float updateInterval;
            public bool alwaysVisibleToOwner;
        }

        [MenuItem("Tools/Network/Apply Distance Culling to Player Prefab")]
        public static void ApplyToPlayerPrefab()
        {
            string prefabPath = "Assets/_Project/5_Content/Prefabs/Player/Player.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Error", $"Player prefab not found at {prefabPath}", "OK");
                return;
            }

            // Check if already has component
            var existing = prefab.GetComponent<Genesis.Core.Networking.NetworkDistanceCulling>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Already Configured",
                    "Player prefab already has NetworkDistanceCulling component.\n\n" +
                    "You can manually configure it in the Inspector.",
                    "OK"
                );
                Selection.activeObject = prefab;
                return;
            }

            // Load profile
            string profilePath = "Assets/_Project/1_Data/Resources/NetworkProfiles/PlayerVisibilityProfile.asset";
            NetworkVisibilityProfile profile = AssetDatabase.LoadAssetAtPath<NetworkVisibilityProfile>(profilePath);

            if (profile == null)
            {
                bool generate = EditorUtility.DisplayDialog(
                    "Profile Not Found",
                    "PlayerVisibilityProfile not found. Generate profiles first?",
                    "Yes, Generate",
                    "Cancel"
                );

                if (generate)
                {
                    GenerateProfiles();
                    profile = AssetDatabase.LoadAssetAtPath<NetworkVisibilityProfile>(profilePath);
                }
                else
                {
                    return;
                }
            }

            // Open prefab for editing
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);

            // Add component
            var culling = instance.AddComponent<Genesis.Core.Networking.NetworkDistanceCulling>();

            // Assign profile using reflection (since it's a prefab instance)
            SerializedObject so = new SerializedObject(culling);
            SerializedProperty profileProp = so.FindProperty("profile");
            profileProp.objectReferenceValue = profile;
            so.ApplyModifiedProperties();

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            PrefabUtility.UnloadPrefabContents(instance);

            Debug.Log("<color=green>[NetworkProfileGenerator] ✅ Added NetworkDistanceCulling to Player prefab with PlayerVisibilityProfile (120m)</color>");

            EditorUtility.DisplayDialog(
                "Success",
                "NetworkDistanceCulling added to Player prefab!\n\n" +
                "Configuration:\n" +
                "- Profile: PlayerVisibilityProfile\n" +
                "- Max Distance: 120m\n" +
                "- Update Interval: 1s\n\n" +
                "Players will only see other players within 120 meters.",
                "OK"
            );

            // Select prefab
            Selection.activeObject = prefab;
        }

        [MenuItem("Tools/Network/Remove Distance Culling from Player Prefab")]
        public static void RemoveFromPlayerPrefab()
        {
            string prefabPath = "Assets/_Project/5_Content/Prefabs/Player/Player.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Error", $"Player prefab not found at {prefabPath}", "OK");
                return;
            }

            // Check if has component
            var existing = prefab.GetComponent<Genesis.Core.Networking.NetworkDistanceCulling>();
            if (existing == null)
            {
                EditorUtility.DisplayDialog(
                    "Not Found",
                    "Player prefab does not have NetworkDistanceCulling component.",
                    "OK"
                );
                return;
            }

            // Open prefab for editing
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);

            var component = instance.GetComponent<Genesis.Core.Networking.NetworkDistanceCulling>();
            if (component != null)
            {
                Object.DestroyImmediate(component);
            }

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            PrefabUtility.UnloadPrefabContents(instance);

            Debug.Log("[NetworkProfileGenerator] Removed NetworkDistanceCulling from Player prefab");

            EditorUtility.DisplayDialog(
                "Success",
                "NetworkDistanceCulling removed from Player prefab.\n\n" +
                "All players will now be visible regardless of distance.",
                "OK"
            );
        }
    }
}
