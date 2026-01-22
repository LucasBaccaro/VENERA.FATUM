# FASE 4 - DATA PIPELINE - SETUP MANUAL

La infraestructura de código está lista. Ahora necesitas crear los Assets (ScriptableObjects) que definirán el contenido del juego.

## 📂 1. CREAR ESTRUCTURA DE CARPETAS DE DATOS

Organiza tu carpeta `Assets/_Project/1_Data/` así:

```
1_Data/
├── Databases/          <-- Aquí irá la AbilityDatabase
├── Abilities/          <-- Tus habilidades (Fireball, Heal, etc.)
│   ├── Mage/
│   ├── Warrior/
│   └── Logic/          <-- Scripts lógicos (Fase 5)
└── StatusEffects/      <-- Buffs y Debuffs
```

## 🗄️ 2. CREAR LA ABILITY DATABASE

1.  Ve a `Assets/_Project/1_Data/Databases/` (créala si no existe).
2.  Click derecho > `Create > Genesis > System > Ability Database`.
3.  Renómbralo a: `AbilityDatabase`.
4.  **IMPORTANTE:** Mueve este archivo a una carpeta `Resources/Databases/` para que el Singleton pueda cargarlo automáticamente, O asígnalo manualmente en tu EntryPoint (si tienes uno).
    *   *Opción Recomendada:* Crea `Assets/Resources/Databases/` y ponlo ahí.

## 🔮 3. CREAR TUS PRIMERAS HABILIDADES

Vamos a crear 2 habilidades de prueba. Aún no harán nada (falta la lógica), pero ya tendremos los datos.

### Habilidad 1: Fireball (Mago)
1.  Ve a `Assets/_Project/1_Data/Abilities/Mage/`.
2.  Click derecho > `Create > Genesis > Combat > Ability`.
3.  Nombre: `Ability_Fireball`.
4.  Configuración:
    *   **ID**: `1001`
    *   **Name**: `Fireball`
    *   **Mana Cost**: `20`
    *   **Cooldown**: `0.5`
    *   **Cast Type**: `Casting` (con Cast Time 1.5s)
    *   **Target Type**: `Enemy`
    *   **Range**: `25`
    *   **Base Damage**: `50`
    *   **Projectile Speed**: `20`

### Habilidad 2: Heal (Clérigo/Self)
1.  Crear `Ability_Heal`.
2.  Configuración:
    *   **ID**: `2001`
    *   **Name**: `Heal`
    *   **Mana Cost**: `15`
    *   **Cast Type**: `Instant`
    *   **Target Type**: `Self` (o Ally)
    *   **Base Heal**: `40`

## ⚡ 4. CREAR EFECTO DE PRUEBA (Buff)

1.  Ve a `Assets/_Project/1_Data/StatusEffects/`.
2.  Click derecho > `Create > Genesis > Combat > Status Effect`.
3.  Nombre: `Effect_SpeedBuff`.
4.  Configuración:
    *   **ID**: `1`
    *   **Name**: `Haste`
    *   **Type**: `Speed`
    *   **Is Buff**: `True`
    *   **Duration**: `5`
    *   **Percentage Value**: `0.3` (30% más rápido)

## 🔗 5. VINCULAR TODO

1.  Selecciona tu `AbilityDatabase` en Resources.
2.  En el Inspector, haz click en el botón (context menu o botón si aparece) **"Auto-Find All Abilities"** (Click en los 3 puntitos del script > Auto-Find...).
3.  Verifica que la lista `Abilities` se haya llenado con Fireball y Heal.

---

## ✅ CRITERIO DE ÉXITO FASE 4

*   Tienes los archivos `.asset` creados.
*   La Database tiene las referencias.
*   Puedes leer los datos desde un script de prueba (opcional).

Una vez hecho esto, estaremos listos para la **FASE 5: COMBAT CORE**, donde escribiremos el código (`AbilityLogic`) para que la Fireball realmente vuele y haga daño.
