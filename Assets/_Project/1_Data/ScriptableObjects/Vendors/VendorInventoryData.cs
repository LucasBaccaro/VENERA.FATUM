using UnityEngine;
using Genesis.Items;

namespace Genesis.Data {

    [System.Serializable]
    public struct VendorItem {
        public int ItemID;
        public int Price;
        public ItemTier Tier;
        public ItemRarity Rarity;
    }

    [CreateAssetMenu(menuName = "VENERA.FATUM/Vendors/Vendor Inventory")]
    public class VendorInventoryData : ScriptableObject {
        public VendorItem[] Items;
    }
}
