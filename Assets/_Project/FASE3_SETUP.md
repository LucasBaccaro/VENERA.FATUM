# FASE 3 - TARGETING SYSTEM - SETUP MANUAL

El sistema de targeting ya está implementado en código. Ahora necesitas configurar los prefabs y la escena.

## ✅ YA COMPLETADO (Scripts)
- **TargetingSystem.cs**: Lógica de selección, ciclado y visualización.
- **HUDController.cs**: Actualizado para mostrar target frame (si existe en UI).
- **EventBus**: Conectado para eventos `OnTargetChanged`.

---

## 📋 CONFIGURACIÓN MANUAL EN UNITY

### 1. CREAR PREFAB VISUAL "TARGET RING"
Este es el círculo rojo que aparecerá bajo los pies del enemigo seleccionado.

1.  En la escena, crea un **Cylinder**: `GameObject > 3D Object > Cylinder`.
2.  Renómbralo a: `TargetRing`.
3.  Escálalo a: `(2, 0.05, 2)` (plano como un disco).
4.  **IMPORTANTE:** Elimina el componente **Capsule Collider** (para que no interfiera con clicks).
5.  Crea un Material nuevo en `Assets/_Project/5_Content/Materials/VFX/`:
    *   Nombre: `Mat_TargetRing`
    *   Color: Rojo brillante (#FF0000).
    *   (Opcional) Shader: Unlit/Transparent si quieres que se vea mejor.
6.  Asigna el material al cilindro.
7.  Convierte el objeto en Prefab: Arrástralo a `Assets/_Project/5_Content/Prefabs/VFX/`.
    *   *Si la carpeta VFX no existe, créala.*
8.  Borra el objeto de la escena.

### 2. CONFIGURAR EL PLAYER PREFAB
Ahora le daremos al jugador la capacidad de targetear.

1.  Abre el prefab **Player** (`Assets/_Project/5_Content/Prefabs/Player/Player.prefab`).
2.  Añade el componente: **TargetingSystem** (script).
3.  Configura en el Inspector:
    *   **Max Target Distance**: `40`
    *   **Target Layer**: `Enemy` (Asegúrate que sea Layer 6).
    *   **Ground Layer**: `Environment` (Layer 8).
    *   **Target Ring Prefab**: Arrastra el `TargetRing` que creaste en el paso 1.
    *   **Cursor Cross Prefab**: (Déjalo vacío por ahora).

### 3. CREAR ENEMIGO DUMMY
Necesitas algo a qué disparar/seleccionar.

1.  En la escena `Bootstrap` (o tu escena de test), crea una **Capsule**.
2.  Renómbrala: `DummyEnemy`.
3.  Posición: `(5, 1, 5)` (un poco lejos del centro).
4.  **IMPORTANTE:** Asigna su Layer a **Enemy** (Layer 6).
    *   *Si Unity pregunta "Change Children?", di "Yes, change children".*
5.  Agrega los componentes mínimos de red:
    *   `NetworkObject` (Is Networked: True).
    *   `NetworkTransform` (Client Authoritative: False).
    *   `PlayerStats` (Para tener vida).
6.  Crea un prefab de esto en `Assets/_Project/5_Content/Prefabs/Enemies/`.

---

## 🧪 TEST FINAL

1.  Dale Play (Host).
2.  Acércate al Dummy.
3.  **Click Izquierdo** sobre el Dummy -> Debería aparecer el anillo rojo en sus pies.
4.  Presiona **Escape** -> El anillo desaparece.
5.  Presiona **Tab** -> Debería seleccionarlo automáticamente.
6.  Mira la consola: Debería decir `[HUD] Target Selected: DummyEnemy`.

---

## 🐛 SOLUCIÓN DE PROBLEMAS

*   **No puedo seleccionar nada:**
    *   Verifica que el `DummyEnemy` tenga Layer `Enemy` (6).
    *   Verifica que `TargetingSystem` tenga `Target Layer` configurado a `Enemy`.
    *   Verifica que no haya UI bloqueando el raycast (el script ignora clicks en UI, pero a veces el Canvas bloquea).

*   **El anillo aparece muy arriba/abajo:**
    *   Ajusta la altura del `TargetRing` prefab o el offset en el script `TargetingSystem.cs` (línea 160).
