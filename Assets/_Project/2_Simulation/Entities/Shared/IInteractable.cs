using FishNet.Object;

namespace Genesis.Simulation {

    /// <summary>
    /// Interface para objetos con los que el jugador puede interactuar.
    /// Ej: Cofres, NPCs, puertas, botones, etc.
    /// </summary>
    public interface IInteractable {

        /// <summary>
        /// Llamado cuando un jugador intenta interactuar (presiona E)
        /// </summary>
        /// <param name="player">NetworkObject del jugador que interactúa</param>
        void Interact(NetworkObject player);

        /// <summary>
        /// Retorna si actualmente se puede interactuar con este objeto
        /// </summary>
        bool CanInteract(NetworkObject player);

        /// <summary>
        /// Texto que se muestra en la UI cuando el jugador está cerca
        /// Ej: "Presiona E para abrir", "Hablar con NPC"
        /// </summary>
        string GetInteractionPrompt();
    }
}
