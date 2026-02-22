using Genesis.Data;

namespace Genesis.Presentation.Audio {

    public static class UISoundPlayer {
        public static void PlayClick() => AudioManager.Instance?.PlaySFX(SoundType.UI_Click);
        public static void PlayOpen() => AudioManager.Instance?.PlaySFX(SoundType.UI_Open);
        public static void PlayClose() => AudioManager.Instance?.PlaySFX(SoundType.UI_Close);
        public static void PlayError() => AudioManager.Instance?.PlaySFX(SoundType.UI_Error);
        public static void PlaySuccess() => AudioManager.Instance?.PlaySFX(SoundType.UI_Success);
        public static void PlayLootPickup() => AudioManager.Instance?.PlaySFX(SoundType.Loot_Pickup);
    }
}
