# PRD — Aplicación Personal de Finanzas

> Este documento antecede a `diseno_definitivo.md`. Define **qué** se construye y **para quién**, sin comprometerse a un **cómo**. Ningún término de implementación (CQRS, Outbox, monolito modular, nombres de clase) debería aparecer acá — si aparece, es una señal de que el PRD se contaminó con la solución.

---

## 1. Resumen

Una aplicación personal para registrar y consultar las finanzas de una sola persona: gastos en débito y crédito (con cuotas), suscripciones recurrentes, y gastos compartidos con terceros que generan deuda cruzada. Reemplaza el método actual de tracking (planilla, notas sueltas, u otra app) por un sistema con contabilidad de partida doble real, que no pierde precisión en repartos entre personas ni en cuotas, y que deja una pista de auditoría cuando algo se carga mal.

---

## 2. El problema

Llevar las finanzas personales con una planilla o una app genérica de gastos falla en tres puntos específicos, que son justamente los que este producto ataca:

- **Los gastos compartidos con redondeo pierden centavos.** Dividir $100 entre 3 personas da $33,33 repetido; la mayoría de las herramientas o truncan (y la suma no da $100) o fuerzan a la persona a ajustar a mano cada vez.
- **No hay diferencia entre "gasté" y "voy a tener que pagar".** Una compra en cuotas se siente como un gasto único en la mayoría de las apps, cuando en realidad es un compromiso que se va a devengar mes a mes — y eso hace que "cuánto tengo disponible este mes" sea sistemáticamente incorrecto.
- **La deuda con terceros no tiene trazabilidad.** "Fulano me debe algo de las vacaciones" termina siendo un cálculo mental o una nota, sin una línea de tiempo de quién pagó qué y cuánto queda pendiente.

Un motivo secundario, y honesto: esta aplicación también es un ejercicio para practicar diseño de API y una arquitectura que la persona que la construye no tiene experiencia usando en producción. Eso no cambia el problema de negocio de arriba, pero sí afecta las decisiones de alcance de la sección 5 — se explica ahí.

---

## 3. Objetivo

Dar a una sola persona una forma de registrar y consultar sus finanzas donde:

1. Los números cuadran siempre (partida doble, sin redondeos que se pierden).
2. La diferencia entre "gasté" y "voy a pagar" es explícita (débito vs. crédito, devengo vs. pago).
3. La deuda con terceros por gastos compartidos es consultable en cualquier momento, con quién y por cuánto.
4. Un error de carga se puede corregir sin perder el registro de que hubo un error (auditoría).

**No-objetivo explícito:** esta aplicación no busca reemplazar un ERP contable, ni una app de finanzas para más de una persona, ni ofrecer asesoría financiera. El alcance es deliberadamente doméstico.

---

## 4. Usuario

**Persona única:** el propio dueño de la aplicación. No hay multi-tenant, no hay roles, no hay onboarding de terceros — los "terceros" de RF-7 son datos de referencia (nombres y saldos), no cuentas de usuario.

Esto tiene consecuencias de producto que vale la pena decir explícitamente:

- No hace falta autenticación más allá de proteger el acceso a la aplicación en sí (no hay "usuarios" que loguearse dentro del sistema).
- No hace falta UX indulgente para gente que no entiende el dominio — el usuario entiende contabilidad básica porque la está diseñando. Tooltips explicando qué es un asiento contable, por ejemplo, no son necesarios.
- No hay ciclo de feedback de producto externo: la misma persona que define el requisito lo usa y lo evalúa. Eso es una ventaja de velocidad y un riesgo de punto ciego — nadie más va a decir "esto no tiene sentido" si el diseño se aleja de lo que realmente hace falta en el uso diario.

---

## 5. Alcance y fases

Los siete casos de uso descritos en la sección 6 fueron encargados como un bloque único. Desde una perspectiva de producto, eso merece una observación: **entregar los siete de una es más ambicioso que lo que normalmente se recomienda para una v1.** Un PRD real casi siempre corta esto en fases para validar el núcleo antes de invertir en lo periférico. No estoy decidiendo esto por vos — lo dejo como propuesta, porque el objetivo dual (utilidad real + práctica de arquitectura) puede justificar legítimamente ir directo por el todo.

**Fase 1 — núcleo transaccional (da valor solo, sin el resto):**
- US-1 (resumen de gastos débito)
- US-2 (cuánto pagar por tarjeta)
- US-3 (ingresar gasto en cuotas)
- US-4 (pagar el resumen)
- US-6 (revertir un error) — se sugiere en fase 1 y no al final, porque los errores de carga van a aparecer desde el primer día de uso real, no son un caso límite tardío.

**Fase 2 — recurrencia:**
- US-5 (suscripciones y autorrenovación)

**Fase 3 — terceros:**
- US-7 (cuentas corrientes y liquidación)

Si el objetivo prioritario es tener algo usable cuanto antes, esta fasificación tiene sentido. Si el objetivo prioritario es ejercitar la arquitectura completa (que ya se confirmó como el objetivo real), construir los siete módulos en paralelo es razonable — pero entonces vale la pena revisar la sección 8 (métricas), porque "úsalo de verdad" como criterio de éxito solo aplica una vez que la fase 1 está terminada, aunque el resto se construya en simultáneo.

---

## 6. Historias de usuario

Cada una en lenguaje de producto — sin nombres de clase, comandos, ni patrones. La correspondencia con el documento técnico está en la última columna solo para trazabilidad.

### US-1 — Ver el resumen de gastos del mes

> Como usuario, quiero ver cuánto gasté este mes en débito, para saber qué salió efectivamente de mi cuenta sin mirar movimiento por movimiento.

**Criterios de aceptación:**
- Puedo ver el total gastado en el mes actual y en meses anteriores.
- El total no incluye la parte de un gasto que le corresponde pagar a un tercero, aunque yo lo haya adelantado.
- Puedo distinguir gastos por categoría, si los categoricé al cargarlos.

*(RF-1)*

### US-2 — Ver cuánto tengo que pagar por tarjeta

> Como usuario, quiero ver cuánto me van a cobrar este mes en cada tarjeta, para poder prever mi flujo de caja antes de que llegue el resumen real del banco.

**Criterios de aceptación:**
- Puedo ver, por tarjeta, el total que corresponde pagar este mes.
- El número coincide con lo que efectivamente va a figurar en el resumen del banco (incluye solo cuotas que ya "cerraron", no compromisos futuros).
- Puedo ver por separado cuánto me queda comprometido a futuro en cuotas que todavía no llegaron.

*(RF-2)*

### US-3 — Cargar un gasto

> Como usuario, quiero cargar un gasto hecho con tarjeta, indicando en cuántas cuotas, desde qué mes, y si alguien más me debe una parte, para no tener que hacer ese cálculo a mano.

**Criterios de aceptación:**
- Puedo cargar un gasto con: monto, tarjeta, cantidad de cuotas, mes de inicio.
- Opcionalmente, puedo indicar que el gasto se divide entre N personas (incluyéndome a mí).
- Si divido el gasto, el sistema calcula la parte de cada uno sin que la suma de las partes difiera del total por errores de redondeo.
- Después de cargarlo, puedo ver cuánto le corresponde pagar a cada persona involucrada.

*(RF-3)*

### US-4 — Pagar el resumen de una tarjeta

> Como usuario, quiero marcar como pagado el resumen consolidado de una tarjeta, para que mi sistema refleje que ya no debo ese dinero.

**Criterios de aceptación:**
- Puedo ver, antes de pagar, el detalle de qué cuotas componen el total a pagar de una tarjeta ese mes.
- Al confirmar el pago, el sistema deja de contar ese monto como deuda pendiente.
- Puedo distinguir un mes ya pagado de uno pendiente cuando reviso el historial.

*(RF-4)*

### US-5 — Pagar y autorrenovar suscripciones

> Como usuario, quiero que mis suscripciones recurrentes (streaming, software, etc.) se registren solas cada mes sin que tenga que cargarlas a mano, para no olvidarme de ninguna.

**Criterios de aceptación:**
- Puedo definir una suscripción una sola vez (monto, frecuencia, método de pago).
- Cada período, el gasto correspondiente aparece automáticamente en mis registros, sin acción manual.
- Puedo dar de baja una suscripción y que deje de renovarse desde ese momento en adelante (sin afectar los cargos ya generados).

*(RF-5)*

### US-6 — Corregir un error de carga

> Como usuario, quiero poder anular un movimiento que cargué mal, para corregir el error sin perder el registro de que existió.

**Criterios de aceptación:**
- Puedo revertir un movimiento cargado por error.
- Después de revertirlo, mi saldo refleja la corrección.
- Puedo ver en el historial que ese movimiento fue revertido y cuándo — no desaparece como si nunca hubiera existido.
- Si el movimiento ya fue parte de un pago de tarjeta confirmado, el sistema me avisa que no puedo revertirlo directamente y me explica por qué.

*(RF-6)*

### US-7 — Llevar cuenta con terceros

> Como usuario, quiero ver cuánto me debe o le debo a cada persona con la que comparto gastos, con el historial de cómo se llegó a ese número, para saldar cuentas sin discutir de memoria.

**Criterios de aceptación:**
- Por cada persona, puedo ver el saldo neto actual (me debe / le debo / estamos a mano).
- Puedo ver la línea de tiempo de movimientos que componen ese saldo (qué gasto, cuándo, cuánto).
- Cuando esa persona me paga (o yo le pago) para saldar la diferencia, puedo registrarlo y el saldo se actualiza.
- Si tengo gastos cruzados con la misma persona (a veces pago yo, a veces paga ella), el saldo neto los compensa automáticamente — no tengo que sumar y restar a mano.

*(RF-7)*

---

## 7. Fuera de alcance

Exclusiones explícitas para esta versión. Están marcadas como supuestos — confirmalas o corregilas si alguna no aplica:

- **Multi-usuario / autenticación de terceros.** Los terceros son datos de referencia, no cuentas.
- **Integración bancaria automática** (open banking, importación de extractos, conciliación automática contra movimientos reales del banco).
- **Multi-moneda.** Se asume una sola moneda de referencia.
- **Notificaciones o recordatorios proactivos** ("te vence la tarjeta en 3 días").
- **Presupuestos, metas de ahorro, o proyecciones financieras.**
- **Reportes fiscales o exportación contable formal** (declaración de impuestos, formatos AFIP/similares).
- **Gastos en efectivo.** US-1 habla específicamente de débito; no está definido si el efectivo entra en algún flujo. Ver pregunta abierta en sección 9.

---

## 8. Métricas de éxito

Este proyecto no tiene usuarios más allá de quien lo construye, así que métricas de producto convencionales (retención, activación, NPS) no aplican. Las métricas honestas para este caso son:

**De utilidad real:**
- El sistema reemplaza por completo el método de tracking anterior durante al menos 4 semanas seguidas de uso real (no en paralelo con la planilla vieja "por las dudas").
- El saldo de cada tarjeta que muestra el sistema coincide con el resumen real del banco, sin ajustes manuales, desde el primer ciclo de uso.
- El saldo con cada persona que muestra el sistema coincide con lo que esa persona reconoce deber o que le deben, cuando se le pregunta directamente.

**De aprendizaje (dado el objetivo dual declarado):**
- El código ejercita en un caso real: partida doble con invariantes verificados, separación de comandos y consultas, un patrón de mensajería asíncrona usado donde genuinamente corresponde (no donde no), y aislamiento entre módulos verificado por tests, no solo por convención.

Si en algún punto la Fase 1 (sección 5) no cumple la primera métrica de utilidad real después de un uso honesto, es una señal de que el diseño resolvió el problema equivocado, más allá de cuán prolijo esté el código — vale la pena revisarlo antes de seguir construyendo las fases siguientes.

---

## 9. Preguntas abiertas

No las contesté por vos porque son decisiones de producto, no de arquitectura:

1. **No hay definida ninguna interfaz de uso.** Los siete casos de uso están descritos como capacidades, pero ¿cómo los vas a ejecutar en la práctica? ¿Un cliente HTTP tipo Postman/Swagger manualmente, una CLI, o eventualmente una interfaz web o mobile? Esto no es un detalle menor: "ver el resumen del mes" (US-1) sin una superficie de consumo definida es un JSON crudo, y eso probablemente mata la métrica de "lo uso de verdad" de la sección 8 antes de que el backend tenga la culpa.
2. **US-6 (revertir) — ¿es una acción frecuente o una red de seguridad rara?** Afecta si merece confirmación explícita en la interfaz ("¿estás seguro?") o si alcanza con que exista como capacidad técnica sin ceremonia adicional.
3. **Gastos en efectivo.** US-1 dice "débito" explícitamente. ¿El efectivo queda fuera a propósito, entra como parte de débito, o necesita su propia categoría?
4. **Definición de "mes" para cuotas y suscripciones.** ¿Es el mes calendario, o un ciclo de facturación con fecha de corte propia por tarjeta (que es como funcionan las tarjetas reales)? Cambia qué significa "este mes" en US-2 y US-4.

---

## 10. Relación con el documento técnico

`diseno_definitivo.md` es la respuesta de ingeniería a este PRD: las decisiones D1–D11 ahí documentadas (Ledger como fuente de verdad contable, separación devengo/pago, orquestación eventual, etc.) existen para satisfacer las historias de usuario de la sección 6. Si el alcance de este documento cambia — se agrega, se saca, o se refasea un caso de uso — el documento técnico necesita revisarse en consecuencia; no son independientes.
