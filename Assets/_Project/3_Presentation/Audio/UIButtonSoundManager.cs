using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genesis.Presentation.Audio {

    /// <summary>
    /// Registra un callback TrickleDown en todos los UIDocuments activos de la escena.
    /// Cualquier Button que reciba un click emite UI_Click automaticamente, sin necesidad
    /// de modificar cada controller individualmente.
    ///
    /// Agregar este componente en el Bootstrap GameObject (junto a AudioManager).
    /// </summary>
    public class UIButtonSoundManager : MonoBehaviour {

        private readonly HashSet<VisualElement> _registeredRoots = new HashSet<VisualElement>();

        private void Start() {
            // Escaneo inicial
            RegisterAllDocuments();
            StartCoroutine(PeriodicRescan());
        }

        private void OnDisable() {
            // Al destruirse, limpiamos las referencias para evitar memory leaks en hot-reload
            _registeredRoots.Clear();
        }

        // ═══════════════════════════════════════════════════════
        // REGISTRO
        // ═══════════════════════════════════════════════════════

        private void RegisterAllDocuments() {
            var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in docs) {
                RegisterDocument(doc);
            }
        }

        private void RegisterDocument(UIDocument doc) {
            if (doc == null || doc.rootVisualElement == null) return;
            var root = doc.rootVisualElement;
            if (_registeredRoots.Contains(root)) return;

            // TrickleDown = captura antes de que el evento llegue al target
            // Nos aseguramos de que el sonido se reproduzca en todos los clicks de Button
            root.RegisterCallback<ClickEvent>(OnAnyClick, TrickleDown.TrickleDown);
            _registeredRoots.Add(root);
        }

        // Re-escanea cada 2 segundos para capturar UIDocuments que se habiliten despues del Start
        private IEnumerator PeriodicRescan() {
            var wait = new WaitForSeconds(2f);
            while (true) {
                yield return wait;
                RegisterAllDocuments();
                // Eliminar raices que ya no existen (panels destruidos)
                _registeredRoots.RemoveWhere(r => r == null);
            }
        }

        // ═══════════════════════════════════════════════════════
        // CALLBACK
        // ═══════════════════════════════════════════════════════

        private void OnAnyClick(ClickEvent evt) {
            // Sube por el arbol de elementos desde el target hasta encontrar un Button
            var element = evt.target as VisualElement;
            while (element != null) {
                if (element is Button) {
                    UISoundPlayer.PlayClick();
                    return;
                }
                element = element.parent;
            }
        }
    }
}
