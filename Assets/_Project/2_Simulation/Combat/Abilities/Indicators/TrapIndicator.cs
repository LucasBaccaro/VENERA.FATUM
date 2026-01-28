using UnityEngine;
using Genesis.Data;
using UnityEngine.Rendering.Universal;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Indicador para trampas (ej: Trampa de Hielo)
    /// Muestra un círculo de placement + preview del modelo de la trampa
    /// Versión decal: proyecta una textura circular en el suelo adaptándose al terreno.
    /// </summary>
    public class TrapIndicator : AbilityIndicator {

        [Header("Decal Components")]
        [SerializeField] private DecalProjector decal; // Círculo de placement (decal)
        [SerializeField] private float projectionDepth = 6f; // Profundidad de proyección del decal

        [Header("Trap Model")]
        [SerializeField] private GameObject trapModelPreview; // Preview del modelo de trampa
        [SerializeField] private float modelHeightOffset = 0.2f; // Altura del preview de la trampa

        [Header("Settings")]
        [Tooltip("Distancia máxima desde el jugador donde se puede colocar la trampa (en unidades del mundo)")]
        [SerializeField] private float maxPlacementDistance = 5f;
        [Tooltip("Radio con el que fue diseñado el prefab (escala 1).")]
        [SerializeField] private float baseRadius = 2.5f;
        [Tooltip("Altura del volumen de proyección del decal.")]
        [SerializeField] private float decalHeight = 3f;

        private float _triggerRadius;
        private Vector3 _targetPoint;

        public override void Initialize(AbilityData abilityData) {
            this.transform.localScale = Vector3.one; // FIX: Reset root scale
            _abilityData = abilityData;
            _triggerRadius = abilityData.Radius;
            maxPlacementDistance = abilityData.Range;

            // Buscar DecalProjector si no está asignado
            if (decal == null)
                decal = GetComponentInChildren<DecalProjector>(true);

            // Configurar DecalProjector y Transform
            if (decal != null) {
                // Material personalizado por habilidad (si existe)
                if (abilityData.IndicatorMaterial != null)
                    decal.material = abilityData.IndicatorMaterial;

                // 1. FORZAR MODO DE ESCALADO
                decal.scaleMode = DecalScaleMode.InheritFromHierarchy;

                // 2. ESCALAR EL TRANSFORM
                this.transform.localScale = Vector3.one;
                float scaleMultiplier = _triggerRadius / baseRadius;
                this.transform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);

                // 3. CONFIGURAR TAMAÑO DEL DECAL
                float baseDiameter = baseRadius * 2f;
                decal.size = new Vector3(baseDiameter, baseDiameter, projectionDepth);
                decal.pivot = Vector3.zero;

                // Asegurar que el objeto del decal no tenga escala propia que interfiera
                decal.transform.localScale = Vector3.one;
            }

            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction) {

            // === PROYECCIÓN A SUELO ===
            bool foundGround = TryProjectToGround(worldPoint, out _targetPoint);

            // === ACTUALIZAR DECAL PROJECTOR (Círculo de placement) ===
            if (decal != null) {
                // Posición: levantar el projector arriba del suelo para que atraviese el terreno
                float yLift = Mathf.Max(0.25f, decalHeight * 0.5f);
                decal.transform.position = _targetPoint + Vector3.up * yLift;

                // Rotación: proyectar hacia abajo (90° en X)
                decal.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                // Color del decal según validez
                UpdateDecalColor();
            }

            // === ACTUALIZAR PREVIEW DEL MODELO DE TRAMPA ===
            if (trapModelPreview != null) {
                trapModelPreview.transform.position = _targetPoint + Vector3.up * modelHeightOffset;
                trapModelPreview.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

                // Cambiar transparencia/color según validez
                if (trapModelPreview.TryGetComponent<Renderer>(out var modelRenderer)) {
                    UpdateRendererColor(modelRenderer);
                }
            }

            // === VALIDACIÓN (throttleada) ===
            if (ShouldValidate()) {
                // Validar que:
                // 1. Se haya encontrado suelo válido
                // 2. La distancia al caster esté dentro del rango permitido
                float distanceToCaster = Vector3.Distance(transform.position, _targetPoint);
                bool inRange = distanceToCaster <= maxPlacementDistance;
                bool wasValid = _isValid;
                _isValid = foundGround && inRange;

                // DEBUG: Log cuando cambia el estado
                if (wasValid != _isValid) {
                    if (_isValid) {
                        Debug.Log($"[TrapIndicator] NOW VALID at dist={distanceToCaster:F2}m (max={maxPlacementDistance:F2}m)");
                    } else {
                        Debug.LogWarning($"[TrapIndicator] NOW INVALID: foundGround={foundGround}, inRange={inRange} (dist={distanceToCaster:F2}m > max={maxPlacementDistance:F2}m)");
                    }
                }
            }
        }

        /// <summary>
        /// Actualiza el color del decal según validez
        /// </summary>
        private void UpdateDecalColor() {
            if (decal == null || decal.material == null) return;

            // Usar el sistema de colores del parent (validColor/invalidColor)
            Color targetColor = _isValid ? validColor : invalidColor;

            // Intentar setear el color en el material del decal
            if (decal.material.HasProperty("_BaseColor"))
                decal.material.SetColor("_BaseColor", targetColor);
            else if (decal.material.HasProperty("_Color"))
                decal.material.SetColor("_Color", targetColor);
        }

        public override Vector3 GetTargetPoint() => _targetPoint;

        public override Vector3 GetDirection() {
            // Para trampas, la dirección no es crítica (es un objeto estático)
            return (_targetPoint - transform.position).normalized;
        }

        public override bool IsValid() => _isValid;

        public override void Show() {
            base.Show();
            if (decal != null) decal.enabled = true;
            if (trapModelPreview != null) trapModelPreview.SetActive(true);
        }

        public override void Hide() {
            base.Hide();
            if (decal != null) decal.enabled = false;
            if (trapModelPreview != null) trapModelPreview.SetActive(false);
        }
    }
}
