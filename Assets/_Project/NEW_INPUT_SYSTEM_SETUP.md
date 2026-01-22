# NEW INPUT SYSTEM - CONFIGURACIÓN FINAL

## ✅ YA COMPLETADO (Scripts)
- **InputManager.cs**: Wrapper para el New Input System
- **PlayerController.cs**: Actualizado para usar InputManager

---

## 📝 PASOS MANUALES EN UNITY

### 1. Generar Clase C# del Input Actions

1. **En Unity**, abre el Project panel
2. Navega a: `Assets/`
3. Encuentra el archivo: **InputSystem_Actions.inputactions**
4. Click en él para seleccionarlo
5. En el **Inspector**, busca la sección superior
6. Activa la checkbox: **☑ Generate C# Class**
7. Click en **Apply** (aparecerá abajo)
8. Espera a que Unity recompile (1-2 segundos)

**Resultado:** Unity generará el archivo `InputSystem_Actions.cs` automáticamente.

---

### 2. Agregar InputManager a Bootstrap

1. Abre la escena **Bootstrap**
2. En Hierarchy, dentro de `[MANAGERS]`, crea un Empty GameObject
3. Renómbralo: **InputManager**
4. Agrega el script: **InputManager.cs**

---

### 3. Verificar Compilación

Vuelve a Unity y espera la compilación. Deberías ver:
- ✅ **0 Errors**
- ✅ Archivo generado: `InputSystem_Actions.cs` junto al .inputactions

---

## 🧪 TEST

1. Click **Play** en la escena Bootstrap
2. Una vez spawneado el jugador:
   - Presiona **WASD** o **Flechas**
   - Deberías ver al jugador moverse

---

## 🐛 TROUBLESHOOTING

### Error: "InputSystem_Actions does not exist"
**Solución:** Asegúrate de activar "Generate C# Class" en el Inspector del .inputactions

### Error: "Namespace 'UnityEngine.InputSystem' not found"
**Solución:** En Package Manager, verifica que `Input System` esté instalado (ya debería estarlo)

### El jugador no se mueve
**Solución:**
1. Verifica que InputManager esté en la escena Bootstrap
2. Verifica en consola que no haya errores
3. Verifica que el jugador tenga el script PlayerController

---

## ✅ SIGUIENTE PASO

Una vez que el input funcione correctamente, continúa con el resto de `FASE2_SETUP.md`:
- Crear prefab Player
- Configurar PlayerSpawnManager
- Test con 2 clientes

---

**Avísame cuando termines y te mueves correctamente con WASD!** 🎮
