## 11. ROADMAP DE IMPLEMENTACIÓN

### FASE 1: FOUNDATION (Semana 1-2)
**Objetivo:** Infraestructura básica funcional

**Tasks:**
*   ✅ Crear estructura de carpetas completa
*   ✅ Configurar Assembly Definitions (.asmdef)
*   ✅ Configurar Layers y Collision Matrix
*   ✅ Crear escena Bootstrap con NetworkManager
*   ✅ Configurar FishNet (Tugboat transport)
*   ✅ Implementar ServiceLocator y EventBus
*   ✅ Crear ObjectPool genérico
*   ✅ Setup Nakama (Docker local)

**Criterio de Éxito:**
*   2 clientes pueden conectarse al servidor
*   Servidor corre a 50 FPS estable
*   Logs muestran ticks correctos

### FASE 2: ENTITY BASICS (Semana 3)
**Objetivo:** Jugador se mueve en red

**Tasks:**
*   ✅ Crear prefab Player base
*   ✅ Implementar PlayerController (movimiento WASD)
*   ✅ Implementar PlayerStats (HP/Mana con SyncVar)
*   ✅ Crear HUD básico con UI Toolkit
    *   Barras de HP/Mana
    *   Display de nombre
*   ✅ Implementar cámara Third-Person (Cinemachine)
*   ✅ Test: 2 jugadores se ven moverse entre sí

**Criterio de Éxito:**
*   Movimiento fluido sin jitter
*   Stats visibles en HUD
*   RTT < 100ms en localhost

### FASE 3: TARGETING SYSTEM (Semana 4)
**Objetivo:** Selección de objetivos funcional

**Tasks:**
*   ✅ Implementar TargetingSystem.cs
*   ✅ Crear prefab TargetRing (decal en los pies)
*   ✅ Implementar Tab-Targeting (ciclar enemigos)
*   ✅ Implementar Ground Targeting (cursor cruz)
*   ✅ Crear Target Frame en HUD (nombre + barra HP del target)
*   ✅ Spawner básico de NPCs dummy

**Criterio de Éxito:**
*   Click selecciona target visual
*   Tab cicla entre enemigos cercanos
*   Ground cursor funciona para AoE

### FASE 4: DATA PIPELINE (Semana 5)
**Objetivo:** Sistema de datos completo

**Tasks:**
*   ✅ Crear AbilityData.cs (ScriptableObject)
*   ✅ Crear StatusEffectData.cs
*   ✅ Crear ItemData.cs (básico)
*   ✅ Implementar AbilityDatabase (Registry singleton)
*   ✅ Crear assets para las 18 habilidades:
    *   Guerrero: 3
    *   Mago: 5
    *   Cazador: 3
    *   Sacerdote: 3
*   ✅ Exportar desde Google Sheets a JSON (opcional)

**Criterio de Éxito:**
*   Todos los assets creados
*   Database los encuentra por ID
*   Inspector muestra datos correctamente

### FASE 5: COMBAT CORE (Semana 6-7)
**Objetivo:** Primera habilidad funcional end-to-end

**Tasks:**
*   ✅ Implementar AbilityLogic (abstract)
*   ✅ Implementar ProjectileAbility
*   ✅ Implementar ProjectileController (SphereCast)
*   ✅ Implementar PlayerCombat.cs (input + RPC)
*   ✅ Crear prefab Fireball con trail
*   ✅ Integrar Object Pool para proyectiles
*   ✅ Implementar IDamageable interface
*   ✅ Test: Mago lanza Fireball y hace daño a dummy

**Criterio de Éxito:**
*   Fireball vuela y colisiona
*   Daño se aplica en servidor
*   HP del target baja
*   VFX visible en todos los clientes

### FASE 6: STATUS EFFECTS (Semana 8)
**Objetivo:** Sistema de buffs/debuffs completo

**Tasks:**
*   ✅ Implementar StatusEffectSystem.cs
*   ✅ Crear assets de effects:
    *   Stun
    *   Slow (50%)
    *   Shield
    *   Reflect
*   ✅ Integrar con PlayerController (Slow afecta velocidad)
*   ✅ Integrar con PlayerStats (Shield absorbe daño)
*   ✅ Integrar con ProjectileController (Reflect rebota)
*   ✅ Crear VFX para cada efecto (aura persistente)
*   ✅ Crear UI de buffs/debuffs (iconos arriba de HP bar)

**Criterio de Éxito:**
*   Rayo Helado aplica Slow visible
*   Barrera de Hielo absorbe daño
*   Reflect rebota proyectil

### FASE 7: REMAINING ABILITIES (Semana 9-10)
**Objetivo:** Todas las habilidades implementadas

**Tasks:**
*   ✅ Implementar MeleeAbility (Guerrero)
*   ✅ Implementar AoEAbility (Meteoro, Lluvia Flechas)
*   ✅ Implementar HealAbility (Sacerdote)
*   ✅ Implementar BuffAbility (Bendición)
*   ✅ Crear VFX para cada habilidad:
    *   Meteoro: Explosión de fuego
    *   Lluvia Flechas: Múltiples flechas cayendo
    *   Curación: Aura verde
*   ✅ Balanceo inicial (damage, cooldowns)

**Criterio de Éxito:**
*   Todas las 18 habilidades funcionan
*   No hay bugs críticos
*   Gameplay se siente responsive

### FASE 8: WORLD & ZONES (Semana 11)
**Objetivo:** Mundo abierto con zonas

**Tasks:**
*   ✅ Crear Map_OpenWorld_01 (Terrain básico)
*   ✅ Implementar ZoneController.cs
*   ✅ Crear SafeZone (invulnerabilidad)
*   ✅ Crear PvPZone
*   ✅ Implementar SpawnPoint system
*   ✅ Cargar mapa de forma Aditiva desde Bootstrap

**Criterio de Éxito:**
*   Jugador spawna en SafeZone
*   No puede recibir daño en SafeZone
*   Al salir, combate funciona normal

### FASE 9: LOOT SYSTEM (Semana 12)
**Objetivo:** Full Loot al morir

**Tasks:**
*   ✅ Implementar PlayerInventory.cs
*   ✅ Implementar LootBag.cs (contenedor en el suelo)
*   ✅ Crear UI de Inventory (UI Toolkit)
*   ✅ Lógica de Death:
    *   Dropear LootBag con todos los items
    *   Respawn en SafeZone
    *   Inventory vacío
*   ✅ Implementar pickup de items (raycast + E)

**Criterio de Éxito:**
*   Al morir, aparece LootBag
*   Otros jugadores pueden lootear
*   Items se transfieren correctamente

### FASE 10: PERSISTENCE (Semana 13)
**Objetivo:** Guardado persistente

**Tasks:**
*   ✅ Configurar Nakama en producción (VPS)
*   ✅ Implementar NakamaManager completo
*   ✅ Implementar Save/Load de CharacterData
*   ✅ Auto-save cada 30s
*   ✅ Save on disconnect
*   ✅ Load on connect (hidratar player)

**Criterio de Éxito:**
*   Jugador desconecta y reconecta
*   Stats, posición e inventory persisten
*   No hay pérdida de datos

### FASE 11: UI & POLISH (Semana 14)
**Objetivo:** Experiencia de usuario pulida

**Tasks:**
*   ✅ Implementar CastBar (barra de casteo)
*   ✅ Implementar Cooldown indicators (círculo en iconos)
*   ✅ Floating Damage Text (números voladores)
*   ✅ Implementar Chat básico (Nakama real-time)
*   ✅ SFX para todas las habilidades
*   ✅ Música ambiente
*   ✅ Settings menu (volumen, gráficos)

**Criterio de Éxito:**
*   UI es clara y funcional
*   Feedback visual/audio en toda acción
*   Juego se siente "juicy"

### FASE 12: OPTIMIZATION (Semana 15)
**Objetivo:** Cumplir performance budget

**Tasks:**
*   ✅ Profiling (Unity Profiler + Network Profiler)
*   ✅ Optimizar draw calls (Static Batching, LODs)
*   ✅ Optimizar network (Delta compression custom si necesario)
*   ✅ Implementar Occlusion Culling
*   ✅ Stress test con 50 bots

**Criterio de Éxito:**
*   Server mantiene 50 FPS con 50 jugadores
*   Client mantiene 60 FPS en combate
*   Network < 5 KB/s por jugador

### FASE 13: TESTING (Semana 16)
**Objetivo:** Bug fixing y balance

**Tasks:**
*   ✅ Playtest con 10+ personas
*   ✅ Bug tracking (Trello/Notion)
*   ✅ Balance de habilidades (spreadsheet)
*   ✅ Fix de exploits conocidos
*   ✅ Documentación de gameplay

**Criterio de Éxito:**
*   0 bugs críticos
*   Gameplay balanceado
*   Feedback positivo de testers

---