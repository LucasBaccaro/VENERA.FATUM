using UnityEngine;
using System.Collections.Generic;
using Genesis.Items;
using Genesis.Core;
using Genesis.Data;

namespace Genesis.Simulation
{
    /// <summary>
    /// Gestiona la visualización de equipamiento en el personaje.
    /// Se sincroniza con EquipmentManager para mostrar/ocultar items.
    /// </summary>
    public class PlayerEquipmentVisuals : MonoBehaviour
    {
        [Header("Body Parts References")]
        [SerializeField] private SkinnedMeshRenderer headBase;
        [SerializeField] private SkinnedMeshRenderer shouldersBase;
        [SerializeField] private SkinnedMeshRenderer chestBase;
        [SerializeField] private SkinnedMeshRenderer armsBase;
        [SerializeField] private SkinnedMeshRenderer handsBase;
        [SerializeField] private SkinnedMeshRenderer beltBase;
        [SerializeField] private SkinnedMeshRenderer pantsBase;
        [SerializeField] private SkinnedMeshRenderer feetBase;
        [SerializeField] private SkinnedMeshRenderer weaponBase;
        [SerializeField] private SkinnedMeshRenderer offhandBase;
        
        [Header("Equipment Attachment Points")]
        [SerializeField] private Transform headSlotTransform;
        [SerializeField] private Transform shouldersSlotTransform;
        [SerializeField] private Transform chestSlotTransform;
        [SerializeField] private Transform handsSlotTransform;
        [SerializeField] private Transform beltSlotTransform;
        [SerializeField] private Transform pantsSlotTransform;
        [SerializeField] private Transform feetSlotTransform;
        [SerializeField] private Transform weaponSlotTransform;
        [SerializeField] private Transform offhandSlotTransform;
        
        [Header("References")]
        [SerializeField] private EquipmentManager equipmentManager;
        [SerializeField] private RarityVisualConfig globalRarityConfig;
        
        // Cache de items equipados actualmente (slot -> GameObject instanciado)
        private Dictionary<EquipmentSlot, GameObject> _equippedVisuals = new Dictionary<EquipmentSlot, GameObject>();
        
        // Cache de qué partes del cuerpo están ocultas actualmente
        private BodyPartFlags _hiddenParts = BodyPartFlags.None;
        
        private void Start()
        {
            if (equipmentManager == null)
                equipmentManager = GetComponentInParent<EquipmentManager>();
            
            if (equipmentManager == null)
            {
                Debug.LogWarning($"[PlayerEquipmentVisuals] EquipmentManager not found on {gameObject.name} or its parents. Visuals will not work.");
                return;
            }
            
            // Suscribirse a cambios de equipamiento de este manager específicamente
            equipmentManager.OnEquipmentChangedSync += OnEquipmentChanged;
            
            // Inicializar: mostrar todas las partes del cuerpo base
            ShowAllBodyParts();
            
            // Refrescar inicial
            RefreshAllEquipment();
        }
        
        private void OnDestroy()
        {
            if (equipmentManager != null)
                equipmentManager.OnEquipmentChangedSync -= OnEquipmentChanged;
            
            // Limpiar items equipados (devolverlos al pool)
            foreach (var kvp in _equippedVisuals)
            {
                if (kvp.Value != null && EquipmentVisualPool.Instance != null)
                    EquipmentVisualPool.Instance.ReturnToPool(kvp.Value);
            }
            _equippedVisuals.Clear();
        }
        
        private void OnEquipmentChanged()
        {
            // Refrescar toda la visualización
            RefreshAllEquipment();
        }
        
        /// <summary>
        /// Refresca la visualización de todo el equipamiento.
        /// </summary>
        public void RefreshAllEquipment()
        {
            if (equipmentManager == null) return;
            
            // Resetear partes ocultas
            _hiddenParts = BodyPartFlags.None;
            
            // Procesar cada slot (solo si el transform de anclaje está asignado)
            RefreshSlot(EquipmentSlot.Head, equipmentManager.GetEquipmentSlot(EquipmentSlot.Head), headSlotTransform);
            RefreshSlot(EquipmentSlot.Shoulders, equipmentManager.GetEquipmentSlot(EquipmentSlot.Shoulders), shouldersSlotTransform);
            RefreshSlot(EquipmentSlot.Chest, equipmentManager.GetEquipmentSlot(EquipmentSlot.Chest), chestSlotTransform);
            RefreshSlot(EquipmentSlot.Hands, equipmentManager.GetEquipmentSlot(EquipmentSlot.Hands), handsSlotTransform);
            RefreshSlot(EquipmentSlot.Belt, equipmentManager.GetEquipmentSlot(EquipmentSlot.Belt), beltSlotTransform);
            RefreshSlot(EquipmentSlot.Pants, equipmentManager.GetEquipmentSlot(EquipmentSlot.Pants), pantsSlotTransform);
            RefreshSlot(EquipmentSlot.Feet, equipmentManager.GetEquipmentSlot(EquipmentSlot.Feet), feetSlotTransform);
            RefreshSlot(EquipmentSlot.Weapon, equipmentManager.GetEquipmentSlot(EquipmentSlot.Weapon), weaponSlotTransform);
            RefreshSlot(EquipmentSlot.OffHand, equipmentManager.GetEquipmentSlot(EquipmentSlot.OffHand), offhandSlotTransform);
            
            // Actualizar visibilidad de body parts
            UpdateBodyPartsVisibility();
        }
        
        private void RefreshSlot(EquipmentSlot slot, ItemSlot itemSlot, Transform attachmentPoint)
        {
            if (itemSlot.IsEmpty || attachmentPoint == null)
            {
                RemoveVisual(slot);
                return;
            }
            
            // Obtener data del item
            var itemData = ItemDatabase.Instance.GetEquipment(itemSlot.ItemID);
            if (itemData == null || itemData.VisualData == null)
            {
                RemoveVisual(slot);
                return;
            }
            
            // Equipar visual
            EquipVisual(slot, itemData.VisualData, attachmentPoint, itemSlot.Rarity);
            
            // Acumular partes del cuerpo a ocultar
            _hiddenParts |= itemData.VisualData.HiddenBodyParts;
        }
        
        private void EquipVisual(EquipmentSlot slot, EquipmentVisualData visualData, Transform attachmentPoint, ItemRarity rarity)
        {
            // Si ya hay un item en este slot, removerlo primero
            RemoveVisual(slot);
            
            if (EquipmentVisualPool.Instance == null)
            {
                Debug.LogWarning("[PlayerEquipmentVisuals] EquipmentVisualPool instance not found!");
                return;
            }
            
            // Obtener del pool
            GameObject visualInstance = EquipmentVisualPool.Instance.GetFromPool(visualData.VisualPrefab);
            
            if (visualInstance == null) return;
            
            // Attachear al punto de anclaje
            visualInstance.transform.SetParent(attachmentPoint);
            
            // Aplicar transform autodetectado (preserva Blender pos)
            var (pos, rot, scale) = visualData.GetPrefabTransform();
            visualInstance.transform.localPosition = pos;
            visualInstance.transform.localRotation = rot;
            visualInstance.transform.localScale = scale;

            // --- ATTACHMENT / RE-SKINNING LOGIC ---
            SkinnedMeshRenderer equipSMR = visualInstance.GetComponentInChildren<SkinnedMeshRenderer>();
            
            // Si el slot es de los que se "socketan" (Armas/OffHand), no transferimos huesos.
            // Simplemente se quedan como hijos del attachmentPoint (que debería ser un hijo del hueso de la mano).
            bool isSocketedSlot = (slot == EquipmentSlot.Weapon || slot == EquipmentSlot.OffHand);
            
            if (equipSMR != null && !isSocketedSlot)
            {
                // Si el item es ropa skinneada (pecheras, etc.), debe compartir la armature del personaje.
                SkinnedMeshRenderer referenceSMR = chestBase != null ? chestBase : headBase;
                if (referenceSMR != null)
                {
                    equipSMR.bones = referenceSMR.bones;
                    equipSMR.rootBone = referenceSMR.rootBone;
                }
            }
            
            // Aplicar rareza visual
            ApplyRarityVisual(visualInstance, visualData, rarity);
            
            // Cachear
            _equippedVisuals[slot] = visualInstance;
        }
        
        private void ApplyRarityVisual(GameObject visualInstance, EquipmentVisualData visualData, ItemRarity rarity)
        {
            RarityVisualConfig config = visualData.RarityConfig != null ? visualData.RarityConfig : globalRarityConfig;
            if (config == null) return;
            
            Renderer renderer = visualInstance.GetComponentInChildren<Renderer>();
            if (renderer == null) return;
            
            RarityVisualSettings settings = config.GetSettingsForRarity(rarity);
            
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propBlock);
            
            // Aplicar _FlakeColor
            propBlock.SetColor("_FlakeColor", settings.FlakeColor);
            
            // Aplicar fixed _FlakeIntensity (siempre 5.0)
            propBlock.SetFloat("_FlakeIntensity", RarityVisualSettings.FLAKE_INTENSITY);
            
            renderer.SetPropertyBlock(propBlock);
        }
        
        private void RemoveVisual(EquipmentSlot slot)
        {
            if (_equippedVisuals.TryGetValue(slot, out GameObject visual))
            {
                if (visual != null && EquipmentVisualPool.Instance != null)
                    EquipmentVisualPool.Instance.ReturnToPool(visual);
                
                _equippedVisuals.Remove(slot);
            }
        }
        
        private void UpdateBodyPartsVisibility()
        {
            SetBodyPartVisibility(headBase, BodyPartFlags.Head);
            SetBodyPartVisibility(shouldersBase, BodyPartFlags.Shoulders);
            SetBodyPartVisibility(chestBase, BodyPartFlags.Chest);
            SetBodyPartVisibility(armsBase, BodyPartFlags.Arms);
            SetBodyPartVisibility(handsBase, BodyPartFlags.Hands);
            SetBodyPartVisibility(beltBase, BodyPartFlags.Belt);
            SetBodyPartVisibility(pantsBase, BodyPartFlags.Pants);
            SetBodyPartVisibility(feetBase, BodyPartFlags.Feet);
            SetBodyPartVisibility(weaponBase, BodyPartFlags.Weapon);
            SetBodyPartVisibility(offhandBase, BodyPartFlags.OffHand);
        }
        
        private void SetBodyPartVisibility(SkinnedMeshRenderer renderer, BodyPartFlags flag)
        {
            if (renderer == null) return;
            bool shouldBeVisible = (_hiddenParts & flag) == 0;
            renderer.enabled = shouldBeVisible;
        }
        
        private void ShowAllBodyParts()
        {
            if (headBase) headBase.enabled = true;
            if (shouldersBase) shouldersBase.enabled = true;
            if (chestBase) chestBase.enabled = true;
            if (armsBase) armsBase.enabled = true;
            if (handsBase) handsBase.enabled = true;
            if (beltBase) beltBase.enabled = true;
            if (pantsBase) pantsBase.enabled = true;
            if (feetBase) feetBase.enabled = true;
            if (weaponBase) weaponBase.enabled = true;
            if (offhandBase) offhandBase.enabled = true;
        }
    }
}
