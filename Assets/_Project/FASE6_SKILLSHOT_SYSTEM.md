# FASE 6: SKILLSHOT SYSTEM - ARQUITECTURA FINAL

## 📋 RESUMEN EJECUTIVO

Este documento detalla la implementación del **Sistema de Skillshots** para VENERA.FATUM, transformando el combate de tab-target clásico a un sistema híbrido que soporta:

- **14 habilidades Targeted** (sistema legacy - mantener sin cambios)
- **10 habilidades Skillshot** (nuevo sistema de apuntado manual)

### Tipos de Targeting Implementados

| Tipo | Cantidad | Indicador | Input Flow |
|------|----------|-----------|------------|
| **Targeted** | 14 | Ninguno | Instant cast con target seleccionado |
| **Line (Skillshot)** | 1 | LineIndicator | Press → Aim → Click |
| **Line (Channel)** | 1 | LineIndicator | Press → Aim → Hold Click |
| **Circle (Ground AOE)** | 3 | CircleIndicator | Press → Move mouse → Click |
| **Circle (Self AOE)** | 2 | CircleIndicator | Press → Instant cast |
| **Cone** | 1 | ConeIndicator | Press → Aim → Click |
| **Dash** | 2 | ArrowIndicator | Press → Aim → Click |
| **Trap** | 1 | TrapIndicator | Press → Place → Persists |

---

## 🎯 CATEGORIZACIÓN DE HABILIDADES

### TIPO 1: TARGETED (Legacy - NO TOCAR) ✅

**Sistema**: Tab-target actual (Fase 5)
**Validaciones**: Mana, Cooldown, Range, Line of Sight
**Indicador**: Ninguno

**Habilidades** (14):
```
GUERRERO:
  - Golpe Rápido (Instant, Damage, 2m, 0.8s CD)
  - Reflejo (Instant, Buff, Self, 15s CD)
  - Empalamiento (Instant, Damage + Root, 3m, 12s CD)
  - Fortificar (Instant, Buff -30% damage, Self, 25s CD)

MAGO:
  - Daga de Maná (Cast 0.4s, Damage, 20m, 0.5s CD)
  - Armadura Arcana (Instant, Buff + Shield, Self, 30s CD)

SACERDOTE:
  - Punición (Cast 0.4s, Damage, 20m, 0.5s CD)
  - Luz Sanadora (Cast 1.5s, Heal, 25m, 0s CD)
  - Escudo Sagrado (Instant, Buff + Shield, 20m, 6s CD)
  - Rezo de Fe (Instant, Buff AOE, 15m, 60s CD)
  - Ira de Dios (Instant, Damage + Knockback, 10m, 10s CD)

CAZADOR:
  - Tiro Firme (Instant, Damage, 22m, 0.7s CD)
  - Ojo de Halcón (Instant, Buff, Self, 25s CD)
```

**Configuración en AbilityData**:
```yaml
IndicatorType: None
TargetingMode: Enemy / Ally / Self
```

---

### TIPO 2: DIRECCIONAL (Proyectil Skillshot) 🎯

**Sistema**: Nuevo - Indicador de línea + confirmación
**Input**: Press key → Aim con mouse → Click para lanzar
**Indicador**: LineIndicator (rayo con esfera al final)

**Habilidades** (1):
```
MAGO:
  - Bola de Fuego (Cast 1.2s, Damage, 25m, 0s CD)
    - Proyectil viaja en línea recta
    - Daño al primer impacto: 50
    - Velocidad: 20 m/s
```

**Configuración**:
```yaml
IndicatorType: Line
TargetingMode: Ground
Logic: SkillshotLogic
ProjectilePrefab: Fireball
Range: 25
Radius: 0.5  # Ancho del proyectil
```

**Prefab Visual**:
- LineRenderer (verde si válido, rojo si obstruido)
- Esfera al final (preview de impacto)
- Raycast para detectar obstáculos

---

### TIPO 3: DIRECCIONAL (Línea Channel) 🌊

**Sistema**: Nuevo - Laser continuo mientras se mantiene click
**Input**: Press key → Aim → HOLD Left Click → Release para terminar
**Indicador**: LineIndicator (persistent, más ancho)

**Habilidades** (1):
```
MAGO:
  - Rayo de Hielo (Channel, Damage, 15m, 8s CD)
    - Tick damage: 10 cada 0.2s
    - Total damage max: ~250 (si channel completo)
    - Slow: 30% mientras se mantiene
```

**Configuración**:
```yaml
IndicatorType: Line
TargetingMode: Ground
CastType: Channeling
Logic: ChannelLogic
Range: 15
Radius: 1.0  # Ancho del rayo
```

**Diferencia con Skillshot**:
- Skillshot: Click único → Proyectil sale → Impacta → Termina
- Channel: Hold click → Laser continuo → Tick damage → Release

---

### TIPO 4: AOE CIRCULAR (Ground Point) 🎯

**Sistema**: Nuevo - Círculo en el suelo + confirmación
**Input**: Press key → Move mouse → Click en ground point
**Indicador**: CircleIndicator (disco en el suelo)

**Habilidades** (3):
```
MAGO:
  - Meteorito (Cast 1.8s, Damage, 20m range, 4m radius, 20s CD)
    - Damage: 150 en área
    - Delay de impacto: 1 segundo después de confirmar

SACERDOTE:
  - Sagrario (Cast 1.0s, Heal AOE, 15m range, 5m radius, 12s CD)
    - Heal: 60 a todos los aliados en área

CAZADOR:
  - Salva (Cast 0.8s, Damage, 20m range, 3m radius, 8s CD)
    - Damage: 80 a todos los enemigos en área
```

**Configuración**:
```yaml
IndicatorType: Circle
TargetingMode: Ground
Logic: AOELogic
Range: 20      # Max distance from caster
Radius: 4      # Circle radius
```

**Prefab Visual**:
- Cylinder plano (disco)
- Escala según radio
- Color verde/rojo según validez
- Opcional: Mostrar cantidad de enemigos en área

---

### TIPO 5: AOE CIRCULAR (Self-Centered) 💥

**Sistema**: Nuevo - Círculo centrado en el jugador + instant cast
**Input**: Press key → Instant cast (NO requiere confirmación)
**Indicador**: CircleIndicator centrado en caster (opcional preview)

**Habilidades** (2):
```
GUERRERO:
  - Torbellino (Instant, Damage, 5m radius, 5s CD)
    - Damage: 40 a todos los enemigos cercanos

MAGO:
  - Nova de Escarcha (Instant, Damage + Slow, 6m radius, 12s CD)
    - Damage: 50
    - Slow: 40% durante 2s
```

**Configuración**:
```yaml
IndicatorType: Circle
TargetingMode: Self
Logic: SelfAOELogic
Range: 0       # No aplica (siempre centrado)
Radius: 5      # Radio del AOE
```

**Diferencia con Tipo 4**:
- Tipo 4: Requiere click de confirmación, se coloca en el suelo
- Tipo 5: Instant cast, siempre centrado en el caster

---

### TIPO 6: CONO (Frontal Area) 🍕

**Sistema**: Nuevo - Área cónica frontal + confirmación
**Input**: Press key → Aim dirección → Click
**Indicador**: ConeIndicator (forma de pizza slice)

**Habilidades** (1):
```
CAZADOR:
  - Multidisparo (Instant, Damage, 15m range, 60° angle, 4s CD)
    - Damage: 35 a todos los enemigos en cono
    - Hits múltiples: 3 proyectiles
```

**Configuración**:
```yaml
IndicatorType: Cone
TargetingMode: Ground
Logic: ConeLogic
Range: 15
Radius: 0      # No aplica para cono
Angle: 60      # NEW: Ángulo del cono en grados
```

**Prefab Visual**:
- Mesh cónico (fan shape)
- Ángulo y distancia configurables
- Color según cantidad de enemigos detectados

---

### TIPO 7: DASH/CHARGE (Movement) 🏃

**Sistema**: Nuevo - Flecha de movimiento + confirmación
**Input**: Press key → Aim posición → Click
**Indicador**: ArrowIndicator (flecha 3D + path line)

**Habilidades** (2):
```
GUERRERO:
  - Carga (Movement, 15m range, 10s CD)
    - Dirección: HACIA ADELANTE (hacia mouse)
    - Damage: 30 si impacta enemigos en trayecto
    - Knock-up: Opcional

CAZADOR:
  - Desenganche (Movement, 8m range, 12s CD)
    - Dirección: HACIA ATRÁS (opuesto al mouse)
    - No daño
    - Evade: Invulnerable durante dash
```

**Configuración**:
```yaml
IndicatorType: Arrow
TargetingMode: None
Logic: DashLogic
Range: 15
Radius: 1.0    # Radio de colisión durante dash

# Custom parameter en DashLogic:
isBackwards: false  # True para Desenganche
```

**Prefab Visual**:
- LineRenderer (path preview)
- Arrow model 3D al final
- Validación de obstáculos (rojo si hay pared)

**Caso especial - Desenganche**:
```csharp
// DashLogic.cs
if (isBackwards) {
    direction = -direction;
}
```

---

### TIPO 8: TRAMPA (Trigger AOE) 🪤

**Sistema**: Nuevo - Placement + persistencia en mundo
**Input**: Press key → Place en ground → Persiste hasta activarse
**Indicador**: TrapIndicator (circle + trap model preview)

**Habilidades** (1):
```
CAZADOR:
  - Trampa de Hielo (Instant, Damage + Slow, 5m place range, 2m radius, 15s CD)
    - Lifetime: 30 segundos
    - Trigger: Enemigo entra en área
    - Damage: 40 + Slow 50% por 3s
```

**Configuración**:
```yaml
IndicatorType: Trap
TargetingMode: Ground
Logic: TrapLogic
Range: 5       # Max distance to place
Radius: 2      # Trigger radius
```

**Prefab Visual**:
- CircleIndicator para placement
- Trap model 3D preview
- Después de colocar: Trap persiste en el mundo (networked entity)

**Comportamiento**:
1. Press key → Show indicator
2. Click → Spawn trap entity (networked)
3. Trap persists hasta:
   - Enemigo lo activa → Explota
   - Lifetime expira (30s)
   - Destruido por player

---

## 🏗️ ARQUITECTURA DE CÓDIGO

### 1. AbilityData.cs - Actualización

**Archivo**: `Assets/_Project/1_Data/ScriptableObjects/Core/AbilityData.cs`

```csharp
[Header("Targeting")]
public TargetType TargetingMode;      // EXISTENTE
public IndicatorType IndicatorType;   // NEW - Línea 30
public float Range;
public float Radius;                  // Para AOE
public float Angle;                   // NEW - Para Cone (línea 34)

public enum IndicatorType {
    None,      // Targeted abilities (legacy system)
    Line,      // Skillshot direccional + Channel
    Circle,    // AOE circular (ground o self)
    Cone,      // Área cónica frontal
    Arrow,     // Dash/Charge (movement)
    Trap       // Trampa persistente
}
```

---

### 2. AbilityLogic.cs - Actualización

**Archivo**: `Assets/_Project/1_Data/ScriptableObjects/Core/AbilityLogic.cs`

```csharp
public abstract class AbilityLogic : ScriptableObject {

    /// <summary>
    /// LEGACY: Execute con target (mantener compatibilidad)
    /// </summary>
    public virtual void Execute(NetworkObject caster, NetworkObject target, Vector3 groundPoint, AbilityData data) {
        // Default: Redirect to directional
        Vector3 direction = caster.transform.forward;
        if (target != null) {
            direction = (target.transform.position - caster.transform.position).normalized;
        }
        ExecuteDirectional(caster, groundPoint, direction, data);
    }

    /// <summary>
    /// NEW: Execute direccional (para skillshots)
    /// CRITICAL: Solo llamar en SERVER
    /// </summary>
    public abstract void ExecuteDirectional(
        NetworkObject caster,
        Vector3 targetPoint,
        Vector3 direction,
        AbilityData data
    );

    public virtual bool Validate(NetworkObject caster, NetworkObject target, Vector3 point) {
        return true;
    }
}
```

---

### 3. PlayerCombat.cs - Refactorización

**Archivo**: `Assets/_Project/2_Simulation/Entities/Player/PlayerCombat.cs`

#### Nuevo Estado

```csharp
public enum CombatState {
    Idle,
    Aiming,       // NEW: Esperando confirmación (skillshot)
    Casting,      // Casting time (ej: Meteorito 1.8s)
    Channeling    // NEW: Manteniendo (ej: Rayo de Hielo)
}

[Header("Indicator System")]
[SerializeField] private AbilityIndicatorSystem indicatorSystem;

private CombatState _combatState = CombatState.Idle;
private AbilityData _pendingAbility;
```

#### Flujo de Input Actualizado

```csharp
void Update() {
    if (!base.IsOwner) return;

    switch (_combatState) {
        case CombatState.Idle:
            HandleIdleInput();
            break;

        case CombatState.Aiming:
            HandleAimingInput();
            break;

        case CombatState.Casting:
            // Existing casting logic
            break;

        case CombatState.Channeling:
            HandleChannelInput();
            break;
    }
}

private void HandleIdleInput() {
    // Input 1-6
    if (Keyboard.current.digit1Key.wasPressedThisFrame) HandleAbilityInput(0);
    if (Keyboard.current.digit2Key.wasPressedThisFrame) HandleAbilityInput(1);
    // ...
}

private void HandleAbilityInput(int slotIndex) {
    if (slotIndex >= abilitySlots.Count) return;
    AbilityData ability = abilitySlots[slotIndex];
    if (ability == null) return;

    // Validaciones básicas
    if (!ValidateBasicRequirements(ability)) return;

    // DECISIÓN ARQUITECTÓNICA CLAVE:
    if (ability.IndicatorType == IndicatorType.None) {
        // LEGACY PATH: Targeted ability (usa sistema actual)
        ExecuteTargetedAbility(ability);
    } else {
        // NEW PATH: Skillshot (requiere aiming)
        EnterAimingMode(ability);
    }
}
```

#### Modo Aiming

```csharp
private void EnterAimingMode(AbilityData ability) {
    _combatState = CombatState.Aiming;
    _pendingAbility = ability;

    // Show Indicator
    if (indicatorSystem != null) {
        indicatorSystem.ShowIndicator(ability, transform);
    }

    Debug.Log($"[PlayerCombat] Aiming: {ability.Name}");
}

private void HandleAimingInput() {
    // Update indicator position con mouse
    if (indicatorSystem != null) {
        indicatorSystem.UpdateIndicator(Mouse.current.position.ReadValue());
    }

    // Confirm with Left Click
    if (Mouse.current.leftButton.wasPressedThisFrame) {
        ConfirmAbility();
    }

    // Cancel with Right Click or Escape
    if (Mouse.current.rightButton.wasPressedThisFrame ||
        Keyboard.current.escapeKey.wasPressedThisFrame) {
        CancelAiming();
    }
}

private void ConfirmAbility() {
    if (_pendingAbility == null || indicatorSystem == null) return;

    AbilityIndicator indicator = indicatorSystem.GetCurrentIndicator();
    if (indicator == null) return;

    // Validar que el targeting sea válido
    if (!indicator.IsValid()) {
        Debug.LogWarning("Posición inválida!");
        return;
    }

    // Obtener datos del indicador
    Vector3 targetPoint = indicator.GetTargetPoint();
    Vector3 direction = indicator.GetDirection();

    // Hide indicator
    indicatorSystem.HideIndicator();

    // Change state
    _combatState = CombatState.Casting;

    // Send to server con DIRECCIÓN
    CmdCastAbilityDirectional(_pendingAbility.ID, targetPoint, direction);
}

private void CancelAiming() {
    _combatState = CombatState.Idle;
    _pendingAbility = null;

    if (indicatorSystem != null) {
        indicatorSystem.HideIndicator();
    }
}
```

#### Nuevo ServerRpc Direccional

```csharp
[ServerRpc]
private void CmdCastAbilityDirectional(int abilityId, Vector3 targetPoint, Vector3 direction) {
    if (AbilityDatabase.Instance == null) return;

    AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
    if (ability == null) return;

    // Validaciones de servidor
    if (stats.CurrentMana < ability.ManaCost) {
        RpcCastFailed(base.Owner, "No mana");
        return;
    }

    if (!stats.ConsumeMana(ability.ManaCost)) {
        RpcCastFailed(base.Owner, "No mana");
        return;
    }

    // VALIDACIÓN DE DISTANCIA (Anti-cheat)
    float distanceToTarget = Vector3.Distance(transform.position, targetPoint);
    if (distanceToTarget > ability.Range * 1.2f) { // 20% tolerance
        RpcCastFailed(base.Owner, "Too far");
        return;
    }

    // EJECUTAR LÓGICA (con dirección)
    if (ability.Logic != null) {
        ability.Logic.ExecuteDirectional(base.NetworkObject, targetPoint, direction, ability);
    }

    RpcCastSuccess(abilityId);
}
```

---

### 4. Indicator System

**Archivo**: `Assets/_Project/2_Simulation/Combat/Abilities/AbilityIndicatorSystem.cs`

```csharp
public class AbilityIndicatorSystem : MonoBehaviour {

    [Header("Prefabs")]
    [SerializeField] private GameObject lineIndicatorPrefab;
    [SerializeField] private GameObject circleIndicatorPrefab;
    [SerializeField] private GameObject coneIndicatorPrefab;
    [SerializeField] private GameObject arrowIndicatorPrefab;
    [SerializeField] private GameObject trapIndicatorPrefab;

    private AbilityIndicator _currentIndicator;
    private Camera _mainCamera;

    void Awake() {
        _mainCamera = Camera.main;
    }

    public void ShowIndicator(AbilityData ability, Transform playerTransform) {
        HideIndicator();

        GameObject prefab = GetPrefabForAbility(ability);
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, playerTransform.position, Quaternion.identity);
        _currentIndicator = instance.GetComponent<AbilityIndicator>();

        if (_currentIndicator != null) {
            _currentIndicator.Initialize(ability);
            _currentIndicator.transform.SetParent(playerTransform);
            _currentIndicator.Show();
        }
    }

    public void HideIndicator() {
        if (_currentIndicator != null) {
            _currentIndicator.Hide();
            Destroy(_currentIndicator.gameObject);
            _currentIndicator = null;
        }
    }

    public void UpdateIndicator(Vector2 screenPosition) {
        if (_currentIndicator == null || _mainCamera == null) return;

        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);

        // Ground raycast
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMask.GetMask("Environment"))) {
            Vector3 direction = (hit.point - _currentIndicator.transform.position).normalized;
            _currentIndicator.UpdatePosition(hit.point, direction);
        }
    }

    public AbilityIndicator GetCurrentIndicator() => _currentIndicator;

    private GameObject GetPrefabForAbility(AbilityData ability) {
        switch (ability.IndicatorType) {
            case IndicatorType.Line:
                return lineIndicatorPrefab;

            case IndicatorType.Circle:
                return circleIndicatorPrefab;

            case IndicatorType.Cone:
                return coneIndicatorPrefab;

            case IndicatorType.Arrow:
                return arrowIndicatorPrefab;

            case IndicatorType.Trap:
                return trapIndicatorPrefab;

            default:
                return null;
        }
    }
}
```

---

### 5. Base Indicator Class

**Archivo**: `Assets/_Project/2_Simulation/Combat/Abilities/Indicators/AbilityIndicator.cs`

```csharp
public abstract class AbilityIndicator : MonoBehaviour {

    [Header("Visual Settings")]
    [SerializeField] protected Color validColor = new Color(0, 1, 0, 0.3f);
    [SerializeField] protected Color invalidColor = new Color(1, 0, 0, 0.3f);
    [SerializeField] protected LayerMask obstacleMask;

    protected bool _isActive;
    protected bool _isValid;

    // Interface
    public abstract void Initialize(AbilityData abilityData);
    public abstract void UpdatePosition(Vector3 worldPoint, Vector3 direction);
    public abstract Vector3 GetTargetPoint();
    public abstract Vector3 GetDirection();
    public abstract bool IsValid();

    public virtual void Show() {
        _isActive = true;
        gameObject.SetActive(true);
    }

    public virtual void Hide() {
        _isActive = false;
        gameObject.SetActive(false);
    }
}
```

---

## 📦 NUEVOS ABILITY LOGIC TYPES

### TargetedLogic.cs (Legacy)

```csharp
[CreateAssetMenu(fileName = "Logic_Targeted", menuName = "Genesis/Combat/Logic/Targeted")]
public class TargetedLogic : AbilityLogic {

    public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {
        // Para targeted abilities, targetPoint es ignorado
        // La lógica se ejecuta directamente sobre el target

        // Buscar target en TargetingSystem del caster
        if (caster.TryGetComponent(out TargetingSystem targeting)) {
            NetworkObject target = targeting.CurrentTarget;

            if (target == null) {
                Debug.LogError($"[TargetedLogic] {data.Name} requires a target!");
                return;
            }

            // Apply damage/heal/buff según ability data
            if (data.BaseDamage > 0) {
                if (target.TryGetComponent(out IDamageable damageable)) {
                    damageable.TakeDamage(data.BaseDamage, caster);
                }
            }

            if (data.BaseHeal > 0) {
                if (target.TryGetComponent(out PlayerStats stats)) {
                    stats.RestoreHealth(data.BaseHeal);
                }
            }

            // VFX
            if (data.ImpactVFX != null) {
                GameObject vfx = Instantiate(data.ImpactVFX, target.transform.position, Quaternion.identity);
                FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                Destroy(vfx, 2f);
            }
        }
    }
}
```

### SkillshotLogic.cs

```csharp
[CreateAssetMenu(fileName = "Logic_Skillshot", menuName = "Genesis/Combat/Logic/Skillshot")]
public class SkillshotLogic : AbilityLogic {

    public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

        // Spawn point (idealmente desde "Hand_R" bone)
        Vector3 spawnPos = caster.transform.position + Vector3.up * 1.5f;

        if (data.ProjectilePrefab == null) {
            Debug.LogError($"Ability {data.Name} missing ProjectilePrefab!");
            return;
        }

        GameObject instance = Instantiate(data.ProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));

        if (instance.TryGetComponent(out ProjectileController controller)) {
            controller.Initialize(caster, data.BaseDamage, direction * data.ProjectileSpeed, data.Radius);
        }

        FishNet.InstanceFinder.ServerManager.Spawn(instance);
    }
}
```

### AOELogic.cs

```csharp
[CreateAssetMenu(fileName = "Logic_AOE", menuName = "Genesis/Combat/Logic/AOE")]
public class AOELogic : AbilityLogic {

    [Header("AOE Settings")]
    [SerializeField] private float impactDelay = 0f; // Para Meteorito: 1s

    public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

        if (impactDelay > 0) {
            // Spawn warning VFX
            // Wait delay
            // Apply damage
        } else {
            ApplyAOEEffect(caster, targetPoint, data);
        }
    }

    private void ApplyAOEEffect(NetworkObject caster, Vector3 targetPoint, AbilityData data) {
        // Spawn VFX
        if (data.ImpactVFX != null) {
            GameObject vfx = Instantiate(data.ImpactVFX, targetPoint, Quaternion.identity);
            FishNet.InstanceFinder.ServerManager.Spawn(vfx);
            Destroy(vfx, 3f);
        }

        // Detect all targets in radius
        Collider[] hits = Physics.OverlapSphere(targetPoint, data.Radius, LayerMask.GetMask("Enemy", "Player"));

        foreach (var hit in hits) {
            if (hit.TryGetComponent(out NetworkObject netObj)) {
                if (netObj == caster) continue;

                // Apply damage or heal
                if (data.BaseDamage > 0) {
                    if (hit.TryGetComponent(out IDamageable damageable)) {
                        damageable.TakeDamage(data.BaseDamage, caster);
                    }
                }

                if (data.BaseHeal > 0) {
                    if (hit.TryGetComponent(out PlayerStats stats)) {
                        stats.RestoreHealth(data.BaseHeal);
                    }
                }
            }
        }
    }
}
```

### SelfAOELogic.cs

```csharp
[CreateAssetMenu(fileName = "Logic_SelfAOE", menuName = "Genesis/Combat/Logic/Self AOE")]
public class SelfAOELogic : AbilityLogic {

    public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

        Vector3 casterPos = caster.transform.position;

        // Spawn VFX centered on caster
        if (data.CastVFX != null) {
            GameObject vfx = Instantiate(data.CastVFX, casterPos, Quaternion.identity);
            vfx.transform.SetParent(caster.transform);
            FishNet.InstanceFinder.ServerManager.Spawn(vfx);
            Destroy(vfx, 2f);
        }

        // Detect enemies in radius
        Collider[] hits = Physics.OverlapSphere(casterPos, data.Radius, LayerMask.GetMask("Enemy"));

        foreach (var hit in hits) {
            if (hit.TryGetComponent(out IDamageable damageable)) {
                damageable.TakeDamage(data.BaseDamage, caster);
            }
        }

        Debug.Log($"[SelfAOELogic] Hit {hits.Length} targets");
    }
}
```

### DashLogic.cs

```csharp
[CreateAssetMenu(fileName = "Logic_Dash", menuName = "Genesis/Combat/Logic/Dash")]
public class DashLogic : AbilityLogic {

    [Header("Dash Settings")]
    [SerializeField] private bool isBackwards = false;
    [SerializeField] private bool canDashThroughEnemies = false;

    public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

        // Invertir dirección si es backwards (Desenganche)
        if (isBackwards) {
            direction = -direction;
            targetPoint = caster.transform.position + direction * data.Range;
        }

        // Validar destino (debe haber suelo)
        if (!Physics.Raycast(targetPoint + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, LayerMask.GetMask("Environment"))) {
            Debug.LogWarning("[DashLogic] Invalid destination");
            return;
        }

        Vector3 finalPosition = hit.point + Vector3.up * 0.5f;

        // Teleport
        caster.transform.position = finalPosition;

        // VFX trail
        if (data.CastVFX != null) {
            GameObject vfx = Instantiate(data.CastVFX, caster.transform.position, Quaternion.identity);
            FishNet.InstanceFinder.ServerManager.Spawn(vfx);
            Destroy(vfx, 1f);
        }

        // Optional: Damage enemies in path
        if (canDashThroughEnemies && data.BaseDamage > 0) {
            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, finalPosition);

            if (Physics.SphereCast(startPos, 1f, direction, out RaycastHit enemyHit, distance, LayerMask.GetMask("Enemy"))) {
                if (enemyHit.collider.TryGetComponent(out IDamageable damageable)) {
                    damageable.TakeDamage(data.BaseDamage, caster);
                }
            }
        }
    }
}
```

### ConeLogic.cs

```csharp
[CreateAssetMenu(fileName = "Logic_Cone", menuName = "Genesis/Combat/Logic/Cone")]
public class ConeLogic : AbilityLogic {

    public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

        Vector3 casterPos = caster.transform.position;
        float halfAngle = data.Angle / 2f;

        // VFX
        if (data.CastVFX != null) {
            GameObject vfx = Instantiate(data.CastVFX, casterPos, Quaternion.LookRotation(direction));
            FishNet.InstanceFinder.ServerManager.Spawn(vfx);
            Destroy(vfx, 2f);
        }

        // Detect all enemies in sphere
        Collider[] hits = Physics.OverlapSphere(casterPos, data.Range, LayerMask.GetMask("Enemy"));

        foreach (var hit in hits) {
            Vector3 dirToTarget = (hit.transform.position - casterPos).normalized;
            float angleToTarget = Vector3.Angle(direction, dirToTarget);

            // Check if inside cone
            if (angleToTarget <= halfAngle) {
                if (hit.TryGetComponent(out IDamageable damageable)) {
                    damageable.TakeDamage(data.BaseDamage, caster);
                }
            }
        }
    }
}
```

### TrapLogic.cs

```csharp
[CreateAssetMenu(fileName = "Logic_Trap", menuName = "Genesis/Combat/Logic/Trap")]
public class TrapLogic : AbilityLogic {

    [Header("Trap Settings")]
    [SerializeField] private GameObject trapPrefab;
    [SerializeField] private float trapLifetime = 30f;

    public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

        if (trapPrefab == null) {
            Debug.LogError($"Ability {data.Name} missing trapPrefab!");
            return;
        }

        // Spawn trap entity
        GameObject trap = Instantiate(trapPrefab, targetPoint, Quaternion.identity);

        if (trap.TryGetComponent(out TrapController controller)) {
            controller.Initialize(caster, data.BaseDamage, data.Radius, trapLifetime);
        }

        FishNet.InstanceFinder.ServerManager.Spawn(trap);
    }
}
```

---

## 📝 IMPLEMENTATION CHECKLIST

### ✅ **WAVE 1: Core Foundation**

- [ ] Actualizar `AbilityData.cs`:
  - [ ] Agregar enum `IndicatorType`
  - [ ] Agregar field `Angle` (para Cone)

- [ ] Actualizar `AbilityLogic.cs`:
  - [ ] Agregar método `ExecuteDirectional()`
  - [ ] Mantener método legacy `Execute()`

- [ ] Crear `AbilityIndicator.cs` (base class)

- [ ] Refactorizar `PlayerCombat.cs`:
  - [ ] Agregar enum `CombatState`
  - [ ] Implementar `HandleAimingInput()`
  - [ ] Crear `CmdCastAbilityDirectional()` ServerRpc
  - [ ] Mantener `CmdCastAbility()` legacy para targeted

### ✅ **WAVE 2: Indicators**

- [ ] **LineIndicator.cs**:
  - [ ] LineRenderer setup
  - [ ] Obstacle detection
  - [ ] Valid/invalid color feedback

- [ ] **CircleIndicator.cs**:
  - [ ] Cylinder mesh (plane)
  - [ ] Ground raycast
  - [ ] Distance validation

- [ ] **ArrowIndicator.cs**:
  - [ ] Path line renderer
  - [ ] Arrow model 3D
  - [ ] Backwards mode (Desenganche)

- [ ] **ConeIndicator.cs**:
  - [ ] Fan-shaped mesh
  - [ ] Angle configuration
  - [ ] Enemy count display

- [ ] **TrapIndicator.cs**:
  - [ ] Circle + trap model preview
  - [ ] Placement validation

- [ ] **AbilityIndicatorSystem.cs**:
  - [ ] Manager logic
  - [ ] Prefab instantiation
  - [ ] Mouse position tracking

### ✅ **WAVE 3: Ability Logic**

- [ ] **TargetedLogic.cs** (Legacy - 14 habilidades)
- [ ] **SkillshotLogic.cs** (Bola de Fuego)
- [ ] **AOELogic.cs** (Meteorito, Sagrario, Salva)
- [ ] **SelfAOELogic.cs** (Torbellino, Nova)
- [ ] **DashLogic.cs** (Carga, Desenganche)
- [ ] **ConeLogic.cs** (Multidisparo)
- [ ] **TrapLogic.cs** (Trampa de Hielo)
- [ ] **ChannelLogic.cs** (Rayo de Hielo) - Opcional Fase 7

### ✅ **WAVE 4: Prefabs & Assets**

- [ ] Crear prefabs de indicadores:
  - [ ] `Indicator_Line.prefab`
  - [ ] `Indicator_Circle.prefab`
  - [ ] `Indicator_Arrow.prefab`
  - [ ] `Indicator_Cone.prefab`
  - [ ] `Indicator_Trap.prefab`

- [ ] Actualizar AbilityData assets (24 habilidades):
  - [ ] 14 Targeted: `IndicatorType = None`
  - [ ] 10 Skillshot: `IndicatorType = Line/Circle/Cone/Arrow/Trap`

### ✅ **WAVE 5: Testing**

- [ ] Test Targeted abilities (legacy - NO cambios)
- [ ] Test Bola de Fuego (Line skillshot)
- [ ] Test Meteorito (Circle AOE ground)
- [ ] Test Torbellino (Circle AOE self)
- [ ] Test Carga (Arrow forward)
- [ ] Test Desenganche (Arrow backward)
- [ ] Test Multidisparo (Cone)
- [ ] Test Trampa de Hielo (Trap)
- [ ] Test multi-cliente (ParrelSync)
- [ ] Test casos edge (cancelación, inputs durante aiming)

---

## 🎯 RESULTADO ESPERADO

Al completar la Fase 6, el juego tendrá:

1. **Sistema Híbrido Funcional**:
   - 14 habilidades Targeted (sistema legacy intacto)
   - 10 habilidades Skillshot (nuevo sistema de apuntado)

2. **Input Flow Completo**:
   ```
   Press "1" (Bola de Fuego) → LineIndicator aparece → Mouse aim → Click → Proyectil vuela
   Press "2" (Golpe Rápido) → Instant cast (requiere target seleccionado)
   ```

3. **8 Tipos de Targeting Soportados**:
   - Targeted (legacy)
   - Line Skillshot
   - Line Channel
   - Circle Ground AOE
   - Circle Self AOE
   - Cone
   - Dash (forward/backward)
   - Trap

4. **Extensibilidad**:
   - Fácil agregar nuevas habilidades
   - Data-driven (no code para balanceo)
   - Modular (nuevos indicators/logics sin romper existentes)

---

## ⏱️ ESTIMACIÓN

| Wave | Tareas | Tiempo Estimado |
|------|--------|-----------------|
| 1 | Core Foundation | 4-6 horas |
| 2 | Indicators | 6-8 horas |
| 3 | Ability Logic | 4-6 horas |
| 4 | Prefabs & Assets | 2-3 horas |
| 5 | Testing & Polish | 4-6 horas |
| **TOTAL** | | **~20-29 horas** |

---

## ⚠️ RIESGOS

| Riesgo | Mitigación |
|--------|------------|
| Desincronización de indicadores | Solo cliente owner actualiza, servidor valida |
| Performance (100 jugadores) | Object pooling + LOD culling |
| Input conflicts (UI vs Game) | EventSystem check + Input context |
| Backward compatibility | Mantener sistema legacy intacto |

---

## 📚 REFERENCIAS

- `Assets/CLAUDE.md` - Context principal del proyecto
- `Assets/_Project/SETUP_INSTRUCTIONS.md` - Fase 1
- `Assets/_Project/FASE2_SETUP.md` - Player movement
- `Assets/_Project/FASE3_SETUP.md` - Targeting system
- `Assets/_Project/FASE4_SETUP.md` - Data pipeline
- `Assets/_Project/FASE5_SETUP.md` - Combat core
- `Assets/_Project/ABILITIES.md` - Listado completo de habilidades

---

**Fecha**: 2026-01-22
**Versión**: 1.0
**Estado**: Diseño completo - Listo para implementación
