using UnityEngine;

namespace Genesis.Items
{
    [System.Serializable]
    public class EquipmentVisualData
    {
        [Header("Visual Settings")]
        [Tooltip("Prefab del item equipable (SkinnedMeshRenderer en root)")]
        public GameObject VisualPrefab;
        
        [Tooltip("Qué partes del cuerpo base ocultar cuando este item está equipado")]
        public BodyPartFlags HiddenBodyParts;
        
        [Header("Rarity Visual System")]
        [Tooltip("Configuración de rareza global")]
        public RarityVisualConfig RarityConfig;
        
        /// <summary>
        /// Gets the local transform from the visual prefab.
        /// This preserves the exact position/rotation/scale set in Blender.
        /// </summary>
        public (Vector3 position, Quaternion rotation, Vector3 scale) GetPrefabTransform()
        {
            if (VisualPrefab == null)
                return (Vector3.zero, Quaternion.identity, Vector3.one);
            
            Transform t = VisualPrefab.transform;
            return (t.localPosition, t.localRotation, t.localScale);
        }
    }
}
