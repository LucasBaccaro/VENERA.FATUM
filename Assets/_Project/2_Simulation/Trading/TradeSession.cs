using System.Collections.Generic;
using FishNet.Object;
using Genesis.Items;

namespace Genesis.Simulation {

    public struct TradeOfferSnapshot {
        public int[] MyItemIds, MyItemQtys, MyItemTiers, MyItemRarities;
        public int MyGold;
        public bool MyLocked, MyAccepted;
        public int[] PartnerItemIds, PartnerItemQtys, PartnerItemTiers, PartnerItemRarities;
        public int PartnerGold;
        public bool PartnerLocked, PartnerAccepted;
    }

    public struct TradeLockSnapshot {
        public bool MyLocked, MyAccepted, PartnerLocked, PartnerAccepted;
    }

    public enum TradeState {
        None,
        Requested,
        Active,
        Countdown,
        Completed,
        Cancelled
    }

    public static class TradeConstants {
        public const int MAX_TRADE_SLOTS = 6;
        public const float TRADE_RANGE = 10f;
        public const float COMBAT_COOLDOWN = 5f;
        public const float COUNTDOWN_DURATION = 4f;
    }

    public class TradeOffer {
        public ItemSlot[] Items = new ItemSlot[TradeConstants.MAX_TRADE_SLOTS];
        public int[] SourceSlots = new int[TradeConstants.MAX_TRADE_SLOTS];
        public int Gold;
        public bool IsLocked;
        public bool HasAccepted;

        public TradeOffer() {
            for (int i = 0; i < TradeConstants.MAX_TRADE_SLOTS; i++) {
                Items[i] = ItemSlot.Empty;
                SourceSlots[i] = -1;
            }
        }

        public int UsedSlotCount() {
            int count = 0;
            for (int i = 0; i < Items.Length; i++) {
                if (!Items[i].IsEmpty) count++;
            }
            return count;
        }

        public void Reset() {
            for (int i = 0; i < TradeConstants.MAX_TRADE_SLOTS; i++) {
                Items[i] = ItemSlot.Empty;
                SourceSlots[i] = -1;
            }
            Gold = 0;
            IsLocked = false;
            HasAccepted = false;
        }
    }

    public class TradeSession {
        private static uint _nextId = 1;

        public uint SessionId;
        public TradeState State;
        public NetworkObject PlayerA;
        public NetworkObject PlayerB;
        public TradeOffer OfferA = new TradeOffer();
        public TradeOffer OfferB = new TradeOffer();
        public float CountdownTimer;

        public TradeSession(NetworkObject playerA, NetworkObject playerB) {
            SessionId = _nextId++;
            State = TradeState.Requested;
            PlayerA = playerA;
            PlayerB = playerB;
        }

        public TradeOffer GetOffer(NetworkObject player) {
            if (player == PlayerA) return OfferA;
            if (player == PlayerB) return OfferB;
            return null;
        }

        public TradeOffer GetPartnerOffer(NetworkObject player) {
            if (player == PlayerA) return OfferB;
            if (player == PlayerB) return OfferA;
            return null;
        }

        public NetworkObject GetPartner(NetworkObject player) {
            if (player == PlayerA) return PlayerB;
            if (player == PlayerB) return PlayerA;
            return null;
        }

        public bool IsParticipant(NetworkObject player) {
            return player == PlayerA || player == PlayerB;
        }

        public void ResetLocksAndAccepts() {
            OfferA.IsLocked = false;
            OfferA.HasAccepted = false;
            OfferB.IsLocked = false;
            OfferB.HasAccepted = false;
        }

        // Active sessions indexed by player ObjectId
        public static Dictionary<int, TradeSession> ActiveSessions = new Dictionary<int, TradeSession>();

        public static TradeSession FindByPlayer(int objectId) {
            ActiveSessions.TryGetValue(objectId, out TradeSession session);
            return session;
        }

        public static void Register(TradeSession session) {
            ActiveSessions[session.PlayerA.ObjectId] = session;
            ActiveSessions[session.PlayerB.ObjectId] = session;
        }

        public static void Unregister(TradeSession session) {
            ActiveSessions.Remove(session.PlayerA.ObjectId);
            ActiveSessions.Remove(session.PlayerB.ObjectId);
        }
    }
}
