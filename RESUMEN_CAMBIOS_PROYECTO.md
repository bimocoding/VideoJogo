# Resumen de Cambios y Nuevas Mecánicas del Proyecto

Este documento reúne de forma técnica y clara todas las modificaciones, correcciones arquitectónicas y nuevos sistemas integrados en el proyecto para sincronizar al equipo de desarrollo.

---

## 1. Capa Centralizada de Entrada (`PlayerInputHandler.cs`)

Se creó un nuevo componente dedicado en el objeto raíz del jugador con ejecución prioritaria (`[DefaultExecutionOrder(-100)]`), actuando como árbitro único de entrada antes de que corran `player.cs` o `PlayerCombatController.cs`.

* **Resolución SOCD con Last Input Priority (LIP)**:
  * Si mantienes pulsada <kbd>D</kbd> y presionas <kbd>A</kbd>, el personaje cambia instantáneamente a la izquierda sin pasar por 0 ni frenar en seco. Al soltar <kbd>A</kbd>, regresa de inmediato a la derecha. Lo mismo aplica para <kbd>W</kbd> y <kbd>S</kbd>.
  * Elimina los "Backdodges involuntarios" que ocurrían en giros rápidos donde ambas teclas se solapaban por 1 o 2 frames.
* **Input Buffers Deterministas con Consumo Explícito**:
  * **Jump Buffer (0.15 s)**: Registra la pulsación de salto durante ataques o evasiones y se ejecuta en el instante en que el personaje toca suelo o termina su bloqueo de animación. Se limpia con `ClearAllBuffers()` al agarrarse de un borde o recibir knockback.
  * **Attack Buffer (0.14 s)**: Registra pulsaciones de ataque y captura snapshots de teclas direccionales (`AttackBufferedUp` y `AttackBufferedDown`) para que combos como <kbd>W</kbd> + <kbd>J</kbd> o <kbd>S</kbd> + <kbd>J</kbd> no se pierdan si se suelta la dirección un frame antes.
  * **Dodge Buffer (0.12 s)**: Garantiza que presionar <kbd>K</kbd> justo antes de que termine el cooldown o al aterrizar dispare el esquive sin descartar la pulsación.
* **Limpieza de `FixedUpdate`**:
  * `player.cs` ya no lee `UnityEngine.Input` dentro de `FixedUpdate`. Ahora solo consulta booleanos cacheados (`JumpHeld`, `DownHeld`, `MoveX`), eliminando la pérdida de inputs por desincronización de ciclos de físicas.

---

## 2. Ajustes de Movilidad y Evasión (`player.cs`)

### Calibración del Slide
* **Distancia recortada**: Se redujo de 3.31 m (~10.3 baldosas, excesivo para plataformas de precisión) a **~1.83 m** (~5.7 baldosas).
* **Parámetros finales**:
  * `slideInitialSpeed = 7.2f`
  * `slideDeceleration = 12.5f`
  * `slideDuration = 0.38f`
* **Regla estricta de activación**: El Slide solo se dispara manualmente con **<kbd>S</kbd> + <kbd>K</kbd>** (o de forma forzada si el personaje está bajo un techo de 1 bloque y no puede levantarse). Ya no se activa por error al esquivar con el Sprint encendido.

### Consistencia en Dodge y Backdodge
* Se eliminó la multiplicación de velocidad por carrera (`dodgeRunMultiplier`). Ahora presionar <kbd>K</kbd> neutral o adelante produce exactamente el mismo Dodge con velocidad y distancia constantes, esté o no activado el toggle de Sprint.

### Bloqueo de Caminata/Gateo Agachado
* Se eliminó el desplazamiento horizontal mientras el personaje está agachado (`rb2d.linearVelocity.x = 0`).
* **Prevención de softlock**: Se permite girar en el sitio con <kbd>A</kbd> / <kbd>D</kbd> sin desplazarse, permitiendo al jugador apuntar la dirección para deslizarse con <kbd>K</kbd> y escapar de cualquier túnel bajo.

---

## 3. Combate y Game Feel (`PlayerCombatController.cs`)

### Recovery Jump-Cancel Reactivo
* Se respeta íntegramente el peso y los frames activos de impacto del ataque, pero en la fase final de recuperación se permite cancelar el golpe con <kbd>Espacio</kbd> para saltar inmediatamente:
  * **Hit 1**: Cancelable desde el 30% (~0.21 s), **0.25 s antes** de que inicie la ventana del destello cian (0.47 s).
  * **Hit 2 Básico**: Cancelable desde el 30% (~0.24 s).
  * **Hit 3 Básico**: Cancelable desde el 35% (~0.60 s), reteniendo el impacto del golpe pesado sin bloquear al jugador 1.24 s.
  * **Hit 2 Variación**: Cancelable desde el 55% (~0.16 s).
  * **Hit 3 Variación**: Cancelable desde el 65% (~0.29 s).
  * **Hit 4 Lunge**: Cancelable desde el 35% (~0.28 s).
  * **Vertical Finisher**: Permanece **100% blindado** (sin jump-cancel) para preservar la contundencia del impacto y temblor.
* **Control direccional frame 0**: Al pulsar salto durante la recuperación, la orientación y movimiento horizontal se calculan en ese mismo frame antes de despegar.
* **Limpieza de destello**: Se implementó `StopDelayGleamIfActive()` para asegurar que el destello cian se apague de inmediato al cancelar con salto, esquive o interrupción externa.

### Control Aéreo y <kbd>W</kbd> + <kbd>J</kbd>
* **<kbd>W</kbd> + <kbd>J</kbd> estrictamente en el suelo**: El Direct Launcher ahora exige `isGrounded && wantsUp`. Ya no es posible relanzarse infinitamente en el aire a +7.8 m/s.
* **Límite Aéreo y Landing Attack Buffer**:
  * Se configuró **1 corte aéreo limpio por salto** (`maxAirAttacksPerJump = 1`), que recarga al tocar suelo o interactuar con cornisas.
  * Si el jugador gasta su ataque aéreo y vuelve a pulsar <kbd>J</kbd> cerca del suelo, el buffer no se descarta en el aire y dispara automáticamente el **Hit 1 terrestre en el frame 1 del aterrizaje**.

### Sincronización del Animator
* En todos los ataques se invirtió el orden de llamada: primero se actualizan los booleanos (`SetAnimatorCombatBools`) y de inmediato se fuerza `PlayAnimationState` con `Anim.Update(0f)`. Esto evita que las transiciones internas del Animator sobreescriban los clips al cambiar de frame.

---

## 4. Sistema Modular de Stamina (`PlayerStamina.cs`)

Se creó un componente desacoplado para gestionar la resistencia del jugador:

* **Reglas de Consumo**:
  * **Evasión / Dash**: Dodge (`18`), Backdodge (`18`), Slide (`15`), Air Dash (`20`).
  * **Ataques**: Ligeros (`12`), Medios (`16`), Pesados (`22`), Vertical Finisher (`28`).
  * **Movilidad Gratuita**: Caminar, correr con Shift, saltar, hacer doble salto y trepar salientes tienen coste **0**.
* **Regeneración Controlada**:
  * Retardo de espera tras gastar: `regenDelay = 0.65 s`.
  * Tasa de recuperación: `34` puntos por segundo.
  * La regeneración se congela durante animaciones de combate (`IsInCombatAction`), evasiones (`IsInEvasiveAction`) o dash aéreo, garantizando que no se recargue en mitad de un ataque.
* **Cobro Seguro en Cancelaciones**:
  * En **Dodge Cancel** (<kbd>K</kbd> durante combo) y en el atajo **Slide/Dodge -> Hit 4 Lunge** (<kbd>J</kbd>), la stamina se cobra **antes** de interrumpir la acción en curso. Si no alcanza, el ataque actual continúa sin romperse.
* **Preparado para Dificultad y HUD**:
  * Contiene eventos estáticos `OnStaminaChanged(current, max)` y `OnStaminaInsufficient`.
  * Multiplicadores en tiempo real `costMultiplier` y `regenMultiplier` listos para el futuro selector de dificultad.

---

## 5. Actualización Visual del HUD (`PlayerHealthHUD.cs`)

* **Salud en Rojo**:
  * Color base normal: **Rojo vivo** (`#EB2E2E` / `new Color(0.92f, 0.18f, 0.18f, 1f)`).
  * Color de salud crítica (< 30%): **Rojo oscuro** (`#A61414`).
  * Rastro de daño (Catchup): Naranja-rojo translúcido para distinguir visualmente el chip damage.
* **Nueva Barra de Stamina en Verde**:
  * Ubicada inmediatamente debajo de la barra de salud.
  * Relleno activo: **Verde esmeralda vivo** (`#2EE061` / `new Color(0.18f, 0.88f, 0.38f, 1f)`).
  * Rastro de consumo (Catchup): Verde menta suave (`new Color(0.40f, 0.95f, 0.55f, 0.85f)`).
  * Contador numérico dinámico: `100 / 100`.
  * Feedback visual: Destello rojo de advertencia si se intenta ejecutar una acción sin suficiente stamina.

---

## 6. Mapa de Archivos del Proyecto

| Archivo | Estado | Responsabilidad Principal |
| :--- | :---: | :--- |
| [PlayerInputHandler.cs](file:///C:/Users/bimok/My%20project%20(1)/Assets/scripts/PlayerInputHandler.cs) | **Nuevo** | Árbitro de entrada, LIP SOCD y buffers de salto, ataque y esquive. |
| [PlayerStamina.cs](file:///C:/Users/bimok/My%20project%20(1)/Assets/scripts/PlayerStamina.cs) | **Nuevo** | Capacidad, costos de acciones, retardo y eventos de stamina. |
| [SetupStaminaBar.cs](file:///C:/Users/bimok/My%20project%20(1)/Assets/Editor/SetupStaminaBar.cs) | **Nuevo** | Utilidad de editor para construir y vincular la barra de stamina en el Canvas. |
| [player.cs](file:///C:/Users/bimok/My%20project%20(1)/Assets/scripts/player.cs) | Modificado | Calibración de Slide, bloqueo de gateo agachado, integración de stamina y jump-cancel. |
| [PlayerCombatController.cs](file:///C:/Users/bimok/My%20project%20(1)/Assets/scripts/PlayerCombatController.cs) | Modificado | Ventanas de Jump-Cancel, cobro de stamina por golpe, landing buffer y bloqueo de <kbd>W</kbd>+<kbd>J</kbd> en aire. |
| [PlayerHealthHUD.cs](file:///C:/Users/bimok/My%20project%20(1)/Assets/scripts/PlayerHealthHUD.cs) | Modificado | Salud roja, visualización y animación de catchup para la barra verde de stamina. |
| [idle.prefab](file:///C:/Users/bimok/My%20project%20(1)/Assets/Prefab/idle.prefab) | Modificado | Vinculación y serialización de `PlayerInputHandler` y `PlayerStamina`. |
| [SampleScene.unity](file:///C:/Users/bimok/My%20project%20(1)/Assets/Scenes/SampleScene.unity) | Modificado | Estructura de UI de stamina en Canvas e instancia de personaje actualizada. |
