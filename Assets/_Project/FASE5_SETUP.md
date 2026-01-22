# FASE 5 - COMBAT CORE - SETUP

Esta fase implementa la ejecución real de habilidades. El objetivo final es que el Mago lance una Fireball y dañe a un Dummy.

## 🛠️ 1. IMPLEMENTACIÓN DE SCRIPTS (Automático)

Implementaremos los siguientes sistemas:

1.  **ProjectileController.cs**: Física y colisión de proyectiles (Server-Side).
2.  **ProjectileLogic.cs**: La lógica que decide "Spawnear un proyectil".
3.  **PlayerCombat.cs**: El controlador del jugador que gestiona cooldowns y input.
4.  **IDamageable.cs**: Interface unificada para todo lo que puede morir.

## 📦 2. PREFABS REQUERIDOS (Manual)

### A. Prefab: Fireball
Necesitamos el objeto físico que viaja.

1.  Crea una **Esfera** (`Scale: 0.3`).
2.  Quítale el `SphereCollider` (usaremos SphereCast manual, o déjalo como Trigger).
3.  Añade componente: **NetworkObject**.
4.  Añade componente: **ProjectileController** (script).
5.  Añade efectos visuales (Trail Renderer, partículas de fuego).
6.  Guárdalo en `Assets/_Project/5_Content/Prefabs/Projectiles/Fireball.prefab`.
7.  **IMPORTANTE:** Registra este prefab en la lista `DefaultPrefabs` de FishNet (NetworkManager).

### B. Logic Asset: Fireball Logic
Necesitamos el "cerebro" de la habilidad.

1.  Ve a `Assets/_Project/1_Data/Abilities/Logic/` (créala).
2.  Click derecho > `Create > Genesis > Abilities > Projectile Logic`.
3.  Llámalo `Logic_Fireball`.
4.  (No requiere config extra por ahora, usa los datos del AbilityData).

### C. Vincular en AbilityData
1.  Busca `Ability_Fireball` (creado en Fase 4).
2.  Arrastra `Logic_Fireball` al campo **Logic**.
3.  Arrastra el prefab `Fireball` al campo **Projectile Prefab**.

## 🎮 3. CONFIGURAR PLAYER

1.  Abre prefab **Player**.
2.  Añade componente: **PlayerCombat**.
3.  Arrastra referencias:
    *   **Stats**: PlayerStats.
    *   **Targeting**: TargetingSystem.
4.  En la lista **Ability Slots** (Input 1-6), añade tus habilidades:
    *   Element 0: `Ability_Fireball`.
    *   Element 1: `Ability_Heal`.

## 🧪 4. TEST FINAL

1.  Play (Host).
2.  Targetea al DummyEnemy.
3.  Presiona **1**.
4.  Deberías ver:
    *   Animación de cast (si tienes).
    *   Barra de cast (si implementamos UI).
    *   Proyectil saliendo y golpeando al Dummy.
    *   Vida del Dummy bajando (mira el Inspector del Dummy).

---

## ✅ CRITERIOS DE ÉXITO

*   [ ] Input "1" inicia el proceso.
*   [ ] Servidor valida maná y distancia.
*   [ ] Proyectil spawnea en red.
*   [ ] Proyectil impacta y aplica daño.
