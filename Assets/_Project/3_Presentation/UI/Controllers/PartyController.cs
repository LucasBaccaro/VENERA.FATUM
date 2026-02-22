using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using FishNet;
using FishNet.Object;
using Genesis.Core;
using Genesis.Simulation;

namespace Genesis.Presentation.UI {

    /// <summary>
    /// UIToolkit controller for the party panel and party invite popup.
    /// Attach to a GameObject with a UIDocument (PartyUI.uxml).
    /// Listens to EventBus: "OnPartyStateChanged", "OnPartyInviteReceived".
    /// </summary>
    public class PartyController : MonoBehaviour {

        [SerializeField] private UIDocument _uiDocument;

        // Panel
        private VisualElement _partyPanel;
        private VisualElement[] _memberFrames;
        private Label[] _memberNames;
        private ProgressBar[] _memberHpBars;

        // Invite popup
        private VisualElement _invitePopup;
        private Label _inviteMessage;
        private Button _acceptButton;
        private Button _declineButton;

        // Cached party members for HP updates
        private readonly List<NetworkObject> _cachedMembers = new List<NetworkObject>();
        private bool _initialized;

        private void Awake() {
            if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        }

        private void Start() {
            InitUI();
            EventBus.Subscribe("OnPartyStateChanged", OnPartyStateChanged);
            EventBus.Subscribe<string>("OnPartyInviteReceived", OnPartyInviteReceived);
        }

        private void OnDestroy() {
            EventBus.Unsubscribe("OnPartyStateChanged", OnPartyStateChanged);
            EventBus.Unsubscribe<string>("OnPartyInviteReceived", OnPartyInviteReceived);
        }

        private void InitUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            _partyPanel = root.Q<VisualElement>("PartyPanel");

            _memberFrames = new VisualElement[4];
            _memberNames = new Label[4];
            _memberHpBars = new ProgressBar[4];
            for (int i = 0; i < 4; i++) {
                _memberFrames[i] = root.Q<VisualElement>($"MemberFrame_{i}");
                _memberNames[i] = root.Q<Label>($"MemberName_{i}");
                _memberHpBars[i] = root.Q<ProgressBar>($"MemberHP_{i}");
            }

            _invitePopup = root.Q<VisualElement>("InvitePopup");
            _inviteMessage = root.Q<Label>("InviteMessage");
            _acceptButton = root.Q<Button>("AcceptButton");
            _declineButton = root.Q<Button>("DeclineButton");

            if (_acceptButton != null) _acceptButton.clicked += OnAcceptClicked;
            if (_declineButton != null) _declineButton.clicked += OnDeclineClicked;

            if (_partyPanel != null) _partyPanel.style.display = DisplayStyle.None;
            if (_invitePopup != null) _invitePopup.style.display = DisplayStyle.None;

            _initialized = true;
        }

        private void Update() {
            if (!_initialized) return;
            if (_partyPanel != null && _partyPanel.style.display == DisplayStyle.Flex) {
                UpdateMemberHPBars();
            }
        }

        // ═══════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ═══════════════════════════════════════════════════════

        private void OnPartyStateChanged() {
            RebuildPartyPanel();
        }

        private void OnPartyInviteReceived(string inviterName) {
            if (!_initialized) InitUI();
            if (_inviteMessage != null) {
                _inviteMessage.text = $"{inviterName} te invita a su party";
            }
            if (_invitePopup != null) _invitePopup.style.display = DisplayStyle.Flex;
        }

        // ═══════════════════════════════════════════════════════
        // PARTY PANEL
        // ═══════════════════════════════════════════════════════

        private void RebuildPartyPanel() {
            if (!_initialized) InitUI();

            var localPm = GetLocalPartyMember();
            if (localPm == null || !localPm.IsInParty) {
                _cachedMembers.Clear();
                if (_partyPanel != null) _partyPanel.style.display = DisplayStyle.None;
                HideAllMemberFrames();
                return;
            }

            if (_partyPanel != null) _partyPanel.style.display = DisplayStyle.Flex;

            _cachedMembers.Clear();
            _cachedMembers.AddRange(FindPartyMembers(localPm.PartyId));

            for (int i = 0; i < 4; i++) {
                if (_memberFrames[i] == null) continue;

                if (i < _cachedMembers.Count) {
                    _memberFrames[i].style.display = DisplayStyle.Flex;

                    if (_memberNames[i] != null) {
                        string pName = _cachedMembers[i].GetComponent<PlayerClassManager>()?.PlayerName ?? "Player";
                        var pm = _cachedMembers[i].GetComponent<PartyMember>();
                        _memberNames[i].text = (pm != null && pm.IsLeader) ? $"[L] {pName}" : pName;
                    }

                    UpdateMemberHP(i, _cachedMembers[i]);
                } else {
                    _memberFrames[i].style.display = DisplayStyle.None;
                }
            }
        }

        private void UpdateMemberHPBars() {
            for (int i = 0; i < _cachedMembers.Count && i < 4; i++) {
                if (_cachedMembers[i] != null) {
                    UpdateMemberHP(i, _cachedMembers[i]);
                }
            }
        }

        private void UpdateMemberHP(int index, NetworkObject member) {
            if (_memberHpBars[index] == null) return;
            var stats = member.GetComponent<PlayerStats>();
            if (stats != null && stats.MaxHealth > 0f) {
                _memberHpBars[index].value = (stats.CurrentHealth / stats.MaxHealth) * 100f;
            }
        }

        private void HideAllMemberFrames() {
            if (_memberFrames == null) return;
            for (int i = 0; i < 4; i++) {
                if (_memberFrames[i] != null) _memberFrames[i].style.display = DisplayStyle.None;
            }
        }

        // ═══════════════════════════════════════════════════════
        // INVITE POPUP
        // ═══════════════════════════════════════════════════════

        private void OnAcceptClicked() {
            if (_invitePopup != null) _invitePopup.style.display = DisplayStyle.None;
            GetLocalPartyMember()?.CmdAcceptInvite();
        }

        private void OnDeclineClicked() {
            if (_invitePopup != null) _invitePopup.style.display = DisplayStyle.None;
            GetLocalPartyMember()?.CmdDeclineInvite();
        }

        // ═══════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════

        private PartyMember GetLocalPartyMember() {
            if (InstanceFinder.ClientManager == null) return null;
            return InstanceFinder.ClientManager.Connection?.FirstObject?.GetComponent<PartyMember>();
        }

        private List<NetworkObject> FindPartyMembers(int partyId) {
            var result = new List<NetworkObject>();
            var allPMs = Object.FindObjectsByType<PartyMember>(FindObjectsSortMode.None);
            foreach (var pm in allPMs) {
                if (pm.IsInParty && pm.PartyId == partyId) {
                    result.Add(pm.NetworkObject);
                }
            }
            return result;
        }
    }
}
