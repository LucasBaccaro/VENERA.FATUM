using System;
using UnityEngine;

namespace Genesis.Data.Dungeon
{
    [Serializable]
    public class ModuleCategory
    {
        public ModuleType Type;
        public bool CanRotate180; // Valid for 180 degree rotation
        public GameObject[] Variants;
    }
}
