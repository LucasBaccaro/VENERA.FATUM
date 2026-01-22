using UnityEngine;
using FishNet.Object;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Interface para cualquier entidad que puede recibir daño.
    /// Implementada por jugadores, NPCs, estructuras destructibles, etc.
    /// </summary>
    public interface IDamageable {

        /// <summary>
        /// Aplica daño a esta entidad.
        /// IMPORTANTE: Este método debe ser llamado SOLO en el servidor.
        /// </summary>
        /// <param name="damage">Cantidad de daño a aplicar</param>
        /// <param name="attacker">NetworkObject del atacante (puede ser null para daño ambiental)</param>
        void TakeDamage(float damage, NetworkObject attacker);

        /// <summary>
        /// Retorna si la entidad está viva
        /// </summary>
        bool IsAlive();

        /// <summary>
        /// Retorna la vida actual
        /// </summary>
        float GetCurrentHealth();

        /// <summary>
        /// Retorna la vida máxima
        /// </summary>
        float GetMaxHealth();
    }
}
