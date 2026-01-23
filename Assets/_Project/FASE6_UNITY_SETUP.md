# FASE 6 - SKILLSHOT SYSTEM - SETUP MANUAL EN UNITY

## ✅ CÓDIGO COMPLETADO

Todo el código del sistema de skillshots ha sido implementado. Ahora necesitas configurar los prefabs y assets en Unity.

---

## 📦 PARTE 1: CREAR PREFABS DE INDICADORES

### 1.1 LineIndicator Prefab (Skillshot)

**Ubicación**: `Assets/_Project/5_Content/Prefabs/Indicators/Indicator_Line.prefab`

**Estructura**:
```
Indicator_Line (Empty GameObject)
├── LineIndicator.cs (script)
├── LineRenderer (Component)
│   ├── Positions: 2
│   ├── Width: 1.0
│   ├── Material: Material transparente verde/rojo
│   └── Color Gradient: Verde (inicio) → Verde (fin)
│
└── EndMarker (Sphere)
    ├── Scale: (0.5, 0.5, 0.5)
    ├── Material: Mismo que LineRenderer
    └── Collider: REMOVER (no debe interferir con clicks)
```

**Configuración del Script LineIndicator**:
- `Line Renderer`: Arrastra el LineRenderer
- `End Marker`: Arrastra la esfera
- `Default Width`: 1.0
- `Valid Color`: Verde (0, 1, 0, 0.3)
- `Invalid Color`: Rojo (1, 0, 0, 0.3)
- `Obstacle Mask`: Environment (Layer 8)

---

### 1.2 CircleIndicator Prefab (AOE)

**Ubicación**: `Assets/_Project/5_Content/Prefabs/Indicators/Indicator_Circle.prefab`

**Estructura**:
```
Indicator_Circle (Empty GameObject)
├── CircleIndicator.cs (script)
│
└── CirclePlane (Cylinder)
    ├── Scale: (2, 0.05, 2) [se ajusta dinámicamente]
    ├── Rotation: (0, 0, 0)
    ├── Material: Material transparente verde/rojo
    └── Collider: REMOVER
```

**Configuración del Script CircleIndicator**:
- `Circle Prefab`: Arrastra el Cylinder
- `Circle Renderer`: Arrastra el MeshRenderer del Cylinder
- `Height Offset`: 0.1
- `Is Self Centered`: FALSE (se configura automáticamente)
- `Max Distance`: 30
- `Valid Color`: Verde (0, 1, 0, 0.3)
- `Invalid Color`: Rojo (1, 0, 0, 0.3)

---

### 1.3 ArrowIndicator Prefab (Dash)

**Ubicación**: `Assets/_Project/5_Content/Prefabs/Indicators/Indicator_Arrow.prefab`

**Estructura**:
```
Indicator_Arrow (Empty GameObject)
├── ArrowIndicator.cs (script)
│
├── PathLine (LineRenderer)
│   ├── Positions: 2
│   ├── Width: 0.2
│   └── Material: Material transparente amarillo
│
└── ArrowModel (GameObject)
    ├── Modelo 3D de flecha (puede ser un Cone rotado)
    ├── Scale: (0.5, 0.5, 1.5)
    └── Material: Mismo color que PathLine
```

**Configuración del Script ArrowIndicator**:
- `Arrow Model`: Arrastra el modelo de flecha
- `Path Line`: Arrastra el LineRenderer
- `Arrow Renderer`: Arrastra el MeshRenderer de la flecha
- `Is Backwards`: FALSE (se configura automáticamente)
- `Max Dash Distance`: 15
- `Valid Color`: Verde (0, 1, 0, 0.5)
- `Invalid Color`: Rojo (1, 0, 0, 0.5)
- `Obstacle Mask`: Environment (Layer 8)

---

### 1.4 ConeIndicator Prefab (Frontal Area)

**Ubicación**: `Assets/_Project/5_Content/Prefabs/Indicators/Indicator_Cone.prefab`

**Estructura**:
```
Indicator_Cone (Empty GameObject)
├── ConeIndicator.cs (script)
│
└── ConeMesh (GameObject)
    ├── MeshFilter (generado por script)
    ├── MeshRenderer
    │   └── Material: Material transparente naranja
    └── No Collider
```

**Configuración del Script ConeIndicator**:
- `Cone Mesh Filter`: Arrastra el MeshFilter
- `Cone Renderer`: Arrastra el MeshRenderer
- `Segments`: 20 (resolución del cono)
- `Valid Color`: Naranja (1, 0.6, 0, 0.3)
- `Invalid Color`: Rojo (1, 0, 0, 0.3)

**NOTA**: El mesh del cono se genera proceduralmente en Initialize().

---

### 1.5 TrapIndicator Prefab (Trampa)

**Ubicación**: `Assets/_Project/5_Content/Prefabs/Indicators/Indicator_Trap.prefab`

**Estructura**:
```
Indicator_Trap (Empty GameObject)
├── TrapIndicator.cs (script)
│
├── CirclePlane (Cylinder - área de trigger)
│   ├── Scale: (2, 0.05, 2)
│   ├── Material: Material transparente azul
│   └── No Collider
│
└── TrapModelPreview (Modelo 3D de trampa)
    ├── Modelo de trampa (ej: spike trap, ice trap)
    ├── Transparencia: 50%
    └── No Collider
```

**Configuración del Script TrapIndicator**:
- `Circle Prefab`: Arrastra el Cylinder
- `Trap Model Preview`: Arrastra el modelo de trampa
- `Circle Renderer`: Arrastra el renderer del círculo
- `Height Offset`: 0.1
- `Max Placement Distance`: 5
- `Valid Color`: Azul (0, 0.5, 1, 0.3)
- `Invalid Color`: Rojo (1, 0, 0, 0.3)

---

## 📦 PARTE 2: CONFIGURAR PLAYER PREFAB

### 2.1 Agregar AbilityIndicatorSystem al Player

**Archivo**: `Assets/_Project/5_Content/Prefabs/Player/Player.prefab`

**Pasos**:
1. Abre el prefab `Player` en el Inspector
2. Agrega componente: `AbilityIndicatorSystem` (script)
3. Configura referencias en el Inspector:
   - **Line Indicator Prefab**: Arrastra `Indicator_Line.prefab`
   - **Circle Indicator Prefab**: Arrastra `Indicator_Circle.prefab`
   - **Cone Indicator Prefab**: Arrastra `Indicator_Cone.prefab`
   - **Arrow Indicator Prefab**: Arrastra `Indicator_Arrow.prefab`
   - **Trap Indicator Prefab**: Arrastra `Indicator_Trap.prefab`

### 2.2 Configurar PlayerCombat

**Componente**: `PlayerCombat` (ya existe en el Player)

**Actualizar Inspector**:
- **Stats**: Arrastra `PlayerStats` (ya debería estar)
- **Targeting**: Arrastra `TargetingSystem` (ya debería estar)
- **Animator**: Arrastra el `Animator` (si tienes)
- **Indicator System**: Arrastra el `AbilityIndicatorSystem` que acabas de agregar ⭐ NUEVO

---

## 📦 PARTE 3: CREAR ABILITY LOGIC ASSETS

### 3.1 Crear Logic Assets

**Ubicación**: `Assets/_Project/1_Data/Abilities/Logic/`

Crea los siguientes ScriptableObjects:

1. **Logic_Targeted** (para habilidades tab-target)
   - Right Click en `Logic/` > `Create > Genesis > Combat > Logic > Targeted`
   - Nombre: `Logic_Targeted`

2. **Logic_Skillshot** (para Bola de Fuego)
   - Right Click > `Create > Genesis > Combat > Logic > Skillshot`
   - Nombre: `Logic_Skillshot`

3. **Logic_AOE** (para Meteorito, Sagrario, Salva)
   - Right Click > `Create > Genesis > Combat > Logic > AOE`
   - Nombre: `Logic_AOE_Damage`
   - **Impact Delay**: 1.0 (para Meteorito)
   - **Affects Enemies**: TRUE
   - **Affects Allies**: FALSE

   - Duplica este asset para crear `Logic_AOE_Heal`:
     - **Impact Delay**: 0
     - **Affects Enemies**: FALSE
     - **Affects Allies**: TRUE

4. **Logic_SelfAOE** (para Torbellino, Nova)
   - Right Click > `Create > Genesis > Combat > Logic > Self AOE`
   - Nombre: `Logic_SelfAOE`
   - **Include Self**: FALSE

5. **Logic_Dash** (para Carga, Desenganche)
   - Right Click > `Create > Genesis > Combat > Logic > Dash`
   - Nombre: `Logic_Dash_Forward`
   - **Is Backwards**: FALSE
   - **Can Dash Through Enemies**: FALSE
   - **Apply Damage In Path**: TRUE

   - Duplica para crear `Logic_Dash_Backward` (Desenganche):
     - **Is Backwards**: TRUE
     - **Apply Damage In Path**: FALSE

6. **Logic_Cone** (para Multidisparo)
   - Right Click > `Create > Genesis > Combat > Logic > Cone`
   - Nombre: `Logic_Cone`
   - **Requires Line Of Sight**: FALSE

7. **Logic_Trap** (para Trampa de Hielo)
   - Right Click > `Create > Genesis > Combat > Logic > Trap`
   - Nombre: `Logic_Trap`
   - **Trap Prefab**: (crear después - ver Parte 4)
   - **Trap Lifetime**: 30
   - **Visible To Enemies**: TRUE

---

## 📦 PARTE 4: ACTUALIZAR ABILITY DATA ASSETS

### 4.1 Actualizar Habilidades Targeted (14 habilidades)

Para todas estas habilidades, configura:
- **Indicator Type**: `None`

**Lista**:
```
Guerrero:  Golpe Rápido, Reflejo, Empalamiento, Fortificar
Mago:      Daga de Maná, Armadura Arcana
Sacerdote: Punición, Luz Sanadora, Escudo Sagrado, Rezo de Fe, Ira de Dios
Cazador:   Tiro Firme, Ojo de Halcón 
```

**Ejemplo: Ability_GolpeRapido.asset**:
```yaml
# Identity
ID: 101
Name: Golpe Rápido

# Logic
Logic: Logic_Targeted ⭐ Asignar

# Targeting
Targeting Mode: Enemy
Indicator Type: None ⭐ NUEVO CAMPO
Range: 2
Radius: 0

# Combat
Base Damage: 25
```

---

### 4.2 Configurar Habilidades Skillshot

#### **Bola de Fuego** (Mago)

**Archivo**: `Ability_BolaFuego.asset`

```yaml
# Identity
ID: 1001
Name: Bola de Fuego

# Logic
Logic: Logic_Skillshot ⭐ Asignar

# Targeting
Targeting Mode: Ground
Indicator Type: Line ⭐ NUEVO
Range: 25
Radius: 0.5
Angle: 0

# Projectile
Projectile Prefab: (tu prefab de Fireball)
Projectile Speed: 20

# Combat
Base Damage: 50
```

---

#### **Meteorito** (Mago)

**Archivo**: `Ability_Meteorito.asset`

```yaml
# Identity
ID: 1002
Name: Meteorito

# Logic
Logic: Logic_AOE_Damage ⭐ Asignar

# Targeting
Targeting Mode: Ground
Indicator Type: Circle ⭐ NUEVO
Range: 20
Radius: 4
Angle: 0

# Combat
Base Damage: 150
```

---

#### **Sagrario** (Sacerdote - AOE Heal)

**Archivo**: `Ability_Sagrario.asset`

```yaml
# Logic
Logic: Logic_AOE_Heal ⭐ Asignar

# Targeting
Indicator Type: Circle ⭐
Range: 15
Radius: 5

# Combat
Base Damage: 0
Base Heal: 60 ⭐
```

---

#### **Torbellino** (Guerrero - Self AOE)

**Archivo**: `Ability_Torbellino.asset`

```yaml
# Logic
Logic: Logic_SelfAOE ⭐

# Targeting
Targeting Mode: Self
Indicator Type: Circle ⭐
Range: 0
Radius: 5

# Combat
Base Damage: 40
```

---

#### **Carga** (Guerrero - Dash Forward)

**Archivo**: `Ability_Carga.asset`

```yaml
# Logic
Logic: Logic_Dash_Forward ⭐

# Targeting
Targeting Mode: None
Indicator Type: Arrow ⭐
Range: 15
Radius: 1.0

# Combat
Base Damage: 30
```

---

#### **Desenganche** (Cazador - Dash Backward)

**Archivo**: `Ability_Desenganche.asset`

```yaml
# Logic
Logic: Logic_Dash_Backward ⭐

# Targeting
Indicator Type: Arrow ⭐
Range: 8
Radius: 1.0

# Combat
Base Damage: 0
```

---

#### **Multidisparo** (Cazador - Cone)

**Archivo**: `Ability_Multidisparo.asset`

```yaml
# Logic
Logic: Logic_Cone ⭐

# Targeting
Targeting Mode: Ground
Indicator Type: Cone ⭐
Range: 15
Radius: 0
Angle: 60 ⭐ NUEVO CAMPO

# Combat
Base Damage: 35
```

---

#### **Trampa de Hielo** (Cazador)

**Archivo**: `Ability_TrampaHielo.asset`

```yaml
# Logic
Logic: Logic_Trap ⭐

# Targeting
Indicator Type: Trap ⭐
Range: 5
Radius: 2

# Combat
Base Damage: 40
```

---

## 📦 PARTE 5: CREAR TRAP PREFAB

### 5.1 Crear Prefab de Trampa de Hielo

**Ubicación**: `Assets/_Project/5_Content/Prefabs/Traps/Trap_Ice.prefab`

**Estructura**:
```
Trap_Ice (GameObject)
├── NetworkObject (FishNet)
│   └── Is Networked: TRUE
│
├── SphereCollider
│   ├── Is Trigger: TRUE
│   ├── Radius: 2 (ajustado por script)
│   └── Layer: Default
│
├── TrapController.cs (script)
│
└── Model (Visual)
    ├── Modelo 3D de trampa de hielo
    ├── Partículas de hielo (opcional)
    └── No collider (solo visual)
```

**Configuración del Script TrapController**:
- `Model`: Arrastra el modelo visual
- `Trigger VFX`: (opcional) Partículas de explosión de hielo

**Asignar en Logic_Trap**:
- Vuelve a `Logic_Trap.asset`
- **Trap Prefab**: Arrastra `Trap_Ice.prefab`

---

## 📦 PARTE 6: CONFIGURAR ABILITY SLOTS EN PLAYER

### 6.1 Asignar Habilidades de Prueba

**Prefab**: `Player.prefab`
**Componente**: `PlayerCombat`
**Inspector**: Sección `Ability Slots`

**Configuración de prueba (Mago)**:
- **Size**: 6
- **Element 0**: `Ability_DagaMana` (Targeted)
- **Element 1**: `Ability_BolaFuego` (Skillshot) ⭐
- **Element 2**: `Ability_Meteorito` (Circle AOE) ⭐
- **Element 3**: `Ability_ArmaduraArcana` (Targeted Self)
- **Element 4**: (vacío)
- **Element 5**: (vacío)

---

## 🧪 PARTE 7: TESTING

### Test 1: Habilidad Targeted (Sistema Legacy)

1. Play en Unity
2. Selecciona un enemigo (click izquierdo o Tab)
3. Presiona **1** (Daga de Maná)
4. ✅ Debería castear inmediatamente hacia el target

### Test 2: Skillshot (Bola de Fuego)

1. Play
2. Presiona **2** (Bola de Fuego)
3. ✅ Debería aparecer LineIndicator (línea verde)
4. Mueve el mouse → La línea sigue al mouse
5. Click izquierdo → Proyectil sale en esa dirección
6. Click derecho → Cancela (línea desaparece)

### Test 3: AOE Ground (Meteorito)

1. Presiona **3** (Meteorito)
2. ✅ Debería aparecer CircleIndicator (círculo verde)
3. Mueve el mouse → Círculo sigue al mouse
4. Click izquierdo → Impacto en el área (con delay de 1s)

### Test 4: Cancelación

1. Presiona **2** (Bola de Fuego)
2. Aparece indicador
3. Presiona **Escape** → Indicador desaparece
4. Vuelves a estado Idle

### Test 5: Multi-Cliente

1. Abre ParrelSync clone
2. Play en ambos
3. Cliente 1: Lanza Bola de Fuego hacia Cliente 2
4. ✅ Cliente 2 debería ver el proyectil y recibir daño

---

## 🐛 TROUBLESHOOTING

### Problema: "AbilityIndicatorSystem is NULL!"

**Solución**: Verifica que en PlayerCombat → Indicator System esté asignado.

---

### Problema: Indicador no aparece

**Soluciones**:
1. Verifica que el Ability tenga `IndicatorType != None`
2. Verifica que el prefab de indicador esté asignado en AbilityIndicatorSystem
3. Revisa la consola para errores

---

### Problema: Click no confirma habilidad

**Soluciones**:
1. Verifica que el indicador esté en estado `IsValid() = true` (color verde)
2. Verifica que no haya UI bloqueando el raycast
3. Revisa que el suelo tenga Layer `Environment`

---

### Problema: Proyectil no spawna

**Soluciones**:
1. Verifica que el Ability tenga `ProjectilePrefab` asignado
2. Verifica que el prefab tenga componente `ProjectileController`
3. Verifica que el prefab tenga `NetworkObject`
4. Revisa que el prefab esté en la lista `DefaultPrefabs` de FishNet NetworkManager

---

## ✅ CHECKLIST FINAL

- [ ] Los 5 prefabs de indicadores creados y configurados
- [ ] AbilityIndicatorSystem agregado al Player prefab
- [ ] PlayerCombat tiene referencia al Indicator System
- [ ] 7 Logic assets creados (Targeted, Skillshot, AOE, SelfAOE, Dash x2, Cone, Trap)
- [ ] Todas las AbilityData actualizadas con IndicatorType
- [ ] Trap prefab creado con TrapController
- [ ] Ability Slots configurados en Player
- [ ] Test de targeted ability: ✅
- [ ] Test de skillshot: ✅
- [ ] Test de AOE: ✅
- [ ] Test multi-cliente: ✅

---

## 🎉 RESULTADO ESPERADO

Al completar todos los pasos:

1. **14 habilidades Targeted** funcionan igual que antes (sin cambios)
2. **Bola de Fuego** muestra línea, aim con mouse, click para lanzar
3. **Meteorito** muestra círculo, click para impacto AOE
4. **Torbellino** instant cast AOE alrededor del player
5. **Carga/Desenganche** muestran flecha, dash al confirmar
6. **Multidisparo** muestra cono, daño en área frontal
7. **Trampa de Hielo** coloca trampa que persiste hasta activarse

Sistema híbrido completamente funcional! 🚀

---

**Fecha**: 2026-01-22
**Versión**: 1.0
**Autor**: Claude Sonnet 4.5
