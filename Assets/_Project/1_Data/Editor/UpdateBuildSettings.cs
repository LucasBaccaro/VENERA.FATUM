
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;

public static class UpdateBuildSettings
{
    [MenuItem("Genesis/Tools/Update Build Settings (Login First)")]
    public static void Execute()
    {
        var scenes = new List<EditorBuildSettingsScene>();

        // Login scene first (index 0)
        scenes.Add(new EditorBuildSettingsScene("Assets/_Project/5_Content/Scenes/Login.unity", true));

        // Bootstrap second (index 1)
        scenes.Add(new EditorBuildSettingsScene("Assets/_Project/4_Bootstrap/Bootstrap.unity", true));

        // Keep existing chunk scenes
        string[] chunkPaths = new[]
        {
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_0_0.unity",
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_0_1.unity",
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_0_2.unity",
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_1_0.unity",
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_1_1.unity",
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_1_2.unity",
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_2_0.unity",
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_2_1.unity",
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_2_2.unity",
            "Assets/_Project/5_Content/Scenes/Chunks/Chunk_50_0.unity"
        };

        foreach (var path in chunkPaths)
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        UnityEngine.Debug.Log($"[UpdateBuildSettings] Build settings updated: {scenes.Count} scenes. Login=0, Bootstrap=1");
    }
}
#endif
