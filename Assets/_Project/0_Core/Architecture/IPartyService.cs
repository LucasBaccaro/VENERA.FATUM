namespace Genesis.Core {

    /// <summary>
    /// Interface for party service - defined in Core so PlayerSpawnManager (Core assembly)
    /// can call it without depending on Genesis.Simulation assembly.
    /// </summary>
    public interface IPartyService {
        void OnPlayerDisconnected(int clientId);
    }
}
