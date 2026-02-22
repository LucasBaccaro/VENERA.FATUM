using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using Genesis.Items;
using Genesis.Core;

namespace Genesis.Simulation {

    public class TradeManager : NetworkBehaviour {

        private TradeSession _currentSession;
        private PlayerInventory _inventory;
        private PlayerAttributes _attributes;
        private PlayerStats _stats;

        private void Awake() {
            _inventory = GetComponent<PlayerInventory>();
            _attributes = GetComponent<PlayerAttributes>();
            _stats = GetComponent<PlayerStats>();
        }

        public override void OnStopNetwork() {
            base.OnStopNetwork();
            if (base.IsServerInitialized && _currentSession != null) {
                CancelTradeInternal("Player disconnected");
            }
        }

        // ═══════════════════════════════════════════════════════
        // SERVER UPDATE - Countdown tick & death check
        // ═══════════════════════════════════════════════════════

        private void Update() {
            if (!base.IsServerInitialized) return;
            if (_currentSession == null) return;

            // Check death during active trade
            if (_currentSession.State == TradeState.Active ||
                _currentSession.State == TradeState.Countdown) {
                if (_stats != null && _stats.IsDead) {
                    CancelTradeInternal("Player died");
                    return;
                }

                var partner = _currentSession.GetPartner(base.NetworkObject);
                if (partner != null) {
                    var partnerStats = partner.GetComponent<PlayerStats>();
                    if (partnerStats != null && partnerStats.IsDead) {
                        CancelTradeInternal("Partner died");
                        return;
                    }
                }
            }

            // Countdown tick
            if (_currentSession.State == TradeState.Countdown) {
                _currentSession.CountdownTimer -= Time.deltaTime;
                if (_currentSession.CountdownTimer <= 0f) {
                    ExecuteTrade();
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // SERVER RPCs (Client → Server)
        // ═══════════════════════════════════════════════════════

        [ServerRpc]
        public void CmdRequestTrade(int targetObjectId) {
            Debug.Log($"[TradeManager] CmdRequestTrade called: from={base.ObjectId} to={targetObjectId}");

            if (_currentSession != null) {
                Debug.LogWarning("[TradeManager] REJECTED: Already in a trade");
                TargetTradeError(base.Owner, "You are already in a trade.");
                return;
            }

            if (!FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(targetObjectId, out NetworkObject targetObj)) {
                Debug.LogWarning($"[TradeManager] REJECTED: ObjectId {targetObjectId} not found in Spawned");
                TargetTradeError(base.Owner, "Player not found.");
                return;
            }

            if (targetObj == base.NetworkObject) {
                Debug.LogWarning("[TradeManager] REJECTED: Trading with self");
                TargetTradeError(base.Owner, "Cannot trade with yourself.");
                return;
            }

            var targetStats = targetObj.GetComponent<PlayerStats>();
            if (_stats == null || _stats.IsDead || targetStats == null || targetStats.IsDead) {
                Debug.LogWarning($"[TradeManager] REJECTED: Dead check - myStats={_stats != null}, myDead={_stats?.IsDead}, targetStats={targetStats != null}, targetDead={targetStats?.IsDead}");
                TargetTradeError(base.Owner, "Cannot trade while dead.");
                return;
            }

            if (_stats.IsInCombat) {
                Debug.LogWarning("[TradeManager] REJECTED: Requester in combat");
                TargetTradeError(base.Owner, "Cannot trade while in combat.");
                return;
            }

            if (targetStats.IsInCombat) {
                Debug.LogWarning("[TradeManager] REJECTED: Target in combat");
                TargetTradeError(base.Owner, "Target is in combat.");
                return;
            }

            float distance = Vector3.Distance(transform.position, targetObj.transform.position);
            if (distance > TradeConstants.TRADE_RANGE) {
                Debug.LogWarning($"[TradeManager] REJECTED: Too far ({distance:F1}m > {TradeConstants.TRADE_RANGE}m)");
                TargetTradeError(base.Owner, "Too far away.");
                return;
            }

            var targetTradeManager = targetObj.GetComponent<TradeManager>();
            if (targetTradeManager == null) {
                Debug.LogWarning("[TradeManager] REJECTED: No TradeManager on target");
                TargetTradeError(base.Owner, "Target cannot trade.");
                return;
            }

            if (targetTradeManager._currentSession != null) {
                Debug.LogWarning("[TradeManager] REJECTED: Target already trading");
                TargetTradeError(base.Owner, "Target is already trading.");
                return;
            }

            // Create session
            var session = new TradeSession(base.NetworkObject, targetObj);
            _currentSession = session;
            targetTradeManager._currentSession = session;
            TradeSession.Register(session);

            // Get requester name
            string requesterName = "Player";
            var classManager = GetComponent<PlayerClassManager>();
            if (classManager != null && !string.IsNullOrEmpty(classManager.PlayerName)) {
                requesterName = classManager.PlayerName;
            }

            Debug.Log($"[TradeManager] Trade request SENT: session={session.SessionId}, from={base.ObjectId} to={targetObjectId}, targetOwner={targetObj.Owner?.ClientId}");
            TargetTradeRequested(targetObj.Owner, session.SessionId, requesterName);
        }

        [ServerRpc]
        public void CmdRespondTrade(uint sessionId, bool accepted) {
            Debug.Log($"[TradeManager] CmdRespondTrade called: sessionId={sessionId}, accepted={accepted}, myObjectId={base.ObjectId}, hasSession={_currentSession != null}, sessionState={_currentSession?.State}");

            if (_currentSession == null || _currentSession.SessionId != sessionId) {
                Debug.LogWarning($"[TradeManager] REJECTED respond: currentSession={(_currentSession != null ? _currentSession.SessionId.ToString() : "NULL")}, requested={sessionId}");
                TargetTradeError(base.Owner, "No pending trade request.");
                return;
            }

            if (_currentSession.State != TradeState.Requested) {
                Debug.LogWarning($"[TradeManager] REJECTED respond: state={_currentSession.State}, expected=Requested");
                TargetTradeError(base.Owner, "Trade is not in request state.");
                return;
            }

            if (!accepted) {
                CancelTradeInternal("Trade declined");
                return;
            }

            _currentSession.State = TradeState.Active;

            var partner = _currentSession.GetPartner(base.NetworkObject);
            string localName = GetPlayerName();
            string partnerName = GetPlayerName(partner);

            TargetTradeOpened(base.Owner, _currentSession.SessionId, partnerName);
            TargetTradeOpened(partner.Owner, _currentSession.SessionId, localName);

            Debug.Log($"[TradeManager] Trade opened: session {sessionId}");
        }

        [ServerRpc]
        public void CmdAddTradeItem(int inventorySlotIndex) {
            if (_currentSession == null || _currentSession.State != TradeState.Active) {
                TargetTradeError(base.Owner, "No active trade.");
                return;
            }

            if (_inventory == null) return;

            var slot = _inventory.GetSlot(inventorySlotIndex);
            if (slot.IsEmpty) {
                TargetTradeError(base.Owner, "Empty inventory slot.");
                return;
            }

            var offer = _currentSession.GetOffer(base.NetworkObject);
            if (offer == null) return;

            // Check if slot is already offered
            for (int i = 0; i < TradeConstants.MAX_TRADE_SLOTS; i++) {
                if (offer.SourceSlots[i] == inventorySlotIndex) {
                    TargetTradeError(base.Owner, "Item already in trade.");
                    return;
                }
            }

            // Find empty trade slot
            int emptyTradeSlot = -1;
            for (int i = 0; i < TradeConstants.MAX_TRADE_SLOTS; i++) {
                if (offer.Items[i].IsEmpty) {
                    emptyTradeSlot = i;
                    break;
                }
            }

            if (emptyTradeSlot == -1) {
                TargetTradeError(base.Owner, "Trade slots full (max 6).");
                return;
            }

            offer.Items[emptyTradeSlot] = slot;
            offer.SourceSlots[emptyTradeSlot] = inventorySlotIndex;

            // Reset locks on any change
            _currentSession.ResetLocksAndAccepts();

            var partner = _currentSession.GetPartner(base.NetworkObject);
            NotifyOfferUpdate(base.NetworkObject);
            NotifyOfferUpdate(partner);
            NotifyLockChange(base.NetworkObject);
            NotifyLockChange(partner);

            Debug.Log($"[TradeManager] Item added to trade: slot {inventorySlotIndex} -> trade slot {emptyTradeSlot}");
        }

        [ServerRpc]
        public void CmdRemoveTradeItem(int tradeSlotIndex) {
            if (_currentSession == null || _currentSession.State != TradeState.Active) {
                TargetTradeError(base.Owner, "No active trade.");
                return;
            }

            var offer = _currentSession.GetOffer(base.NetworkObject);
            if (offer == null) return;

            if (tradeSlotIndex < 0 || tradeSlotIndex >= TradeConstants.MAX_TRADE_SLOTS) return;
            if (offer.Items[tradeSlotIndex].IsEmpty) return;

            offer.Items[tradeSlotIndex] = ItemSlot.Empty;
            offer.SourceSlots[tradeSlotIndex] = -1;

            _currentSession.ResetLocksAndAccepts();

            var partner = _currentSession.GetPartner(base.NetworkObject);
            NotifyOfferUpdate(base.NetworkObject);
            NotifyOfferUpdate(partner);
            NotifyLockChange(base.NetworkObject);
            NotifyLockChange(partner);
        }

        [ServerRpc]
        public void CmdSetTradeGold(int amount) {
            if (_currentSession == null || _currentSession.State != TradeState.Active) {
                TargetTradeError(base.Owner, "No active trade.");
                return;
            }

            if (amount < 0) amount = 0;

            if (_attributes != null && amount > _attributes.Gold) {
                amount = _attributes.Gold;
            }

            var offer = _currentSession.GetOffer(base.NetworkObject);
            if (offer == null) return;

            offer.Gold = amount;

            _currentSession.ResetLocksAndAccepts();

            var partner = _currentSession.GetPartner(base.NetworkObject);
            NotifyOfferUpdate(base.NetworkObject);
            NotifyOfferUpdate(partner);
            NotifyLockChange(base.NetworkObject);
            NotifyLockChange(partner);
        }

        [ServerRpc]
        public void CmdLockTrade() {
            if (_currentSession == null || _currentSession.State != TradeState.Active) {
                TargetTradeError(base.Owner, "No active trade.");
                return;
            }

            var offer = _currentSession.GetOffer(base.NetworkObject);
            if (offer == null) return;

            offer.IsLocked = true;

            var partner = _currentSession.GetPartner(base.NetworkObject);
            NotifyLockChange(base.NetworkObject);
            NotifyLockChange(partner);
        }

        [ServerRpc]
        public void CmdUnlockTrade() {
            if (_currentSession == null || _currentSession.State != TradeState.Active) {
                TargetTradeError(base.Owner, "No active trade.");
                return;
            }

            _currentSession.ResetLocksAndAccepts();

            var partner = _currentSession.GetPartner(base.NetworkObject);
            NotifyOfferUpdate(base.NetworkObject);
            NotifyOfferUpdate(partner);
            NotifyLockChange(base.NetworkObject);
            NotifyLockChange(partner);
        }

        [ServerRpc]
        public void CmdAcceptTrade() {
            if (_currentSession == null || _currentSession.State != TradeState.Active) {
                TargetTradeError(base.Owner, "No active trade.");
                return;
            }

            var myOffer = _currentSession.GetOffer(base.NetworkObject);
            var partnerOffer = _currentSession.GetPartnerOffer(base.NetworkObject);
            if (myOffer == null || partnerOffer == null) return;

            if (!myOffer.IsLocked || !partnerOffer.IsLocked) {
                TargetTradeError(base.Owner, "Both players must lock first.");
                return;
            }

            myOffer.HasAccepted = true;

            if (myOffer.HasAccepted && partnerOffer.HasAccepted) {
                // Both accepted - start countdown
                _currentSession.State = TradeState.Countdown;
                _currentSession.CountdownTimer = TradeConstants.COUNTDOWN_DURATION;

                TargetTradeCountdownStarted(base.Owner, TradeConstants.COUNTDOWN_DURATION);
                var partner = _currentSession.GetPartner(base.NetworkObject);
                TargetTradeCountdownStarted(partner.Owner, TradeConstants.COUNTDOWN_DURATION);

                Debug.Log($"[TradeManager] Countdown started for session {_currentSession.SessionId}");
            } else {
                // Notify partner that we accepted
                var partner = _currentSession.GetPartner(base.NetworkObject);
                NotifyLockChange(base.NetworkObject);
                NotifyLockChange(partner);
            }
        }

        [ServerRpc]
        public void CmdCancelTrade() {
            if (_currentSession == null) return;
            CancelTradeInternal("Trade cancelled");
        }

        // ═══════════════════════════════════════════════════════
        // TARGET RPCs (Server → Specific Client)
        // ═══════════════════════════════════════════════════════

        [TargetRpc]
        private void TargetTradeRequested(NetworkConnection conn, uint sessionId, string requesterName) {
            EventBus.Trigger<uint, string>("OnTradeRequested", sessionId, requesterName);
        }

        [TargetRpc]
        private void TargetTradeOpened(NetworkConnection conn, uint sessionId, string partnerName) {
            EventBus.Trigger<uint, string>("OnTradeOpened", sessionId, partnerName);
        }

        [TargetRpc]
        private void TargetTradeOfferUpdated(NetworkConnection conn,
            int[] myItemIds, int[] myItemQtys, int[] myItemTiers, int[] myItemRarities, int myGold, bool myLocked, bool myAccepted,
            int[] partnerItemIds, int[] partnerItemQtys, int[] partnerItemTiers, int[] partnerItemRarities, int partnerGold, bool partnerLocked, bool partnerAccepted) {
            var snapshot = new TradeOfferSnapshot {
                MyItemIds = myItemIds, MyItemQtys = myItemQtys, MyItemTiers = myItemTiers, MyItemRarities = myItemRarities,
                MyGold = myGold, MyLocked = myLocked, MyAccepted = myAccepted,
                PartnerItemIds = partnerItemIds, PartnerItemQtys = partnerItemQtys, PartnerItemTiers = partnerItemTiers, PartnerItemRarities = partnerItemRarities,
                PartnerGold = partnerGold, PartnerLocked = partnerLocked, PartnerAccepted = partnerAccepted
            };
            EventBus.Trigger<TradeOfferSnapshot>("OnTradeOfferUpdated", snapshot);
        }

        [TargetRpc]
        private void TargetTradeLockChanged(NetworkConnection conn, bool myLocked, bool myAccepted, bool partnerLocked, bool partnerAccepted) {
            var snapshot = new TradeLockSnapshot {
                MyLocked = myLocked, MyAccepted = myAccepted,
                PartnerLocked = partnerLocked, PartnerAccepted = partnerAccepted
            };
            EventBus.Trigger<TradeLockSnapshot>("OnTradeLockChanged", snapshot);
        }

        [TargetRpc]
        private void TargetTradeCountdownStarted(NetworkConnection conn, float duration) {
            EventBus.Trigger<float>("OnTradeCountdownStarted", duration);
        }

        [TargetRpc]
        private void TargetTradeCompleted(NetworkConnection conn) {
            EventBus.Trigger("OnTradeCompleted");
        }

        [TargetRpc]
        private void TargetTradeCancelled(NetworkConnection conn, string reason) {
            EventBus.Trigger<string>("OnTradeCancelled", reason);
        }

        [TargetRpc]
        private void TargetTradeError(NetworkConnection conn, string message) {
            EventBus.Trigger<string>("OnCombatError", message);
        }

        // ═══════════════════════════════════════════════════════
        // SERVER HELPERS
        // ═══════════════════════════════════════════════════════

        [Server]
        private void NotifyOfferUpdate(NetworkObject player) {
            if (_currentSession == null || player == null) return;

            var myOffer = _currentSession.GetOffer(player);
            var partnerOffer = _currentSession.GetPartnerOffer(player);
            if (myOffer == null || partnerOffer == null) return;

            SerializeOffer(myOffer, out int[] myIds, out int[] myQtys, out int[] myTiers, out int[] myRarities);
            SerializeOffer(partnerOffer, out int[] pIds, out int[] pQtys, out int[] pTiers, out int[] pRarities);

            TargetTradeOfferUpdated(player.Owner,
                myIds, myQtys, myTiers, myRarities, myOffer.Gold, myOffer.IsLocked, myOffer.HasAccepted,
                pIds, pQtys, pTiers, pRarities, partnerOffer.Gold, partnerOffer.IsLocked, partnerOffer.HasAccepted);
        }

        [Server]
        private void NotifyLockChange(NetworkObject player) {
            if (_currentSession == null || player == null) return;

            var myOffer = _currentSession.GetOffer(player);
            var partnerOffer = _currentSession.GetPartnerOffer(player);
            if (myOffer == null || partnerOffer == null) return;

            TargetTradeLockChanged(player.Owner, myOffer.IsLocked, myOffer.HasAccepted, partnerOffer.IsLocked, partnerOffer.HasAccepted);
        }

        private void SerializeOffer(TradeOffer offer, out int[] ids, out int[] qtys, out int[] tiers, out int[] rarities) {
            ids = new int[TradeConstants.MAX_TRADE_SLOTS];
            qtys = new int[TradeConstants.MAX_TRADE_SLOTS];
            tiers = new int[TradeConstants.MAX_TRADE_SLOTS];
            rarities = new int[TradeConstants.MAX_TRADE_SLOTS];

            for (int i = 0; i < TradeConstants.MAX_TRADE_SLOTS; i++) {
                ids[i] = offer.Items[i].ItemID;
                qtys[i] = offer.Items[i].Quantity;
                tiers[i] = (int)offer.Items[i].Tier;
                rarities[i] = (int)offer.Items[i].Rarity;
            }
        }

        [Server]
        private void ExecuteTrade() {
            if (_currentSession == null) return;

            var playerA = _currentSession.PlayerA;
            var playerB = _currentSession.PlayerB;
            if (playerA == null || playerB == null) {
                CancelTradeInternal("Player unavailable");
                return;
            }

            var invA = playerA.GetComponent<PlayerInventory>();
            var invB = playerB.GetComponent<PlayerInventory>();
            var attrA = playerA.GetComponent<PlayerAttributes>();
            var attrB = playerB.GetComponent<PlayerAttributes>();

            if (invA == null || invB == null || attrA == null || attrB == null) {
                CancelTradeInternal("Trade error");
                return;
            }

            var offerA = _currentSession.OfferA;
            var offerB = _currentSession.OfferB;

            // ── STEP 1: VALIDATE ──
            // Check items still exist at source slots
            for (int i = 0; i < TradeConstants.MAX_TRADE_SLOTS; i++) {
                if (!offerA.Items[i].IsEmpty) {
                    var currentSlot = invA.GetSlot(offerA.SourceSlots[i]);
                    if (currentSlot.ItemID != offerA.Items[i].ItemID || currentSlot.Quantity < offerA.Items[i].Quantity) {
                        CancelTradeInternal("Items changed during trade");
                        return;
                    }
                }
                if (!offerB.Items[i].IsEmpty) {
                    var currentSlot = invB.GetSlot(offerB.SourceSlots[i]);
                    if (currentSlot.ItemID != offerB.Items[i].ItemID || currentSlot.Quantity < offerB.Items[i].Quantity) {
                        CancelTradeInternal("Items changed during trade");
                        return;
                    }
                }
            }

            // Check gold
            if (offerA.Gold > 0 && attrA.Gold < offerA.Gold) {
                CancelTradeInternal("Insufficient gold");
                return;
            }
            if (offerB.Gold > 0 && attrB.Gold < offerB.Gold) {
                CancelTradeInternal("Insufficient gold");
                return;
            }

            // Check inventory space: net slots needed
            int slotsAGiving = offerA.UsedSlotCount();
            int slotsAReceiving = offerB.UsedSlotCount();
            int slotsBGiving = offerB.UsedSlotCount();
            int slotsBReceiving = offerA.UsedSlotCount();

            int emptyA = CountEmptySlots(invA);
            int emptyB = CountEmptySlots(invB);

            // After removing items, player gains empty slots, then needs to fit received items
            int netSlotsNeededA = slotsAReceiving - slotsAGiving;
            int netSlotsNeededB = slotsBReceiving - slotsBGiving;

            if (netSlotsNeededA > 0 && emptyA < netSlotsNeededA) {
                NotifyTradeError(playerA, "Not enough inventory space.");
                NotifyTradeError(playerB, "Partner doesn't have enough space.");
                CancelTradeInternal("Inventory full");
                return;
            }
            if (netSlotsNeededB > 0 && emptyB < netSlotsNeededB) {
                NotifyTradeError(playerA, "Partner doesn't have enough space.");
                NotifyTradeError(playerB, "Not enough inventory space.");
                CancelTradeInternal("Inventory full");
                return;
            }

            // ── STEP 2: EXECUTE SWAP ──
            // Remove items from A
            for (int i = TradeConstants.MAX_TRADE_SLOTS - 1; i >= 0; i--) {
                if (!offerA.Items[i].IsEmpty) {
                    invA.RemoveItem(offerA.SourceSlots[i], offerA.Items[i].Quantity);
                }
            }
            // Remove items from B
            for (int i = TradeConstants.MAX_TRADE_SLOTS - 1; i >= 0; i--) {
                if (!offerB.Items[i].IsEmpty) {
                    invB.RemoveItem(offerB.SourceSlots[i], offerB.Items[i].Quantity);
                }
            }

            // Add A's items to B
            for (int i = 0; i < TradeConstants.MAX_TRADE_SLOTS; i++) {
                if (!offerA.Items[i].IsEmpty) {
                    invB.AddItem(offerA.Items[i].ItemID, offerA.Items[i].Quantity, offerA.Items[i].Tier, offerA.Items[i].Rarity);
                }
            }
            // Add B's items to A
            for (int i = 0; i < TradeConstants.MAX_TRADE_SLOTS; i++) {
                if (!offerB.Items[i].IsEmpty) {
                    invA.AddItem(offerB.Items[i].ItemID, offerB.Items[i].Quantity, offerB.Items[i].Tier, offerB.Items[i].Rarity);
                }
            }

            // Gold transfer
            if (offerA.Gold > 0) {
                attrA.SpendGold(offerA.Gold);
                attrB.GainGold(offerA.Gold);
            }
            if (offerB.Gold > 0) {
                attrB.SpendGold(offerB.Gold);
                attrA.GainGold(offerB.Gold);
            }

            // ── STEP 3: COMPLETE ──
            _currentSession.State = TradeState.Completed;

            TargetTradeCompleted(playerA.Owner);
            TargetTradeCompleted(playerB.Owner);

            CleanupSession();
            Debug.Log($"[TradeManager] Trade completed successfully!");
        }

        [Server]
        private void CancelTradeInternal(string reason) {
            if (_currentSession == null) return;

            var session = _currentSession;
            session.State = TradeState.Cancelled;

            if (session.PlayerA != null && session.PlayerA.Owner != null) {
                TargetTradeCancelled(session.PlayerA.Owner, reason);
            }
            if (session.PlayerB != null && session.PlayerB.Owner != null) {
                TargetTradeCancelled(session.PlayerB.Owner, reason);
            }

            CleanupSession();
            Debug.Log($"[TradeManager] Trade cancelled: {reason}");
        }

        [Server]
        private void NotifyTradeError(NetworkObject player, string message) {
            if (player != null && player.Owner != null) {
                TargetTradeError(player.Owner, message);
            }
        }

        private void CleanupSession() {
            if (_currentSession == null) return;

            var session = _currentSession;
            TradeSession.Unregister(session);

            // Clear reference on both managers
            if (session.PlayerA != null) {
                var tmA = session.PlayerA.GetComponent<TradeManager>();
                if (tmA != null) tmA._currentSession = null;
            }
            if (session.PlayerB != null) {
                var tmB = session.PlayerB.GetComponent<TradeManager>();
                if (tmB != null) tmB._currentSession = null;
            }
        }

        private int CountEmptySlots(PlayerInventory inv) {
            int count = 0;
            var slots = inv.InventorySlots;
            for (int i = 0; i < slots.Count; i++) {
                if (slots[i].IsEmpty) count++;
            }
            return count;
        }

        private string GetPlayerName(NetworkObject player = null) {
            if (player == null) player = base.NetworkObject;
            var classManager = player.GetComponent<PlayerClassManager>();
            if (classManager != null && !string.IsNullOrEmpty(classManager.PlayerName)) {
                return classManager.PlayerName;
            }
            return "Player";
        }
    }
}
