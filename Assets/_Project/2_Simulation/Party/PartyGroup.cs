using System.Collections.Generic;

namespace Genesis.Simulation {

    public enum LootMode { Free, MasterLoot }

    public class PartyGroup {
        public int PartyId;
        public int LeaderClientId;
        public List<int> MemberClientIds = new List<int>();
        public LootMode LootMode;

        public bool HasMember(int clientId) => MemberClientIds.Contains(clientId);
        public bool IsFull => MemberClientIds.Count >= 4;
    }
}
