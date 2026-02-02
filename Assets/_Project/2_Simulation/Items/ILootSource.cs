using System.Collections.Generic;
using FishNet.Object;
using Genesis.Items;

namespace Genesis.Simulation {
    /// <summary>
    /// Interface for any object that acts as a container for loot (LootBag, Chest, etc.)
    /// </summary>
    public interface ILootSource {
        /// <summary>
        /// Display name of the loot source (e.g., "Player's Bag", "Golden Chest")
        /// </summary>
        string LootName { get; }

        /// <summary>
        /// List of items currently in the source
        /// </summary>
        IReadOnlyList<ItemSlot> LootItems { get; }
        
        /// <summary>
        /// True if the source is interactable/viewable
        /// </summary>
        bool CanLoot(NetworkObject player);

        #region Client Requests (to be called via RPCs internally)
        
        /// <summary>
        /// Request to take a specific item
        /// </summary>
        void CmdTakeItem(int index, NetworkObject player);

        /// <summary>
        /// Request to take all items
        /// </summary>
        void CmdTakeAll(NetworkObject player);
        
        /// <summary>
        /// Request to interact/open (e.g. Chest opening animation)
        /// </summary>
        void CmdTryInteract(NetworkObject player);
        
        #endregion
    }
}
