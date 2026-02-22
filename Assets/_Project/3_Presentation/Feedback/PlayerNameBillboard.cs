using UnityEngine;
using TMPro;
using Genesis.Simulation;
using FishNet.Object;

namespace Genesis.Presentation.Feedback {
    /// <summary>
    /// Displays the player name as a world-space billboard below the character.
    /// Reads the name from PlayerClassManager's SyncVar (synced from DB via Nakama).
    /// </summary>
    public class PlayerNameBillboard : MonoBehaviour {
        [Header("Position")]
        [SerializeField] private Vector3 _offset = new Vector3(0, 2.2f, 0);

        [Header("Text Settings")]
        [SerializeField] private float _fontSize = 3f;
        [SerializeField] private Color _nameColor = new Color(0.95f, 0.92f, 0.78f);
        [SerializeField] private Color _outlineColor = new Color(0f, 0f, 0f, 0.8f);
        [SerializeField] private float _outlineWidth = 0.25f;

        [Header("Visibility")]
        [SerializeField] private bool _hideForOwner = false;

        private TextMeshPro _textMesh;
        private Transform _billboardTransform;
        private PlayerClassManager _classManager;
        private NetworkObject _networkObject;
        private bool _nameSet;

        private void Awake() {
            _classManager = GetComponent<PlayerClassManager>();
            _networkObject = GetComponent<NetworkObject>();
            CreateBillboard();
        }

        private void CreateBillboard() {
            var go = new GameObject("NameBillboard");
            _billboardTransform = go.transform;
            _billboardTransform.SetParent(transform, false);
            _billboardTransform.localPosition = _offset;

            _textMesh = go.AddComponent<TextMeshPro>();
            _textMesh.alignment = TextAlignmentOptions.Center;
            _textMesh.fontSize = _fontSize;
            _textMesh.color = _nameColor;
            _textMesh.enableWordWrapping = false;
            _textMesh.sortingOrder = 90;
            _textMesh.text = "";
            _textMesh.enabled = false;

            // Outline for readability
            _textMesh.outlineWidth = _outlineWidth;
            _textMesh.outlineColor = _outlineColor;

            // RectTransform size for proper centering
            var rt = _textMesh.rectTransform;
            rt.sizeDelta = new Vector2(5f, 1f);

            // Force render on top of everything: ZTest Always + Overlay queue
            _textMesh.ForceMeshUpdate();
            ForceOverlayRendering();
        }

        private void ForceOverlayRendering() {
            // Apply ZTest Always to the main material and all shared materials
            var renderer = _textMesh.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            foreach (var mat in renderer.materials) {
                mat.renderQueue = 5000;
                mat.SetInt("_ZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
        }

        private void LateUpdate() {
            if (_classManager == null) return;

            if (_hideForOwner && _networkObject != null && _networkObject.IsOwner) {
                if (_textMesh.enabled) _textMesh.enabled = false;
                return;
            }

            if (!_nameSet) {
                string playerName = _classManager.PlayerName;
                if (string.IsNullOrEmpty(playerName)) return;

                _textMesh.text = playerName;
                _textMesh.enabled = true;
                _textMesh.ForceMeshUpdate();
                ForceOverlayRendering();
                _nameSet = true;
            }

            // Billboard - match camera rotation
            var cam = Camera.main;
            if (cam != null && _billboardTransform != null) {
                _billboardTransform.rotation = cam.transform.rotation;
            }
        }
    }
}
