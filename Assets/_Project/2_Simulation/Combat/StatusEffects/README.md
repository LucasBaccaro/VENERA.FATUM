# STATUS EFFECTS SYSTEM - BUFFS & DEBUFFS

Sistema completo de Status Effects para Genesis (Fase 6). Server-authoritative, sincronizado automáticamente.

## 🚀 SETUP RÁPIDO

### 1. Crear los Status Effects (en Unity)
```
Tools > Genesis > Create Initial Status Effects
```
Esto crea 11 efectos en `Assets/_Project/1_Data/StatusEffects/`

### 2. Crear el Database (en Unity)
```
Tools > Genesis > Create Status Effect Database
```
Crea el database en `Assets/_Project/1_Data/Resources/Databases/StatusEffectDatabase.asset`

### 3. Agregar Component al Player Prefab
- Abrir `Player.prefab`
- `Add Component > Status Effect System`
- Guardar

### 4. Configurar Habilidad con Efectos (Ejemplo: Rayo de Hielo)
1. Abrir `Ability_Rayo de Hielo.asset` (o cualquier habilidad)
2. En **Status Effects > Apply To Target**:
   - Expandir el array
   - Arrastrar `Effect_Slow.asset`
3. Guardar
4. Al castear la habilidad, aplicará Slow a los enemigos golpeados

## 📊 EFECTOS DISPONIBLES

### Crowd Control
- **Stun** (ID 1): Bloquea movimiento y habilidades por 2s
- **Root** (ID 2): Bloquea movimiento por 3s
- **Slow** (ID 3): -50% velocidad por 5s
- **Silence** (ID 4): Bloquea habilidades mágicas por 4s

### Defensivos
- **Shield** (ID 10): Absorbe 50 HP por 10s
- **Reflect** (ID 11): Refleja proyectiles por 5s
- **Invulnerable** (ID 12): Inmune a todo por 3s

### DoT/HoT
- **Poison** (ID 20): 5 daño/tick cada 1s por 6s (stackeable x3)
- **Regen** (ID 21): +10 HP/tick cada 2s por 10s

### Velocidad
- **Speed** (ID 30): +30% velocidad movimiento por 8s
- **Haste** (ID 31): +25% velocidad cast/ataque por 12s

## 🎮 USO EN HABILIDADES

### Configurar una habilidad para aplicar efectos:
1. Seleccionar un `AbilityData` en el Inspector
2. **Status Effects > Apply To Target**: Arrastrar `Effect_Stun.asset`
3. **Status Effects > Apply To Self**: Arrastrar `Effect_Shield.asset` (si es buff)

El sistema los aplica automáticamente cuando la habilidad ejecuta.

## 🔍 API

### Server (solo en servidor)
```csharp
StatusEffectSystem statusSystem = GetComponent<StatusEffectSystem>();

// Aplicar efecto
statusSystem.ApplyEffect(StatusEffectData data);

// Remover efecto
statusSystem.RemoveEffect(int effectID);

// Remover todos los debuffs
statusSystem.RemoveAllDebuffs();
```

### Queries (cliente y servidor)
```csharp
// Verificar si tiene un efecto
bool isStunned = statusSystem.HasEffect(EffectType.Stun);

// Multiplicador de velocidad (1.0 = normal, 0.5 = 50% slower)
float speedMult = statusSystem.GetMovementSpeedMultiplier();

// Stacks de un efecto
int poisonStacks = statusSystem.GetStackCount(20);
```

## 🧪 TESTING

### ⚠️ IMPORTANTE ANTES DE TESTEAR
1. **VFX NO DISPONIBLES AÚN**: Los efectos no tienen VFX prefabs asignados. Se verán solo en consola con debug logs. Los VFX se crean en Fase 11.
2. **SOLO EN HOST**: Los tests solo funcionan si eres el HOST (servidor). En cliente solo verás los efectos sincronizados.

### Testing Rápido (Context Menu)
1. **Play Mode** como HOST
2. Seleccionar **Player en Hierarchy** (debe ser tu player local)
3. Buscar component **Status Effect System**
4. **Click derecho** en el component:
   - `Test: Apply Stun (2s)` - Quedas inmóvil por 2 segundos
   - `Test: Apply Slow (5s)` - Te mueves 50% más lento por 5 segundos
   - `Test: Apply Shield (10s)` - Obtienes 50 HP de escudo (verificar en Stats)
   - `Test: Apply Poison (6s)` - Pierdes 5 HP por segundo durante 6s
   - `Test: Remove All Effects` - Limpia todos los efectos

5. Para verificar efectos activos:
   - `Debug: List Active Effects` - Muestra en consola

### Verificar que Slow funciona
```
1. Apply Slow
2. Muévete con WASD
3. Deberías moverte 50% más lento
4. Espera 5 segundos
5. Deberías volver a velocidad normal
```

### Testing con otro Player
1. **Host**: Aplicar efecto a otro player via habilidad (ej: Bola de Fuego con Poison)
2. **Cliente**: Debería ver el efecto aplicado (sincronizado automáticamente)
3. **NOTA**: El otro player necesita tener `StatusEffectSystem` component

## 🔧 INTEGRACIÓN

### PlayerMotorMultiplayer
- ✅ Stun/Root bloquean movimiento
- ✅ Slow/Speed afectan velocidad
- **NOTA**: Solo funciona si el entity tiene `PlayerMotorMultiplayer` component

### PlayerCombat
- ✅ Stun bloquea habilidades
- ✅ Silence bloquea habilidades mágicas
- **NOTA**: Solo funciona si el entity tiene `PlayerCombat` component

### ProjectileController
- ✅ Reflect rebota proyectiles al atacante

## 🎯 APLICAR EFECTOS DESDE CÓDIGO

### Desde una habilidad (AbilityLogic)
```csharp
// Ejemplo en ProjectileLogic.Execute():
public override void Execute(NetworkObject caster, NetworkObject target, Vector3 groundPoint, AbilityData data) {
    // ... tu lógica de proyectil ...

    // Aplicar efectos al target
    if (target != null && data.ApplyToTarget != null && data.ApplyToTarget.Length > 0) {
        StatusEffectSystem statusSystem = target.GetComponent<StatusEffectSystem>();
        if (statusSystem != null) {
            foreach (var effectData in data.ApplyToTarget) {
                statusSystem.ApplyEffect(effectData);
                Debug.Log($"Applied {effectData.Name} to {target.name}");
            }
        } else {
            Debug.LogWarning($"Target {target.name} has no StatusEffectSystem!");
        }
    }
}
```

### Desde código manual (server-side)
```csharp
[Server]
void ApplySlowToTarget(NetworkObject target) {
    StatusEffectData slowData = StatusEffectDatabase.Instance.GetEffect(3); // ID 3 = Slow
    StatusEffectSystem targetStatus = target.GetComponent<StatusEffectSystem>();

    if (targetStatus != null && slowData != null) {
        targetStatus.ApplyEffect(slowData);
    }
}
```

## 📝 NOTAS

- **Server Authority**: Solo el servidor puede aplicar/remover efectos
- **Stacking**: Solo Poison es stackeable (hasta 3x). Los demás refrescan duración.
- **Invulnerable**: Bloquea todos los debuffs, permite buffs
- **VFX**: Si asignas un VFXPrefab en el StatusEffectData, se spawneará automáticamente sobre la cabeza del target
- **Integración Automática**: Todas las habilidades (Projectile, Channel, AOE, Targeted, Cone, SelfAOE, Dash) aplican automáticamente los efectos configurados en `ApplyToTarget` y `ApplyToSelf`

## ✅ ARCHIVOS ACTUALIZADOS (Integración Completa)

Estos archivos fueron modificados para soportar Status Effects:

### Combat System
- ✅ `StatusEffectSystem.cs` - Manager principal (NUEVO)
- ✅ `ProjectileController.cs` - Aplica efectos al impactar + Reflect
- ✅ `PlayerMotorMultiplayer.cs` - Stun/Root/Slow/Speed afectan movimiento
- ✅ `PlayerCombat.cs` - Stun/Silence bloquean habilidades

### Ability Logics (Todos actualizados)
- ✅ `ChannelLogic.cs` - Aplica efectos por tick
- ✅ `ProjectileLogic.cs` - Pasa efectos al proyectil
- ✅ `AOELogic.cs` - Aplica efectos en área
- ✅ `TargetedLogic.cs` - Aplica efectos al target + self buffs
- ✅ `SelfAOELogic.cs` - Aplica efectos en área alrededor del caster
- ✅ `ConeLogic.cs` - Aplica efectos en cono
- ✅ `DashLogic.cs` - Aplica self-buffs durante dash

### Database
- ✅ `StatusEffectDatabase.cs` - Registry singleton (NUEVO)

## ⚙️ CONFIGURAR ENEMIES/DUMMIES PARA STATUS EFFECTS

Para que un enemy/dummy pueda recibir status effects:

### Setup Mínimo
1. El GameObject debe tener:
   - `NetworkObject` component (FishNet)
   - `StatusEffectSystem` component
   - `PlayerStats` component (o implementar `IDamageable`)

### Setup Completo (con integración de movimiento)
2. Para que Slow/Speed afecten movimiento:
   - Agregar `PlayerMotorMultiplayer` component
   - O crear un sistema de movimiento custom que consulte `StatusEffectSystem.GetMovementSpeedMultiplier()`

3. Para que Stun/Root funcionen:
   - En tu script de movimiento, verificar:
   ```csharp
   StatusEffectSystem statusEffects = GetComponent<StatusEffectSystem>();
   if (statusEffects != null && statusEffects.HasEffect(EffectType.Stun)) {
       return; // No permitir movimiento
   }
   ```

## 🐛 TROUBLESHOOTING

**"StatusEffectDatabase not found"**
→ Ejecutar `Tools > Genesis > Create Status Effect Database`

**"Effect ID X not found"**
→ Abrir database > Click derecho > `Auto-Find All Status Effects`

**Efectos no se aplican**
→ Verificar que el GameObject tiene `StatusEffectSystem` component
→ Verificar que el código se ejecuta en servidor (`[Server]` attribute)

**Slow no afecta al enemy**
→ Verificar que el enemy tiene `PlayerMotorMultiplayer` (o sistema custom integrado)
→ Verificar que el enemy tiene `StatusEffectSystem` component
→ Ejecutar `Debug: List Active Effects` en el enemy para verificar que el efecto está aplicado

**VFX no aparecen**
→ NORMAL: Los VFX prefabs no existen aún (Fase 11)
→ Por ahora solo verás debug logs en consola

**Error "server is not active"**
→ RESUELTO: Actualizar StatusEffectSystem.cs a última versión

## 📂 ARCHIVOS

```
2_Simulation/Combat/StatusEffects/
├── StatusEffectSystem.cs          # Manager component
└── README.md                       # Este archivo

1_Data/
├── Databases/
│   └── StatusEffectDatabase.cs    # Database singleton
└── StatusEffects/
    ├── CreateStatusEffects.cs     # Editor utility
    ├── CreateStatusEffectDatabase.cs
    └── Effect_*.asset             # 11 efectos (crear en Unity)
```

## 🎯 PRÓXIMOS PASOS

- [ ] Crear VFX prefabs (Fase 11)
- [ ] UI de buffs/debuffs (Fase 11)
- [ ] Integrar AudioManager (Fase 11)
- [ ] Crear más efectos específicos por clase
