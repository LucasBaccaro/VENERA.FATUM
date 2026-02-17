using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Data.Dungeon
{
    [CreateAssetMenu(fileName = "NewDungeonTheme", menuName = "Genesis/Dungeon/Theme")]
    public class DungeonTheme : ScriptableObject
    {
        public string ThemeName;
        public List<ModuleCategory> Categories = new List<ModuleCategory>();

        public GameObject GetRandomVariant(ModuleType type)
        {
            var category = Categories.Find(c => c.Type == type);
            if (category != null && category.Variants != null && category.Variants.Length > 0)
            {
                return category.Variants[Random.Range(0, category.Variants.Length)];
            }
            return null;
        }
        
        public GameObject GetVariant(ModuleType type, int index)
        {
            var category = Categories.Find(c => c.Type == type);
            if (category != null && category.Variants != null && category.Variants.Length > 0)
            {
                return category.Variants[Mathf.Clamp(index, 0, category.Variants.Length - 1)];
            }
            return null;
        }

        public int GetVariantCount(ModuleType type)
        {
            var category = Categories.Find(c => c.Type == type);
            return category != null && category.Variants != null ? category.Variants.Length : 0;
        }
    }
}
