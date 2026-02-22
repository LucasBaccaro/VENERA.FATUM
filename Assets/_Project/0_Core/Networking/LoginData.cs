namespace Genesis.Core.Networking {

    /// <summary>
    /// Static DTO that carries login choices (name + class) from the Login UI
    /// to post-spawn systems. Survives scene loads naturally (no MonoBehaviour).
    /// </summary>
    public static class LoginData {
        public static string PlayerName { get; set; } = "";
        public static int ClassIndex { get; set; } = 0;
        public static int FactionIndex { get; set; } = 0;
        public static bool IsSet { get; set; } = false;

        /// <summary>
        /// Set by LoginController.Awake() so NetworkBootstrap knows to wait,
        /// even if the inspector bool wasn't saved to the scene.
        /// </summary>
        public static bool LoginRequired { get; set; } = false;

        public static void Clear() {
            PlayerName = "";
            ClassIndex = 0;
            FactionIndex = 0;
            IsSet = false;
        }
    }
}
