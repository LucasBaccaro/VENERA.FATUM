namespace Genesis.Core.Networking {

    /// <summary>
    /// Static DTO that carries login/auth data from the Login scene
    /// to post-spawn systems. Survives scene loads naturally (no MonoBehaviour).
    /// </summary>
    public static class LoginData {
        // Character creation
        public static string PlayerName { get; set; } = "";
        public static int ClassIndex { get; set; } = 0;
        public static int FactionIndex { get; set; } = 0;
        public static bool IsSet { get; set; } = false;

        // Auth tokens (set by LoginSceneController after Nakama auth)
        public static string Username { get; set; } = "";
        public static string AuthToken { get; set; } = "";
        public static string RefreshToken { get; set; } = "";
        public static string UserId { get; set; } = "";
        public static bool IsNewCharacter { get; set; } = false;

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
            Username = "";
            AuthToken = "";
            RefreshToken = "";
            UserId = "";
            IsNewCharacter = false;
        }
    }
}
