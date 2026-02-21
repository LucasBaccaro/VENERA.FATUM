using System.Threading.Tasks;
using FishNet.Object;

namespace Genesis.Core.Persistence
{
    /// <summary>
    /// Service interface for persistence operations (implemented by NakamaManager).
    /// Registered in ServiceLocator for cross-layer access.
    /// </summary>
    public interface IPersistenceService
    {
        Task<CharacterData> LoadAsync(string userId);
        Task SaveAsync(string userId, CharacterData data);
        void MarkDirty(NetworkObject playerObj);
        void RegisterPlayer(int clientId, NetworkObject playerObj);
        void UnregisterPlayer(int clientId);
    }
}
