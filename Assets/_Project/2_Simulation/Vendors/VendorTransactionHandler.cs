using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using Genesis.Core;
using Genesis.Data;
using Genesis.Items;

namespace Genesis.Simulation {

    /// <summary>
    /// Server-side handler for vendor buy transactions.
    /// Attach to a persistent manager GameObject in the Bootstrap scene.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class VendorTransactionHandler : NetworkBehaviour {

        [ServerRpc(RequireOwnership = false)]
        public void CmdBuyItem(string npcId, int vendorItemIndex, NetworkConnection sender = null) {
            if (sender == null || sender.FirstObject == null) return;

            NetworkObject playerNob = sender.FirstObject;
            PlayerAttributes attrs = playerNob.GetComponent<PlayerAttributes>();
            PlayerInventory inventory = playerNob.GetComponent<PlayerInventory>();

            if (attrs == null || inventory == null) {
                TargetBuyResult(sender, false, "Player components missing.");
                return;
            }

            // Find the NPC to validate the transaction
            NpcController npc = FindVendorNpc(npcId);
            if (npc == null || npc.NpcData == null || !npc.NpcData.IsVendor || npc.NpcData.VendorInventory == null) {
                TargetBuyResult(sender, false, "Vendor not found.");
                return;
            }

            VendorInventoryData vendorData = npc.NpcData.VendorInventory;
            if (vendorItemIndex < 0 || vendorItemIndex >= vendorData.Items.Length) {
                TargetBuyResult(sender, false, "Invalid item.");
                return;
            }

            VendorItem vendorItem = vendorData.Items[vendorItemIndex];

            // Check gold
            if (attrs.Gold < vendorItem.Price) {
                TargetBuyResult(sender, false, "Not enough gold.");
                return;
            }

            // Check inventory space
            if (!inventory.HasSpace(vendorItem.ItemID, 1)) {
                TargetBuyResult(sender, false, "Inventory full.");
                return;
            }

            // Execute transaction
            if (!attrs.SpendGold(vendorItem.Price)) {
                TargetBuyResult(sender, false, "Transaction failed.");
                return;
            }

            bool added = inventory.AddItem(vendorItem.ItemID, 1, vendorItem.Tier, vendorItem.Rarity);
            if (!added) {
                // Refund gold
                attrs.GainGold(vendorItem.Price);
                TargetBuyResult(sender, false, "Failed to add item.");
                return;
            }

            BaseItemData itemData = ItemDatabase.Instance.GetItem(vendorItem.ItemID);
            string itemName = itemData != null ? itemData.ItemName : $"Item {vendorItem.ItemID}";
            Debug.Log($"[VendorTransaction] {playerNob.name} bought {itemName} for {vendorItem.Price} gold");

            TargetBuyResult(sender, true, $"Purchased {itemName}!");
        }

        [TargetRpc]
        private void TargetBuyResult(NetworkConnection conn, bool success, string message) {
            if (!success) {
                EventBus.Trigger("OnCombatError", message);
            }
            EventBus.Trigger("OnVendorBuyResult", success, message);
        }

        private NpcController FindVendorNpc(string npcId) {
            var npcs = FindObjectsByType<NpcController>(FindObjectsSortMode.None);
            foreach (var npc in npcs) {
                if (npc.NpcID == npcId) return npc;
            }
            return null;
        }
    }
}
