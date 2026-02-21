using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Genesis.Data.Dungeon;
using UnityEngine.SceneManagement;

namespace Genesis.Editor.Dungeon
{
    public class RefinedGenerationScript : EditorWindow
    {
        private const string WorldAnchorName = "[WORLD]";
        private static Vector3 CenterPos = new Vector3(-66.1152344f, 0f, -64.6392593f);
        private const float GridSize = 2f;
        private const int Cells = 45;

        // . = Floor, ' ' = Separator/Void (Transition based walls)
        private static string[] LayoutMap = {
            ".............................................", // 44 (N)
            ".............................................",
            ".....               .........................",
            ".....  ...........  .........................",
            ".....  .         .  .........       .........",
            ".....  .  PLAZA  .  .........       .........",
            ".....  .         .  .........       .........",
            ".....  ...........  .........       .........",
            ".....               .........       .........",
            ".....  ...........  .........       .........",
            ".....  .         .  .........       .........",
            ".....  .         .  .........       .........",
            ".............................................",
            ".............................................",
            "                                             ", // 30 (Corridor)
            "                                             ",
            "     .....   .....   .....   .....   .....   ", // 28 (Cells)
            "     .   .   .   .   .   .   .   .   .   .   ",
            "     . 1 .   . 2 .   . 3 .   . 4 .   . 5 .   ",
            "     .   .   .   .   .   .   .   .   .   .   ",
            "     .....   .....   .....   .....   .....   ",
            "                                             ", // 23
            "                                             ",
            "..................         ..................",
            "..................         ..................",
            "..                         ..................",
            "..   ..........            ..................",
            "..   . ROOM   .            ..................",
            "..   ..........            ..................",
            "..                                           ",
            "..   ..........            ..................",
            "..   . ROOM   .            ..................",
            "..   ..........            ..................",
            "..                                           ",
            "..................         ..................",
            "..................         ..................",
            "                           ..................",
            "    ..........             .                .",
            "    .        .             .     BIG ROOM   .",
            "    . CORNER .             .                .",
            "    .        .             .                .",
            "    ..........             ..................",
            "..................                           ",
            ".............................................",
            "............................................."  // 0 (S)
        };

        [MenuItem("Genesis/Dungeon/Refined Generator (90x90)")]
        public static void Generate()
        {
            Transform worldAnchor = FindWorldAnchor();
            if (worldAnchor == null) return;

            Cleanup(worldAnchor);

            Transform floorC = CreateContainer("Floor_Container", worldAnchor);
            Transform wallC = CreateContainer("Wall_Container", worldAnchor);
            Transform columnC = CreateContainer("Column_Container", worldAnchor);

            DungeonTheme theme = AssetDatabase.LoadAssetAtPath<DungeonTheme>("Assets/_Project/5_Content/Prefabs/Dungeon/Prision/Prision.asset");
            if (theme == null) return;

            float offset = (Cells * GridSize) / 2f;
            Vector3 startPos = CenterPos - new Vector3(offset, 0, offset);

            // 0=Void, 1-26=Rooms, 99=Plaza, 100=Corridors (Blue)
            int[,] roomMap = new int[Cells, Cells];

            // --- 1. CORE CONSTRAINTS: BLUE CORRIDORS ---
            // (Verticals)
            FillRoom(roomMap, 9, 5, 5, 20, 100);   // Left vertical
            FillRoom(roomMap, 30, 5, 5, 35, 100);  // Right vertical
            // (Horizontals)
            FillRoom(roomMap, 9, 20, 26, 5, 100);  // Mid horizontal
            FillRoom(roomMap, 14, 10, 21, 5, 100); // Bottom horizontal
            FillRoom(roomMap, 30, 35, 10, 5, 100); // Top-right spur

            // --- 2. THE 26 ROOMS (Precision Mapped) ---
            
            // PLAZA Sector (Top-Left)
            FillRoom(roomMap, 4, 30, 18, 11, 99); 
            FillRoom(roomMap, 7, 33, 12, 5, 0); // Plaza void

            // Sector: Top Right
            FillRoom(roomMap, 24, 30, 6, 15, 1);
            FillRoom(roomMap, 35, 40, 5, 5, 2);
            FillRoom(roomMap, 40, 40, 5, 5, 3);
            FillRoom(roomMap, 40, 30, 5, 5, 4);
            FillRoom(roomMap, 35, 30, 5, 5, 5);
            FillRoom(roomMap, 40, 25, 5, 5, 6);
            FillRoom(roomMap, 35, 25, 5, 5, 7);

            // Sector: Middle
            FillRoom(roomMap, 35, 15, 10, 10, 8); // Room 8
            FillRoom(roomMap, 27, 15, 3, 5, 9);  // Room 9
            FillRoom(roomMap, 27, 10, 3, 5, 10); // Room 10
            FillRoom(roomMap, 24, 10, 3, 10, 11); // Room 11

            // Sector: Bottom Right
            FillRoom(roomMap, 40, 10, 5, 10, 12);
            FillRoom(roomMap, 35, 10, 5, 5, 13);
            FillRoom(roomMap, 35, 0, 10, 10, 14);
            FillRoom(roomMap, 27, 0, 8, 5, 15);
            FillRoom(roomMap, 27, 5, 8, 5, 16);

            // Sector: Bottom Center Cellblock
            FillRoom(roomMap, 19, 0, 8, 10, 17);
            FillRoom(roomMap, 24, 15, 6, 5, 18);
            FillRoom(roomMap, 19, 15, 5, 5, 19);
            FillRoom(roomMap, 14, 15, 5, 5, 20);
            FillRoom(roomMap, 9, 15, 5, 5, 21);
            FillRoom(roomMap, 14, 5, 5, 5, 26);

            // Sector: Bottom Left
            FillRoom(roomMap, 5, 25, 4, 5, 22);
            FillRoom(roomMap, 0, 17, 9, 8, 23);
            FillRoom(roomMap, 0, 8, 9, 9, 24);
            FillRoom(roomMap, 0, 0, 14, 8, 25);

            // --- 3. INSTANTIATE FLOORS ---
            for (int x = 0; x < Cells; x++)
            {
                for (int z = 0; z < Cells; z++)
                {
                    if (roomMap[x, z] != 0)
                    {
                        Vector3 pos = startPos + new Vector3(x * GridSize + GridSize / 2f, 0, z * GridSize + GridSize / 2f);
                        InstantiateRandomVariant(theme, 0, pos, Quaternion.identity, floorC);
                    }
                }
            }

            // --- 4. INSTANTIATE WALLS (Shared Edge Logic) ---
            for (int x = 0; x < Cells; x++)
            {
                for (int z = 0; z <= Cells; z++)
                {
                    int cur = (z < Cells) ? roomMap[x, z] : 0;
                    int prev = (z > 0) ? roomMap[x, z - 1] : 0;
                    if (cur != prev && (cur != 0 || prev != 0))
                    {
                        Vector3 pos = startPos + new Vector3(x * GridSize + GridSize / 2f, 0, z * GridSize);
                        Quaternion rot = (cur != 0) ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                        InstantiateRandomVariant(theme, 1, pos, rot, wallC);
                    }
                }
            }

            for (int z = 0; z < Cells; z++)
            {
                for (int x = 0; x <= Cells; x++)
                {
                    int cur = (x < Cells) ? roomMap[x, z] : 0;
                    int prev = (x > 0) ? roomMap[x - 1, z] : 0;
                    if (cur != prev && (cur != 0 || prev != 0))
                    {
                        Vector3 pos = startPos + new Vector3(x * GridSize, 0, z * GridSize + GridSize / 2f);
                        Quaternion rot = (cur != 0) ? Quaternion.Euler(0, 90, 0) : Quaternion.Euler(0, -90, 0);
                        InstantiateRandomVariant(theme, 1, pos, rot, wallC);
                    }
                }
            }

            // --- 5. COLUMN PLACEMENT (Junctions) ---
            for (int x = 0; x <= Cells; x++)
            {
                for (int z = 0; z <= Cells; z++)
                {
                    int q1 = (x < Cells && z < Cells) ? roomMap[x, z] : 0;
                    int q2 = (x > 0 && z < Cells) ? roomMap[x - 1, z] : 0;
                    int q3 = (x > 0 && z > 0) ? roomMap[x - 1, z - 1] : 0;
                    int q4 = (x < Cells && z > 0) ? roomMap[x, z - 1] : 0;

                    bool isHWall = (q1 != q2) || (q4 != q3);
                    bool isVWall = (q1 != q4) || (q2 != q3);

                    if (isHWall && isVWall)
                    {
                        Vector3 pos = startPos + new Vector3(x * GridSize, 0, z * GridSize);
                        InstantiateRandomVariant(theme, 3, pos, Quaternion.identity, columnC);
                    }
                }
            }

            Debug.Log($"[Generator] Corridor-Constrained Dungeon (26 Rooms) at {CenterPos}");
        }

        private static void FillRoom(int[,] map, int x, int z, int w, int h, int id)
        {
            for (int i = x; i < x + w && i < Cells; i++)
                for (int j = z; j < z + h && j < Cells; j++)
                    map[i, j] = id;
        }
        private static void InstantiateRandomVariant(DungeonTheme theme, int catIndex, Vector3 pos, Quaternion rot, Transform parent)
        {
            if (theme.Categories.Count <= catIndex) return;
            var variants = theme.Categories[catIndex].Variants;
            if (variants == null || variants.Length == 0) return;

            GameObject prefab = variants[Random.Range(0, variants.Length)];
            if (prefab == null) return;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = pos;
            instance.transform.localRotation = rot;
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Module");
        }

        private static void Cleanup(Transform parent)
        {
            List<GameObject> toDelete = new List<GameObject>();
            foreach (Transform child in parent)
            {
                if (child.name.Contains("_Container")) toDelete.Add(child.gameObject);
            }
            foreach (var obj in toDelete) Undo.DestroyObjectImmediate(obj);
        }

        private static Transform CreateContainer(string name, Transform parent)
        {
            GameObject health = GameObject.Find(name);
            if (health != null) Undo.DestroyObjectImmediate(health);

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }

        private static Transform FindWorldAnchor()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == WorldAnchorName) return root.transform;
                }
            }
            return null;
        }
    }
}
