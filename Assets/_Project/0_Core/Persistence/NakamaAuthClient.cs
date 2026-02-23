using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace Genesis.Core.Persistence
{
    /// <summary>
    /// Static client-side auth helper. Used by LoginSceneController (no MonoBehaviour needed).
    /// Creates a lightweight Nakama client for login/register before Bootstrap loads.
    /// </summary>
    public static class NakamaAuthClient
    {
        private const string SCHEME = "http";
        private const string HOST = "127.0.0.1";
        private const int PORT = 7350;
        private const string SERVER_KEY = "defaultkey";

        private const string COLLECTION = "characters";
        private const string KEY = "main";

        private static IClient _client;

        private static IClient GetClient()
        {
            _client ??= new Client(SCHEME, HOST, PORT, SERVER_KEY);
            return _client;
        }

        /// <summary>
        /// Login with email/password. Returns session data or throws on failure.
        /// Uses username@venera.fatum as email format.
        /// </summary>
        public static async Task<AuthResult> LoginAsync(string username, string password)
        {
            var client = GetClient();
            string email = $"{username.ToLowerInvariant().Trim()}@venera.fatum";

            try
            {
                var session = await client.AuthenticateEmailAsync(email, password, username, false);
                Debug.Log($"[NakamaAuthClient] Login OK: userId={session.UserId}");

                var characterData = await LoadCharacterAsync(session);

                return new AuthResult
                {
                    Success = true,
                    UserId = session.UserId,
                    AuthToken = session.AuthToken,
                    RefreshToken = session.RefreshToken,
                    Character = characterData
                };
            }
            catch (ApiResponseException e)
            {
                Debug.LogWarning($"[NakamaAuthClient] Login failed: {e.Message}");
                return new AuthResult { Success = false, Error = ParseError(e) };
            }
            catch (Exception e)
            {
                Debug.LogError($"[NakamaAuthClient] Login exception: {e.Message}");
                return new AuthResult { Success = false, Error = "Server unreachable. Is Nakama running?" };
            }
        }

        /// <summary>
        /// Register a new account. Creates the email identity in Nakama.
        /// </summary>
        public static async Task<AuthResult> RegisterAsync(string username, string password)
        {
            var client = GetClient();
            string email = $"{username.ToLowerInvariant().Trim()}@venera.fatum";

            try
            {
                var session = await client.AuthenticateEmailAsync(email, password, username, true);
                Debug.Log($"[NakamaAuthClient] Register OK: userId={session.UserId}");

                return new AuthResult
                {
                    Success = true,
                    UserId = session.UserId,
                    AuthToken = session.AuthToken,
                    RefreshToken = session.RefreshToken,
                    Character = null // new account, no character yet
                };
            }
            catch (ApiResponseException e)
            {
                Debug.LogWarning($"[NakamaAuthClient] Register failed: {e.Message}");
                return new AuthResult { Success = false, Error = ParseError(e) };
            }
            catch (Exception e)
            {
                Debug.LogError($"[NakamaAuthClient] Register exception: {e.Message}");
                return new AuthResult { Success = false, Error = "Server unreachable. Is Nakama running?" };
            }
        }

        /// <summary>
        /// Load character data from Nakama storage using an existing session.
        /// </summary>
        public static async Task<CharacterData> LoadCharacterAsync(ISession session)
        {
            var client = GetClient();

            try
            {
                var readId = new StorageObjectId
                {
                    Collection = COLLECTION,
                    Key = KEY,
                    UserId = session.UserId
                };

                var result = await client.ReadStorageObjectsAsync(session, new[] { readId });

                foreach (var obj in result.Objects)
                {
                    var data = JsonUtility.FromJson<CharacterData>(obj.Value);
                    Debug.Log($"[NakamaAuthClient] Character loaded: {data.playerName} Lv{data.level}");
                    return data;
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NakamaAuthClient] LoadCharacter failed: {e.Message}");
                return null;
            }
        }

        private static string ParseError(ApiResponseException e)
        {
            string msg = e.Message.ToLowerInvariant();
            if (msg.Contains("not found") || msg.Contains("invalid") || msg.Contains("authenticate"))
                return "Invalid username or password.";
            if (msg.Contains("already") || msg.Contains("exists"))
                return "Account already exists.";
            return e.Message;
        }

        public struct AuthResult
        {
            public bool Success;
            public string UserId;
            public string AuthToken;
            public string RefreshToken;
            public CharacterData Character;
            public string Error;
        }
    }
}
