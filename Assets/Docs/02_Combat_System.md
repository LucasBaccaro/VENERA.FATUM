## 4. SISTEMA DE COMBATE

### 4.1 Arquitectura General

**Flujo de Combate (Secuencia Completa):**
```text
[CLIENTE]                    [SERVIDOR]                  [TODOS LOS CLIENTES]
    │                            │                              │
    │ 1. Presiona "1"            │                              │
    │ (Selecciona habilidad)     │                              │
    │                            │                              │
    │ 2. Click en enemigo        │                              │
    │ (TargetingSystem)          │                              │
    │                            │                              │
    │ 3. Validación local        │                              │
    │    - ¿En rango?            │                              │
    │    - ¿Tengo maná?          │                              │
    │    - ¿Cooldown OK?         │                              │
    │                            │                              │
    │ 4. Client Prediction       │                              │
    │    - Anima "CastStart"     │                              │
    │    - Muestra barra cast    │                              │
    │    - Descuenta maná UI     │                              │
    │                            │                              │
    │ 5. [ServerRpc]             │                              │
    │    CmdCastAbility(10, ID)─────────>                      │
    │                            │                              │
    │                            │ 6. Validación Server         │
    │                            │    - ¿Distancia real OK?     │
    │                            │    - ¿Tiene recursos?        │
    │                            │    - ¿LineOfSight?           │
    │                            │                              │
    │                            │ 7. Si FAIL:                  │
    │<──────[ObserversRpc]───────│    - Reversa predicción     │
    │    OnAbilityFailed()       │                              │
    │                            │                              │
    │                            │ 8. Si OK:                    │
    │                            │    - Descuenta maná real     │
    │                            │    - Inicia GCD              │
    │                            │    - Execute Ability         │
    │                            │                              │
    │                            │ 9. Spawn Projectile          │
    │                            │    (Pool)                    │
    │                            │                              │
    │                            │ 10. [ObserversRpc]           │
    │<────────────────────────────────────────────────────────>│
    │              OnProjectileSpawned(pos, dir)               │
    │                            │                              │
    │ 11. VFX local              │                        11. VFX remoto
    │     (Muzzle flash)         │                              │
```

### 4.2 Definición de Datos: AbilityData.cs

```csharp
using UnityEngine;

namespace Genesis.Data {
    
[CreateAssetMenu(fileName = "Ability_", menuName = "Genesis/Combat/Ability")]
public class AbilityData : ScriptableObject {
    
    [Header("═══ CORE ═══")]
    public int ID;
    public string Name;
    public Sprite Icon;
    
    [Header("═══ REQUIREMENTS ═══")]
    [Tooltip("Global Cooldown - tiempo mínimo entre habilidades")]
    public float GCD = 1.2f;
    
    [Tooltip("Cooldown específico de esta habilidad")]
    public float SpecificCooldown;
    
    [Tooltip("Coste de maná")]
    public float ManaCost;
    
    [Header("═══ CASTING ═══")]
    public CastingType CastType;
    
    [Tooltip("Tiempo de casteo (0 = instant)")]
    public float CastTime;
    
    [Tooltip("¿Se puede castear mientras te mueves?")]
    public bool CanMoveWhileCasting;
    
    [Header("═══ TARGETING ═══")]
    public TargetType TargetingMode;
    
    [Tooltip("Rango máximo (metros)")]
    public float MaxRange;
    
    [Tooltip("Radio AoE (0 = single target)")]
    public float AoERadius;
    
    [Header("═══ EFFECTS ═══")]
    public AbilityType Type;
    public float BaseDamage;
    public float BaseHealing;
    
    [Tooltip("Status effects aplicados al target")]
    public StatusEffectData[] ApplyToTarget;
    
    [Tooltip("Status effects aplicados al caster")]
    public StatusEffectData[] ApplyToSelf;
    
    [Header("═══ VISUALS ═══")]
    public GameObject ProjectilePrefab;
    public GameObject ImpactVFXPrefab;
    public AudioClip CastSound;
    public AudioClip ImpactSound;
    
    [Header("═══ PROJECTILE SETTINGS ═══")]
    [Tooltip("Velocidad del proyectil (m/s) - 0 = hitscan")]
    public float ProjectileSpeed = 15f;
    
    [Tooltip("Radio de colisión del proyectil")]
    public float ProjectileRadius = 0.2f;
}

// ═══════════════════════════════════════════════════════
// ENUMS
// ═══════════════════════════════════════════════════════

public enum CastingType {
    Instant,        // Se ejecuta inmediatamente
    Casting,        // Requiere tiempo de casteo (barra)
    Channeling,     // Efecto continuo mientras se mantiene
    Movement        // Permite movimiento durante cast
}

public enum TargetType {
    None,           // No requiere target (ej: Buff self)
    Enemy,          // Requiere enemigo seleccionado
    Ally,           // Requiere aliado seleccionado
    Ground,         // Click en suelo (AoE posicional)
    EnemyOrGround   // Híbrido: Meteoro puede ir a enemigo O suelo
}

public enum AbilityType {
    Damage,
    Heal,
    Buff,
    Debuff
}

} // namespace
```

### 4.3 Sistema de Execution: Strategy Pattern

**Base Abstract:**
```csharp
// Assets/_Project/2_Simulation/Combat/Abilities/AbilityLogic.cs

using UnityEngine;
using FishNet.Object;

namespace Genesis.Simulation.Combat {

public abstract class AbilityLogic : ScriptableObject {
    
    /// <summary>
    /// Ejecutado SOLO en el servidor. Implementa la lógica de la habilidad.
    /// </summary>
    /// <param name="caster">Quien lanza la habilidad</param>
    /// <param name="target">Target seleccionado (puede ser null si es ground)</param>
    /// <param name="groundPoint">Posición de ground targeting</param>
    public abstract void Execute(
        NetworkObject caster, 
        NetworkObject target, 
        Vector3 groundPoint,
        AbilityData data
    );
    
    /// <summary>
    /// Validación adicional específica de la habilidad (server-side)
    /// </summary>
    public virtual bool Validate(NetworkObject caster, NetworkObject target, Vector3 point) {
        return true;
    }
}

} // namespace
```

**Implementación: Proyectil**
```csharp
// Assets/_Project/2_Simulation/Combat/Abilities/ProjectileAbility.cs

using UnityEngine;
using FishNet.Object;
using Genesis.Core;

namespace Genesis.Simulation.Combat {

[CreateAssetMenu(fileName = "Logic_Projectile", menuName = "Genesis/Abilities/Projectile Logic")]
public class ProjectileAbility : AbilityLogic {
    
    public override void Execute(
        NetworkObject caster, 
        NetworkObject target, 
        Vector3 groundPoint,
        AbilityData data
    ) {
        // Punto de spawn (mano del caster)
        Transform spawnPoint = caster.transform.Find("Hand_R");
        if (spawnPoint == null) spawnPoint = caster.transform;
        
        Vector3 spawnPos = spawnPoint.position;
        
        // Dirección: hacia el target o hacia groundPoint
        Vector3 direction;
        if (target != null) {
            // Apuntar al centro del target
            Bounds bounds = target.GetComponent<Collider>().bounds;
            direction = (bounds.center - spawnPos).normalized;
        } else {
            direction = (groundPoint - spawnPos).normalized;
        }
        
        // Obtener proyectil del pool
        ProjectileController projectile = ObjectPoolManager.Instance
            .GetPool<ProjectileController>(data.ProjectilePrefab.name)
            .Get();
        
        // Configurar
        projectile.transform.position = spawnPos;
        projectile.transform.rotation = Quaternion.LookRotation(direction);
        
        // Inicializar (ver sección 6 para detalles)
        projectile.Initialize(
            owner: caster,
            damage: data.BaseDamage,
            velocity: direction * data.ProjectileSpeed,
            radius: data.ProjectileRadius,
            effects: data.ApplyToTarget
        );
        
        // Spawn en red
        FishNet.InstanceFinder.ServerManager.Spawn(projectile.gameObject);
    }
}

} // namespace
```

**Implementación: Melee**
```csharp
[CreateAssetMenu(fileName = "Logic_Melee", menuName = "Genesis/Abilities/Melee Logic")]
public class MeleeAbility : AbilityLogic {
    
    public override void Execute(NetworkObject caster, NetworkObject target, Vector3 groundPoint, AbilityData data) {
        
        if (target == null) return; // Melee requiere target
        
        // Validar distancia (tolerancia de 0.5m por lag)
        float distance = Vector3.Distance(caster.transform.position, target.transform.position);
        if (distance > data.MaxRange + 0.5f) {
            Debug.LogWarning($"Target fuera de rango melee: {distance}m");
            return;
        }
        
        // Aplicar daño directo
        if (target.TryGetComponent(out IDamageable damageable)) {
            damageable.TakeDamage(data.BaseDamage, caster);
        }
        
        // VFX en el punto de impacto
        if (data.ImpactVFXPrefab != null) {
            GameObject vfx = Instantiate(data.ImpactVFXPrefab, target.transform.position, Quaternion.identity);
            FishNet.InstanceFinder.ServerManager.Spawn(vfx);
            Destroy(vfx, 2f); // Auto-cleanup
        }
    }
}
```

**Implementación: AoE**
```csharp
[CreateAssetMenu(fileName = "Logic_AoE", menuName = "Genesis/Abilities/AoE Logic")]
public class AoEAbility : AbilityLogic {
    
    public override void Execute(NetworkObject caster, NetworkObject target, Vector3 groundPoint, AbilityData data) {
        
        Vector3 center = target != null ? target.transform.position : groundPoint;
        
        // OverlapSphere para detectar entidades
        Collider[] hits = Physics.OverlapSphere(center, data.AoERadius, Layers.Damageable);
        
        foreach (var hit in hits) {
            NetworkObject netObj = hit.GetComponent<NetworkObject>();
            if (netObj == null) continue;
            
            // NO auto-daño
            if (netObj == caster) continue;
            
            // Aplicar daño
            if (hit.TryGetComponent(out IDamageable damageable)) {
                damageable.TakeDamage(data.BaseDamage, caster);
            }
            
            // Aplicar effects
            if (data.ApplyToTarget != null) {
                StatusEffectSystem statusSystem = hit.GetComponent<StatusEffectSystem>();
                foreach (var effect in data.ApplyToTarget) {
                    statusSystem?.ApplyEffect(effect);
                }
            }
        }
        
        // VFX en el centro
        if (data.ImpactVFXPrefab != null) {
            GameObject vfx = Instantiate(data.ImpactVFXPrefab, center, Quaternion.identity);
            FishNet.InstanceFinder.ServerManager.Spawn(vfx);
            Destroy(vfx, 3f);
        }
    }
}
```

### 4.4 PlayerCombat.cs (Core Controller)

```csharp
// Assets/_Project/2_Simulation/Entities/Player/PlayerCombat.cs

using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using Genesis.Data;
using System.Collections.Generic;

namespace Genesis.Simulation {

public class PlayerCombat : NetworkBehaviour {
    
    [Header("References")]
    [SerializeField] private PlayerStats stats;
    [SerializeField] private TargetingSystem targeting;
    
    [Header("Abilities")]
    [SerializeField] private AbilityData[] abilitySlots = new AbilityData[6];
    
    // State
    private Dictionary<int, float> _cooldowns = new Dictionary<int, float>();
    private float _gcdEndTime;
    private bool _isCasting;
    private float _castEndTime;
    private AbilityData _currentCast;
    
    // ═══════════════════════════════════════════════════════
    // CLIENT INPUT
    // ═══════════════════════════════════════════════════════
    
    void Update() {
        if (!base.IsOwner) return;
        
        // Input de habilidades (1-6)
        for (int i = 0; i < 6; i++) {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) {
                TryCastAbility(i);
            }
        }
        
        // Cancelar cast con movimiento (si no permite moverse)
        if (_isCasting && _currentCast != null && !_currentCast.CanMoveWhileCasting) {
            Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            if (input.magnitude > 0.1f) {
                CancelCast();
            }
        }
    }
    
    private void TryCastAbility(int slotIndex) {
        AbilityData ability = abilitySlots[slotIndex];
        if (ability == null) return;
        
        // ═══ VALIDACIONES LOCALES (Client-Side Prediction) ═══
        
        // 1. ¿Estoy casté algo?
        if (_isCasting) {
            ShowError("Ya estás casteando");
            return;
        }
        
        // 2. ¿GCD activo?
        if (Time.time < _gcdEndTime) {
            ShowError("Espera el Global Cooldown");
            return;
        }
        
        // 3. ¿Cooldown específico?
        if (_cooldowns.TryGetValue(ability.ID, out float cdEnd) && Time.time < cdEnd) {
            ShowError($"{ability.Name} en cooldown");
            return;
        }
        
        // 4. ¿Tengo maná?
        if (stats.CurrentMana < ability.ManaCost) {
            ShowError("Maná insuficiente");
            return;
        }
        
        // 5. ¿Requiere target?
        NetworkObject target = targeting.CurrentTarget;
        if (ability.TargetingMode == TargetType.Enemy && target == null) {
            ShowError("Selecciona un objetivo");
            return;
        }
        
        // 6. ¿Está en rango? (check local aproximado)
        if (target != null) {
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > ability.MaxRange) {
                ShowError("Objetivo fuera de rango");
                return;
            }
        }
        
        // ═══ CLIENT PREDICTION ═══
        StartCastPrediction(ability);
        
        // ═══ SEND TO SERVER ═══
        CmdCastAbility(
            abilityID: ability.ID,
            targetID: target != null ? target.ObjectId : 0,
            groundPoint: targeting.GetGroundTargetPoint()
        );
    }
    
    private void StartCastPrediction(AbilityData ability) {
        // Animación
        GetComponent<Animator>().SetTrigger("Cast");
        
        // Si tiene cast time, mostrar barra
        if (ability.CastTime > 0) {
            _isCasting = true;
            _currentCast = ability;
            _castEndTime = Time.time + ability.CastTime;
            EventBus.Trigger("OnCastStart", ability.Name, ability.CastTime);
        }
        
        // Descuento visual de maná (se corregirá si falla)
        stats.CurrentMana -= ability.ManaCost;
        
        // Iniciar cooldown visual
        _gcdEndTime = Time.time + ability.GCD;
        _cooldowns[ability.ID] = Time.time + ability.SpecificCooldown;
    }
    
    private void CancelCast() {
        _isCasting = false;
        _currentCast = null;
        EventBus.Trigger("OnCastCancel");
        GetComponent<Animator>().SetTrigger("CastCancel");
    }
    
    // ═══════════════════════════════════════════════════════
    // SERVER AUTHORITY
    // ═══════════════════════════════════════════════════════
    
    [ServerRpc]
    private void CmdCastAbility(int abilityID, int targetID, Vector3 groundPoint) {
        
        // Buscar ability data
        AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityID);
        if (ability == null) {
            RpcAbilityFailed("Habilidad inválida");
            return;
        }
        
        // ═══ VALIDACIONES SERVER ═══
        
        // 1. Recursos
        if (stats.CurrentMana < ability.ManaCost) {
            RpcAbilityFailed("Maná insuficiente (server check)");
            return;
        }
        
        // 2. Target válido
        NetworkObject target = null;
        if (targetID > 0) {
            if (!FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(targetID, out target)) {
                RpcAbilityFailed("Target no existe");
                return;
            }
        }
        
        // 3. Rango (con tolerancia de lag: +20%)
        if (target != null) {
            float distance = Vector3.Distance(transform.position, target.transform.position);
            float maxAllowed = ability.MaxRange * 1.2f; // Lag compensation
            if (distance > maxAllowed) {
                RpcAbilityFailed($"Fuera de rango: {distance:F1}m > {maxAllowed:F1}m");
                return;
            }
        }
        
        // 4. Line of Sight (opcional, solo para abilities que lo requieran)
        if (ability.TargetingMode == TargetType.Enemy && target != null) {
            Vector3 origin = transform.position + Vector3.up * 1.5f; // Eye height
            Vector3 targetPos = target.transform.position + Vector3.up * 1.5f;
            
            if (Physics.Linecast(origin, targetPos, Layers.Environment)) {
                RpcAbilityFailed("Sin línea de visión");
                return;
            }
        }
        
        // ═══ EXECUTION ═══
        
        // Descontar recursos
        stats.CurrentMana -= ability.ManaCost;
        
        // Ejecutar lógica
        ability.Logic.Execute(base.NetworkObject, target, groundPoint, ability);
        
        // Aplicar effects al caster (ej: buffs)
        if (ability.ApplyToSelf != null) {
            StatusEffectSystem statusSystem = GetComponent<StatusEffectSystem>();
            foreach (var effect in ability.ApplyToSelf) {
                statusSystem.ApplyEffect(effect);
            }
        }
        
        // Notificar a todos los clientes (para VFX/SFX)
        RpcOnAbilityExecuted(abilityID, targetID, groundPoint);
    }
    
    [ObserversRpc]
    private void RpcAbilityFailed(string reason) {
        if (!base.IsOwner) return;
        
        Debug.LogWarning($"Ability failed: {reason}");
        
        // Reversar predicción
        CancelCast();
        // Restaurar maná (el server tiene el valor correcto en stats)
        EventBus.Trigger("OnAbilityFailed", reason);
    }
    
    [ObserversRpc]
    private void RpcOnAbilityExecuted(int abilityID, int targetID, Vector3 groundPoint) {
        AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityID);
        
        // VFX local (muzzle flash, etc)
        if (ability.CastSound != null) {
            AudioManager.Instance.PlaySFX(ability.CastSound, transform.position);
        }
        
        // Finalizar barra de cast si aplica
        if (base.IsOwner && _isCasting) {
            _isCasting = false;
            _currentCast = null;
            EventBus.Trigger("OnCastComplete");
        }
    }
    
    private void ShowError(string message) {
        EventBus.Trigger("OnCombatError", message);
    }
}

} // namespace
```

---

## 5. SISTEMA DE TARGETING

### 5.1 TargetingSystem.cs

```csharp
// Assets/_Project/2_Simulation/Targeting/TargetingSystem.cs

using UnityEngine;
using FishNet.Object;

namespace Genesis.Simulation {

public class TargetingSystem : MonoBehaviour {
    
    [Header("Settings")]
    [SerializeField] private float maxTargetDistance = 50f;
    [SerializeField] private GameObject targetRingPrefab;
    
    [Header("Ground Targeting")]
    [SerializeField] private GameObject cursorCrossPrefab;
    [SerializeField] private LayerMask groundLayer;
    
    // State
    public NetworkObject CurrentTarget { get; private set; }
    private GameObject _targetRingInstance;
    private GameObject _cursorCrossInstance;
    private bool _isGroundTargeting;
    private Vector3 _groundTargetPoint;
    
    // ═══════════════════════════════════════════════════════
    // TAB TARGETING
    // ═══════════════════════════════════════════════════════
    
    void Update() {
        // Click izquierdo: seleccionar target
        if (Input.GetMouseButtonDown(0) && !_isGroundTargeting) {
            TrySelectTarget();
        }
        
        // Tab: ciclar targets
        if (Input.GetKeyDown(KeyCode.Tab)) {
            CycleTargets();
        }
        
        // ESC: deseleccionar
        if (Input.GetKeyDown(KeyCode.Escape)) {
            ClearTarget();
        }
        
        // Update de anillo visual
        UpdateTargetRing();
        
        // Update de cursor ground
        if (_isGroundTargeting) {
            UpdateGroundCursor();
        }
    }
    
    private void TrySelectTarget() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, maxTargetDistance, Layers.TargetingMask)) {
            
            if (hit.collider.TryGetComponent(out NetworkObject netObj)) {
                SetTarget(netObj);
            }
        } else {
            // Click en vacío = deseleccionar
            ClearTarget();
        }
    }
    
    private void CycleTargets() {
        // Buscar todas las entidades Damageable en rango
        Collider[] hits = Physics.OverlapSphere(transform.position, maxTargetDistance, Layers.TargetingMask);
        
        if (hits.Length == 0) return;
        
        // Ordenar por distancia
        System.Array.Sort(hits, (a, b) => {
            float distA = Vector3.Distance(transform.position, a.transform.position);
            float distB = Vector3.Distance(transform.position, b.transform.position);
            return distA.CompareTo(distB);
        });
        
        // Buscar siguiente target después del actual
        int currentIndex = -1;
        if (CurrentTarget != null) {
            for (int i = 0; i < hits.Length; i++) {
                if (hits[i].GetComponent<NetworkObject>() == CurrentTarget) {
                    currentIndex = i;
                    break;
                }
            }
        }
        
        // Siguiente (wrap around)
        int nextIndex = (currentIndex + 1) % hits.Length;
        SetTarget(hits[nextIndex].GetComponent<NetworkObject>());
    }
    
    private void SetTarget(NetworkObject target) {
        CurrentTarget = target;
        
        // Crear anillo si no existe
        if (_targetRingInstance == null) {
            _targetRingInstance = Instantiate(targetRingPrefab);
        }
        
        // Evento para UI (mostrar barra de vida del target)
        EventBus.Trigger("OnTargetChanged", target);
    }
    
    public void ClearTarget() {
        CurrentTarget = null;
        
        if (_targetRingInstance != null) {
            _targetRingInstance.SetActive(false);
        }
        
        EventBus.Trigger("OnTargetCleared");
    }
    
    private void UpdateTargetRing() {
        if (CurrentTarget == null || _targetRingInstance == null) return;
        
        // Posicionar a los pies del target
        _targetRingInstance.SetActive(true);
        _targetRingInstance.transform.position = CurrentTarget.transform.position;
        
        // Rotar para que siempre mire a la cámara
        _targetRingInstance.transform.LookAt(Camera.main.transform);
        _targetRingInstance.transform.Rotate(90, 0, 0); // Plano horizontal
    }
    
    // ═══════════════════════════════════════════════════════
    // GROUND TARGETING (Para AoE)
    // ═══════════════════════════════════════════════════════
    
    public void StartGroundTargeting() {
        _isGroundTargeting = true;
        
        if (_cursorCrossInstance == null) {
            _cursorCrossInstance = Instantiate(cursorCrossPrefab);
        }
        
        _cursorCrossInstance.SetActive(true);
        Cursor.visible = false; // Ocultar cursor OS
    }
    
    public void StopGroundTargeting() {
        _isGroundTargeting = false;
        
        if (_cursorCrossInstance != null) {
            _cursorCrossInstance.SetActive(false);
        }
        
        Cursor.visible = true;
    }
    
    private void UpdateGroundCursor() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer)) {
            _groundTargetPoint = hit.point;
            _cursorCrossInstance.transform.position = hit.point + Vector3.up * 0.1f; // Levitar sobre suelo
        }
        
        // Click confirma posición
        if (Input.GetMouseButtonDown(0)) {
            StopGroundTargeting();
            EventBus.Trigger("OnGroundTargetConfirmed", _groundTargetPoint);
        }
        
        // Click derecho cancela
        if (Input.GetMouseButtonDown(1)) {
            StopGroundTargeting();
            EventBus.Trigger("OnGroundTargetCancelled");
        }
    }
    
    public Vector3 GetGroundTargetPoint() => _groundTargetPoint;
}

} // namespace
```

---

## 6. SISTEMA DE PROYECTILES

### 6.1 ProjectileController.cs (Server-Authoritative Physics)

```csharp
// Assets/_Project/2_Simulation/Combat/Projectiles/ProjectileController.cs

using UnityEngine;
using FishNet.Object;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

[RequireComponent(typeof(NetworkObject))]
public class ProjectileController : NetworkBehaviour {
    
    [Header("Runtime Data - Set via Initialize()")]
    private NetworkObject _owner;
    private float _damage;
    private Vector3 _velocity;
    private float _radius;
    private StatusEffectData[] _effects;
    
    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 10f;
    private float _spawnTime;
    
    [Header("Visual")]
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private ParticleSystem particles;
    
    // ═══════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════
    
    public void Initialize(
        NetworkObject owner,
        float damage,
        Vector3 velocity,
        float radius,
        StatusEffectData[] effects
    ) {
        _owner = owner;
        _damage = damage;
        _velocity = velocity;
        _radius = radius;
        _effects = effects;
        _spawnTime = Time.time;
        
        // Desactivar collider físico (usamos SphereCast manual)
        if (TryGetComponent(out Collider col)) {
            col.enabled = false;
        }
    }
    
    public override void OnStartServer() {
        base.OnStartServer();
        // El servidor es quien mueve el proyectil
    }
    
    public override void OnStartClient() {
        base.OnStartClient();
        // Los clientes solo renderizan
        if (trail != null) trail.Clear();
        if (particles != null) particles.Play();
    }
    
    // ═══════════════════════════════════════════════════════
    // SERVER PHYSICS (Anti-Tunneling con SphereCast)
    // ═══════════════════════════════════════════════════════
    
    [Server]
    void FixedUpdate() {
        // Timeout de seguridad
        if (Time.time - _spawnTime > maxLifetime) {
            Despawn();
            return;
        }
        
        float distance = _velocity.magnitude * Time.fixedDeltaTime;
        Vector3 direction = _velocity.normalized;
        
        // SPHERECAST: detecta colisión ANTES de mover
        // Esto evita tunneling (proyectil atraviesa objetos a alta velocidad)
        if (Physics.SphereCast(
            transform.position, 
            _radius, 
            direction, 
            out RaycastHit hit, 
            distance, 
            Layers.Damageable | Layers.Environment
        )) {
            HandleImpact(hit);
            return;
        }
        
        // Si no hubo impacto, mover el proyectil
        transform.position += direction * distance;
        
        // Rotar hacia dirección de movimiento (para modelos con orientación)
        if (direction != Vector3.zero) {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    // ═══════════════════════════════════════════════════════
    // IMPACT HANDLING
    // ═══════════════════════════════════════════════════════
    
    [Server]
    private void HandleImpact(RaycastHit hit) {
        
        // ═══ CASO 1: Impacto con Environment (pared/suelo) ═══
        if (((1 << hit.collider.gameObject.layer) & Layers.Environment) != 0) {
            SpawnImpactVFX(hit.point, hit.normal);
            PlayImpactSound(hit.point);
            Despawn();
            return;
        }
        
        // ═══ CASO 2: Impacto con Entidad ═══
        NetworkObject targetNetObj = hit.collider.GetComponent<NetworkObject>();
        if (targetNetObj == null) {
            Despawn();
            return;
        }
        
        // No auto-daño (el proyectil no daña a quien lo disparó)
        if (targetNetObj == _owner) return;
        
        // ═══ CASO 3: REFLECT (Guerrero con buff de reflejo activo) ═══
        if (targetNetObj.TryGetComponent(out StatusEffectSystem statusSystem)) {
            if (statusSystem.HasEffect(EffectType.Reflect)) {
                // Reflejar el proyectil en dirección opuesta
                _velocity = Vector3.Reflect(_velocity, hit.normal);
                _owner = targetNetObj; // El target ahora es el nuevo dueño del proyectil
                
                // VFX de reflejo (escudo brillante)
                RpcPlayReflectVFX(hit.point);
                
                return; // NO despawnear, el proyectil sigue volando
            }
        }
        
        // ═══ CASO 4: Daño Normal ═══
        if (targetNetObj.TryGetComponent(out IDamageable damageable)) {
            damageable.TakeDamage(_damage, _owner);
        }
        
        // Aplicar status effects al target
        if (_effects != null && _effects.Length > 0) {
            if (targetNetObj.TryGetComponent(out StatusEffectSystem status)) {
                foreach (var effect in _effects) {
                    status.ApplyEffect(effect);
                }
            }
        }
        
        // VFX de impacto (explosión, sangre, chispas)
        SpawnImpactVFX(hit.point, hit.normal);
        PlayImpactSound(hit.point);
        
        // Despawn del proyectil
        Despawn();
    }
    
    // ═══════════════════════════════════════════════════════
    // VFX & SFX SPAWNING
    // ═══════════════════════════════════════════════════════
    
    [Server]
    private void SpawnImpactVFX(Vector3 position, Vector3 normal) {
        // Buscar VFX en el AbilityData o usar uno genérico
        // TODO: Pasar VFXPrefab desde AbilityData al Initialize()
        GameObject vfxPrefab = Resources.Load<GameObject>("VFX/Impact_Generic");
        if (vfxPrefab == null) return;
        
        GameObject vfx = Instantiate(vfxPrefab, position, Quaternion.LookRotation(normal));
        
        // Spawn en red para que todos lo vean
        FishNet.InstanceFinder.ServerManager.Spawn(vfx);
        
        // Auto-cleanup después de 2 segundos
        Destroy(vfx, 2f);
    }
    
    [Server]
    private void PlayImpactSound(Vector3 position) {
        // TODO: Integrar con AudioManager
        // AudioManager.Instance.PlaySFX("ProjectileImpact", position);
    }
    
    [ObserversRpc]
    private void RpcPlayReflectVFX(Vector3 position) {
        // Efecto visual de reflejo (solo clientes)
        // Instanciar partículas de escudo brillante
        GameObject reflectVFX = Resources.Load<GameObject>("VFX/Reflect_Shield");
        if (reflectVFX != null) {
            GameObject instance = Instantiate(reflectVFX, position, Quaternion.identity);
            Destroy(instance, 1f);
        }
    }
    
    // ═══════════════════════════════════════════════════════
    // DESPAWN & CLEANUP
    // ═══════════════════════════════════════════════════════
    
    [Server]
    private void Despawn() {
        // Si estamos usando Object Pooling, devolver al pool
        if (ObjectPoolManager.Instance != null) {
            ObjectPool<ProjectileController> pool = ObjectPoolManager.Instance
                .GetPool<ProjectileController>(gameObject.name);
            
            if (pool != null) {
                pool.Return(this);
                FishNet.InstanceFinder.ServerManager.Despawn(gameObject);
                return;
            }
        }
        
        // Fallback: Destruir directamente
        FishNet.InstanceFinder.ServerManager.Despawn(gameObject);
        Destroy(gameObject, 0.1f); // Delay para evitar errores de red
    }
}

} // namespace
```

### 6.2 Ejemplo de Uso: Disparar Proyectil desde AbilityLogic

```csharp
// En ProjectileAbility.cs (ver sección 4.3)

public override void Execute(
    NetworkObject caster, 
    NetworkObject target, 
    Vector3 groundPoint,
    AbilityData data
) {
    // ═══ PASO 1: Determinar punto de spawn ═══
    Transform spawnPoint = caster.transform.Find("Hand_R");
    if (spawnPoint == null) spawnPoint = caster.transform;
    
    Vector3 spawnPos = spawnPoint.position;
    
    // ═══ PASO 2: Calcular dirección ═══
    Vector3 direction;
    if (target != null) {
        // Apuntar al centro del target (bounds center)
        Bounds bounds = target.GetComponent<Collider>().bounds;
        direction = (bounds.center - spawnPos).normalized;
    } else {
        // Apuntar al ground point (para AoE)
        direction = (groundPoint - spawnPos).normalized;
    }
    
    // ═══ PASO 3: Obtener proyectil del pool ═══
    ProjectileController projectile = ObjectPoolManager.Instance
        .GetPool<ProjectileController>(data.ProjectilePrefab.name)
        .Get();
    
    // ═══ PASO 4: Configurar proyectil ═══
    projectile.transform.position = spawnPos;
    projectile.transform.rotation = Quaternion.LookRotation(direction);
    
    projectile.Initialize(
        owner: caster,
        damage: data.BaseDamage,
        velocity: direction * data.ProjectileSpeed,
        radius: data.ProjectileRadius,
        effects: data.ApplyToTarget
    );
    
    // ═══ PASO 5: Spawn en red ═══
    FishNet.InstanceFinder.ServerManager.Spawn(projectile.gameObject);
}
```

### 6.3 Configuración de Prefab de Proyectil

**Estructura del Prefab `Fireball.prefab`:**
```text
Fireball (Root)
├── NetworkObject               (FishNet)
├── ProjectileController        (Este script)
├── MeshRenderer                (Esfera con material emisivo)
├── TrailRenderer               (Rastro de fuego)
└── ParticleSystem              (Chispas/humo)

Inspector Settings:
- Layer: Projectile (7)
- Tag: Projectile
- NetworkObject:
  - Is Networked: TRUE
  - Spawn Type: Nested (spawneado por server)
  - Sync Transform: FALSE (lo movemos manualmente)
```

**Material del Proyectil (URP/Lit):**
```text
Base Color: Naranja (#FF6600)
Emission: Activado
  - Color: Amarillo (#FFFF00)
  - Intensity: 2
Smoothness: 0.9
```

### 6.4 Debugging y Visualización

**Gizmos para Debug (agregar al ProjectileController):**
```csharp
#if UNITY_EDITOR
void OnDrawGizmos() {
    if (!Application.isPlaying) return;
    
    // Dibujar la esfera de colisión
    Gizmos.color = Color.red;
    Gizmos.DrawWireSphere(transform.position, _radius);
    
    // Dibujar la dirección de movimiento
    Gizmos.color = Color.yellow;
    Gizmos.DrawRay(transform.position, _velocity.normalized * 2f);
}
#endif
```

### 6.5 Optimizaciones Importantes

**1. Pooling Obligatorio:**
```csharp
// En Bootstrap, configurar pools:
ObjectPoolManager:
  Pool Configs:
    - Fireball: PreWarm 30
    - Arrow: PreWarm 50
    - IceBolt: PreWarm 20
```

**2. Límite de Proyectiles Activos:**
```csharp
// En ProjectilePool o ServerManager
private const int MAX_PROJECTILES = 200;
private int _activeProjectileCount;

[Server]
public void SpawnProjectile(...) {
    if (_activeProjectileCount >= MAX_PROJECTILES) {
        Debug.LogWarning("Max projectiles reached!");
        return;
    }
    
    // Spawn normal...
    _activeProjectileCount++;
}

[Server]
private void OnProjectileDespawned() {
    _activeProjectileCount--;
}
```

**3. Reducir Frecuencia de Ticks:**
```csharp
// Si tienes muchos proyectiles, puedes skipear frames
private int _tickSkip = 0;

[Server]
void FixedUpdate() {
    // Solo procesar cada 2 frames (25Hz en vez de 50Hz)
    _tickSkip++;
    if (_tickSkip % 2 != 0) return;
    
    // Lógica normal...
}
```

### 6.6 Casos Especiales: Proyectiles Homing

Para proyectiles que persiguen al target (ej: Misiles Mágicos):
```csharp
[Header("Homing Settings")]
[SerializeField] private bool isHoming;
[SerializeField] private float homingStrength = 5f;

[Server]
void FixedUpdate() {
    // ... código existente ...
    
    if (isHoming && _target != null) {
        // Calcular dirección hacia el target
        Vector3 toTarget = (_target.transform.position - transform.position).normalized;
        
        // Interpolar suavemente hacia el target
        _velocity = Vector3.Lerp(_velocity.normalized, toTarget, homingStrength * Time.fixedDeltaTime) 
                    * _velocity.magnitude;
    }
    
    // ... resto del código ...
}
```

### 6.7 Casos Especiales: Proyectiles Penetrantes

Para proyectiles que atraviesan múltiples enemigos (ej: Lanza Perforante):
```csharp
[Header("Pierce Settings")]
[SerializeField] private bool canPierce;
[SerializeField] private int maxPierceTargets = 3;
private HashSet<int> _hitTargets = new HashSet<int>();

[Server]
private void HandleImpact(RaycastHit hit) {
    // ... validaciones ...
    
    NetworkObject targetNetObj = hit.collider.GetComponent<NetworkObject>();
    
    // Si ya golpeamos este target, ignorar
    if (_hitTargets.Contains(targetNetObj.ObjectId)) return;
    
    // Aplicar daño
    if (targetNetObj.TryGetComponent(out IDamageable damageable)) {
        damageable.TakeDamage(_damage, _owner);
        _hitTargets.Add(targetNetObj.ObjectId);
    }
    
    // Si NO puede penetrar o alcanzó el límite, despawnear
    if (!canPierce || _hitTargets.Count >= maxPierceTargets) {
        SpawnImpactVFX(hit.point, hit.normal);
        Despawn();
    }
    // Si puede penetrar, continuar volando
}
```

### 6.8 Testing Checklist

**Test Cases para Proyectiles:**

*   ✅ **TC-01: Disparo Básico**
    *   Jugador lanza proyectil → Vuela en línea recta → Impacta dummy → Daño aplicado
*   ✅ **TC-02: Colisión con Pared**
    *   Proyectil impacta Environment → VFX spawneado → Proyectil despawneado
*   ✅ **TC-03: Auto-Daño**
    *   Jugador dispara hacia sus pies → Proyectil lo ignora (no auto-daño)
*   ✅ **TC-04: Reflect**
    *   Guerrero con buff Reflect → Mago dispara → Proyectil rebota → Mago recibe daño
*   ✅ **TC-05: Múltiples Proyectiles**
    *   Spawn 50 proyectiles simultáneos → Server mantiene 50 FPS → No hay lag
*   ✅ **TC-06: Tunneling**
    *   Proyectil a 100 m/s → Atraviesa pared delgada → SphereCast lo detecta
*   ✅ **TC-07: Pooling**
    *   Disparar 100 proyectiles → Todos se despawnean → Pool los recicla → No memory leaks
*   ✅ **TC-08: Network Sync**
    *   Cliente A dispara → Cliente B ve proyectil en misma posición → Latencia < 50ms

---

## 7. SISTEMA DE STATUS EFFECTS

### 7.1 Definición de Datos: StatusEffectData.cs

```csharp
// Assets/_Project/1_Data/ScriptableObjects/Core/StatusEffectData.cs

using UnityEngine;

namespace Genesis.Data {

[CreateAssetMenu(fileName = "Effect_", menuName = "Genesis/Combat/Status Effect")]
public class StatusEffectData : ScriptableObject {
    
    [Header("═══ CORE ═══")]
    public int ID;
    public string Name;
    public Sprite Icon;
    
    [Header("═══ TYPE ═══")]
    public EffectType Type;
    
    [Header("═══ DURATION ═══")]
    [Tooltip("Duración en segundos. 0 = permanente hasta remover")]
    public float Duration;
    
    [Tooltip("¿Se puede apilar con otros efectos del mismo tipo?")]
    public bool IsStackable;
    
    [Tooltip("Máximo de stacks permitidos")]
    public int MaxStacks = 1;
    
    [Header("═══ MAGNITUDE ═══")]
    [Tooltip("Para Slow: % de reducción (0.5 = 50% más lento)")]
    [Range(0f, 1f)]
    public float SlowMagnitude = 0.5f;
    
    [Tooltip("Para Shield: Cantidad de absorción")]
    public float ShieldAmount;
    
    [Tooltip("Para DoT/HoT: Daño/Curación por tick")]
    public float TickDamage;
    
    [Tooltip("Intervalo de tick (segundos)")]
    public float TickInterval = 1f;
    
    [Header("═══ VISUALS ═══")]
    public GameObject VFXPrefab; // Aura persistente
    public AudioClip ApplySound;
    public AudioClip RemoveSound;
}

// ═══════════════════════════════════════════════════════
// ENUMS (BITFLAGS para combinar efectos)
// ═══════════════════════════════════════════════════════

[System.Flags]
public enum EffectType {
    None         = 0,
    Stun         = 1 << 0,  // 1   - Bloquea movimiento y acciones
    Root         = 1 << 1,  // 2   - Bloquea solo movimiento
    Silence      = 1 << 2,  // 4   - Bloquea habilidades
    Slow         = 1 << 3,  // 8   - Reduce velocidad
    Shield       = 1 << 4,  // 16  - Absorbe daño
    Reflect      = 1 << 5,  // 32  - Refleja proyectiles
    Invulnerable = 1 << 6,  // 64  - Inmune a todo daño
    Poison       = 1 << 7,  // 128 - DoT (Damage over Time)
    Regen        = 1 << 8,  // 256 - HoT (Heal over Time)
    Haste        = 1 << 9,  // 512 - Aumenta velocidad
}

} // namespace
```

### 7.2 StatusEffectSystem.cs (Manager por Entidad)

```csharp
// Assets/_Project/2_Simulation/Combat/StatusEffects/StatusEffectSystem.cs

using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Data;
using System.Collections.Generic;

namespace Genesis.Simulation.Combat {

public class StatusEffectSystem : NetworkBehaviour {
    
    // ═══════════════════════════════════════════════════════
    // SYNCVAR - Estado sincronizado con clientes
    // ═══════════════════════════════════════════════════════
    
    [SyncObject]
    private readonly SyncDictionary<int, ActiveEffect> _activeEffects = new SyncDictionary<int, ActiveEffect>();
    
    // Cache local para VFX
    private Dictionary<int, GameObject> _vfxInstances = new Dictionary<int, GameObject>();
    
    // Referencias
    private PlayerController _controller;
    private PlayerStats _stats;
    
    // ═══════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════
    
    void Awake() {
        _controller = GetComponent<PlayerController>();
        _stats = GetComponent<PlayerStats>();
        
        // Suscribirse a cambios en el diccionario (para VFX en clientes)
        _activeEffects.OnChange += OnEffectsChanged;
    }
    
    public override void OnStartClient() {
        base.OnStartClient();
        
        // Inicializar VFX para efectos ya activos (late join)
        foreach (var kvp in _activeEffects) {
            SpawnVFX(kvp.Key, kvp.Value.effectID);
        }
    }
    
    // ═══════════════════════════════════════════════════════
    // SERVER API
    // ═══════════════════════════════════════════════════════
    
    [Server]
    public void ApplyEffect(StatusEffectData data) {
        if (data == null) return;
        
        // ═══ CASO 1: Invulnerable = ignora todo ═══
        if (HasEffect(EffectType.Invulnerable)) {
            Debug.Log($"{gameObject.name} es invulnerable, efecto ignorado");
            return;
        }
        
        // ═══ CASO 2: Efecto ya existe ═══
        if (_activeEffects.ContainsKey(data.ID)) {
            
            // Si NO es stackable, refrescar duración
            if (!data.IsStackable) {
                ActiveEffect existing = _activeEffects[data.ID];
                existing.expirationTime = Time.time + data.Duration;
                _activeEffects[data.ID] = existing;
                return;
            }
            
            // Si ES stackable, incrementar stack
            ActiveEffect stacked = _activeEffects[data.ID];
            if (stacked.stackCount < data.MaxStacks) {
                stacked.stackCount++;
                stacked.expirationTime = Time.time + data.Duration;
                _activeEffects[data.ID] = stacked;
            }
            return;
        }
        
        // ═══ CASO 3: Nuevo efecto ═══
        ActiveEffect newEffect = new ActiveEffect {
            effectID = data.ID,
            type = data.Type,
            expirationTime = data.Duration > 0 ? Time.time + data.Duration : float.MaxValue,
            stackCount = 1,
            tickInterval = data.TickInterval,
            nextTickTime = Time.time + data.TickInterval,
            magnitude = data.SlowMagnitude,
            shieldAmount = data.ShieldAmount,
            tickDamage = data.TickDamage
        };
        
        _activeEffects.Add(data.ID, newEffect);
        
        // Aplicar efecto inmediato (ej: Shield)
        if (data.Type.HasFlag(EffectType.Shield)) {
            _stats.AddShield(data.ShieldAmount);
        }
        
        // Notificar clientes para VFX/SFX
        RpcOnEffectApplied(data.ID);
    }
    
    [Server]
    public void RemoveEffect(int effectID) {
        if (!_activeEffects.ContainsKey(effectID)) return;
        
        ActiveEffect effect = _activeEffects[effectID];
        
        // Limpiar shield si corresponde
        if (effect.type.HasFlag(EffectType.Shield)) {
            _stats.RemoveShield(effect.shieldAmount);
        }
        
        _activeEffects.Remove(effectID);
        
        RpcOnEffectRemoved(effectID);
    }
    
    [Server]
    void Update() {
        // Procesar todos los efectos activos
        List<int> toRemove = new List<int>();
        
        foreach (var kvp in _activeEffects) {
            int id = kvp.Key;
            ActiveEffect effect = kvp.Value;
            
            // ═══ EXPIRACIÓN ═══
            if (Time.time >= effect.expirationTime) {
                toRemove.Add(id);
                continue;
            }
            
            // ═══ TICK DAMAGE/HEAL ═══
            if (effect.tickDamage != 0 && Time.time >= effect.nextTickTime) {
                ProcessTick(effect);
                
                // Actualizar siguiente tick
                effect.nextTickTime = Time.time + effect.tickInterval;
                _activeEffects[id] = effect; // Actualizar struct
            }
        }
        
        // Remover efectos expirados
        foreach (int id in toRemove) {
            RemoveEffect(id);
        }
    }
    
    [Server]
    private void ProcessTick(ActiveEffect effect) {
        if (effect.type.HasFlag(EffectType.Poison)) {
            // DoT
            _stats.TakeDamage(effect.tickDamage * effect.stackCount, null);
        } else if (effect.type.HasFlag(EffectType.Regen)) {
            // HoT
            _stats.Heal(effect.tickDamage * effect.stackCount);
        }
    }
    
    // ═══════════════════════════════════════════════════════
    // QUERIES (Usadas por otros sistemas)
    // ═══════════════════════════════════════════════════════
    
    public bool HasEffect(EffectType type) {
        foreach (var effect in _activeEffects.Values) {
            if (effect.type.HasFlag(type)) return true;
        }
        return false;
    }
    
    public float GetSlowMultiplier() {
        if (!HasEffect(EffectType.Slow)) return 1f;
        
        // Buscar el slow más fuerte
        float maxSlow = 0f;
        foreach (var effect in _activeEffects.Values) {
            if (effect.type.HasFlag(EffectType.Slow)) {
                maxSlow = Mathf.Max(maxSlow, effect.magnitude);
            }
        }
        
        return 1f - maxSlow; // 0.5 magnitude = 50% speed = multiplier 0.5
    }
    
    public int GetStackCount(int effectID) {
        return _activeEffects.TryGetValue(effectID, out ActiveEffect effect) ? effect.stackCount : 0;
    }
    
    // ═══════════════════════════════════════════════════════
    // CLIENT VISUALS
    // ═══════════════════════════════════════════════════════
    
    private void OnEffectsChanged(SyncDictionaryOperation op, int key, ActiveEffect value, bool asServer) {
        // Solo clientes procesan VFX
        if (asServer) return;
        
        switch (op) {
            case SyncDictionaryOperation.Add:
                SpawnVFX(key, value.effectID);
                break;
            
            case SyncDictionaryOperation.Remove:
                DespawnVFX(key);
                break;
        }
    }
    
    [ObserversRpc]
    private void RpcOnEffectApplied(int effectID) {
        StatusEffectData data = StatusEffectDatabase.Instance.GetEffect(effectID);
        if (data == null) return;
        
        // SFX
        if (data.ApplySound != null) {
            AudioManager.Instance.PlaySFX(data.ApplySound, transform.position);
        }
        
        // Floating text (solo owner)
        if (base.IsOwner) {
            EventBus.Trigger("OnStatusApplied", data.Name);
        }
    }
    
    [ObserversRpc]
    private void RpcOnEffectRemoved(int effectID) {
        StatusEffectData data = StatusEffectDatabase.Instance.GetEffect(effectID);
        if (data == null) return;
        
        if (data.RemoveSound != null) {
            AudioManager.Instance.PlaySFX(data.RemoveSound, transform.position);
        }
    }
    
    private void SpawnVFX(int key, int effectID) {
        StatusEffectData data = StatusEffectDatabase.Instance.GetEffect(effectID);
        if (data == null || data.VFXPrefab == null) return;
        
        GameObject vfx = Instantiate(data.VFXPrefab, transform);
        vfx.transform.localPosition = Vector3.up * 1.5f; // Sobre la cabeza
        _vfxInstances[key] = vfx;
    }
    
    private void DespawnVFX(int key) {
        if (_vfxInstances.TryGetValue(key, out GameObject vfx)) {
            Destroy(vfx);
            _vfxInstances.Remove(key);
        }
    }
}

// ═══════════════════════════════════════════════════════
// SERIALIZABLE STRUCT (Para SyncDictionary)
// ═══════════════════════════════════════════════════════

[System.Serializable]
public struct ActiveEffect {
    public int effectID;
    public EffectType type;
    public float expirationTime;
    public int stackCount;
    public float tickInterval;
    public float nextTickTime;
    
    // Magnitude específica por tipo
    public float magnitude;      // Para Slow/Haste
    public float shieldAmount;   // Para Shield
    public float tickDamage;     // Para DoT/HoT
}

} // namespace
```

### 7.3 Integración con PlayerController (Slow Effect)

```csharp
// Modificación en PlayerController.cs - Método de movimiento

void FixedUpdate() {
    if (!base.IsOwner) return;
    
    Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
    
    // ═══ STATUS EFFECTS CHECKS ═══
    StatusEffectSystem statusSystem = GetComponent<StatusEffectSystem>();
    
    // Stun = no movimiento ni acciones
    if (statusSystem.HasEffect(EffectType.Stun)) {
        input = Vector3.zero;
        return;
    }
    
    // Root = no movimiento pero sí acciones
    if (statusSystem.HasEffect(EffectType.Root)) {
        input = Vector3.zero;
        return;
    }
    
    // ═══ MOVIMIENTO NORMAL ═══
    if (input.magnitude > 0.1f) {
        input.Normalize();
        
        // Aplicar slow multiplier
        float speedMultiplier = statusSystem.GetSlowMultiplier();
        
        // Aplicar haste (si existe)
        if (statusSystem.HasEffect(EffectType.Haste)) {
            speedMultiplier *= 1.3f; // +30% velocidad
        }
        
        Vector3 movement = input * moveSpeed * speedMultiplier * Time.fixedDeltaTime;
        
        _characterController.Move(movement);
        
        // Rotar hacia la dirección de movimiento
        transform.rotation = Quaternion.Slerp(
            transform.rotation, 
            Quaternion.LookRotation(input), 
            rotationSpeed * Time.fixedDeltaTime
        );
        
        // Animar
        _animator.SetFloat("Speed", input.magnitude * speedMultiplier);
    } else {
        _animator.SetFloat("Speed", 0f);
    }
}
```

### 7.4 Integración con PlayerStats (Shield)

```csharp
// Modificación en PlayerStats.cs

public class PlayerStats : NetworkBehaviour {
    
    [SyncVar(OnChange = nameof(OnHealthChanged))]
    private float _currentHealth;
    
    [SyncVar(OnChange = nameof(OnManaChanged))]
    private float _currentMana;
    
    [SyncVar(OnChange = nameof(OnShieldChanged))]
    private float _currentShield; // NUEVO
    
    // ═══ SHIELD MANAGEMENT ═══
    
    [Server]
    public void AddShield(float amount) {
        _currentShield += amount;
    }
    
    [Server]
    public void RemoveShield(float amount) {
        _currentShield = Mathf.Max(0, _currentShield - amount);
    }
    
    // ═══ DAMAGE CON SHIELD ═══
    
    [Server]
    public void TakeDamage(float damage, NetworkObject attacker) {
        
        // Check invulnerabilidad
        if (GetComponent<StatusEffectSystem>().HasEffect(EffectType.Invulnerable)) {
            RpcShowDamageText("IMMUNE", Color.yellow);
            return;
        }
        
        // ═══ PASO 1: Absorber con Shield ═══
        if (_currentShield > 0) {
            float shieldAbsorbed = Mathf.Min(damage, _currentShield);
            _currentShield -= shieldAbsorbed;
            damage -= shieldAbsorbed;
            
            RpcShowDamageText($"{shieldAbsorbed:F0} (SHIELD)", Color.cyan);
            
            if (damage <= 0) return; // Shield absorbió todo
        }
        
        // ═══ PASO 2: Daño a HP ═══
        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        
        RpcShowDamageText($"-{damage:F0}", Color.red);
        
        // ═══ PASO 3: Check Death ═══
        if (_currentHealth <= 0) {
            Die();
        }
    }
    
    private void OnShieldChanged(float oldValue, float newValue, bool asServer) {
        if (!asServer) {
            EventBus.Trigger("OnShieldChanged", newValue);
        }
    }
}
```

---