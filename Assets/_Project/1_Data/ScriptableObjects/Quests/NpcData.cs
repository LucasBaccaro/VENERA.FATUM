using UnityEngine;

namespace Genesis.Data {

    [CreateAssetMenu(fileName = "NewNpc", menuName = "VENERA.FATUM/Quests/NPC Data", order = 1)]
    public class NpcData : ScriptableObject {
        public string NpcID;
        public string DisplayName;
        [TextArea] public string IdleDialogue;
    }
}
