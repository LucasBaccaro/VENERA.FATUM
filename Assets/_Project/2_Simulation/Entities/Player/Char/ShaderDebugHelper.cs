using UnityEngine;

/// <summary>
/// Script de debug para verificar que las propiedades globales del shader se estén enviando correctamente
/// Adjunta este script a cualquier GameObject en la escena y revisa la consola en Play Mode
/// </summary>
public class ShaderDebugHelper : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool logEveryFrame = false;
    public float logInterval = 1f;
    
    private float _nextLogTime = 0f;
    
    void Update()
    {
        if (logEveryFrame || Time.time >= _nextLogTime)
        {
            LogShaderGlobals();
            _nextLogTime = Time.time + logInterval;
        }
    }
    
    void LogShaderGlobals()
    {
        // Obtener las propiedades globales del shader
        Vector4 playerScreenPos = Shader.GetGlobalVector("_PlayerScreenPos");
        float playerY = Shader.GetGlobalFloat("_PlayerY");
        float heightCutOffset = Shader.GetGlobalFloat("_HeightCutOffset");
        float screenAspect = Shader.GetGlobalFloat("_ScreenAspect");
        
        Debug.Log($"<color=cyan>SHADER GLOBALS DEBUG:</color>\n" +
                  $"  _PlayerScreenPos: ({playerScreenPos.x:F3}, {playerScreenPos.y:F3})\n" +
                  $"  _PlayerY: {playerY:F3}\n" +
                  $"  _HeightCutOffset: {heightCutOffset:F3}\n" +
                  $"  _ScreenAspect: {screenAspect:F3}\n" +
                  $"  Height Threshold: {playerY - heightCutOffset:F3}");
    }
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Dibujar una línea horizontal en el umbral de altura
        float playerY = Shader.GetGlobalFloat("_PlayerY");
        float heightCutOffset = Shader.GetGlobalFloat("_HeightCutOffset");
        float threshold = playerY - heightCutOffset;
        
        Gizmos.color = Color.yellow;
        Vector3 center = Camera.main.transform.position;
        center.y = threshold;
        
        // Dibujar un plano horizontal en el umbral
        for (int i = -10; i <= 10; i++)
        {
            Vector3 start = center + new Vector3(i, 0, -10);
            Vector3 end = center + new Vector3(i, 0, 10);
            Gizmos.DrawLine(start, end);
            
            start = center + new Vector3(-10, 0, i);
            end = center + new Vector3(10, 0, i);
            Gizmos.DrawLine(start, end);
        }
        
        // Dibujar texto con la altura
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(center, $"Height Threshold: {threshold:F2}m");
        #endif
    }
}
