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

**Prerrequisito transversal.** Antes de cargar cualquier movimiento, el usuario registra sus instrumentos de pago (bancos, tarjetas, efectivo). Cada instrumento se da de alta como débito, crédito o efectivo; si es crédito, además requiere la fecha de corte de esa tarjeta, porque de ella depende a qué ciclo de facturación pertenece cada compra que se cargue con ese instrumento (ver US-2, US-3 y US-4). No tiene una historia numerada propia porque es alta de datos de referencia, no una decisión de producto en sí — pero es condición real para todo lo que sigue.

### US-1 — Ver el resumen de gastos del mes

> Como usuario, quiero ver cuánto gasté este mes en débito y efectivo, para saber qué salió efectivamente de mi cuenta o de mi bolsillo sin mirar movimiento por movimiento.

**Criterios de aceptación:**
- Puedo ver el total gastado en el mes actual y en meses anteriores.
- El total agrupa débito y efectivo como una misma categoría de "ya gastado" — son instrumentos distintos, pero ninguno de los dos es un compromiso futuro como sí lo es el crédito.
- El total no incluye la parte de un gasto que le corresponde pagar a un tercero, aunque yo lo haya adelantado.
- Puedo distinguir gastos por categoría, si los categoricé al cargarlos.

*(RF-1)*

### US-2 — Ver cuánto tengo que pagar por tarjeta

> Como usuario, quiero ver cuánto me van a cobrar este mes en cada tarjeta, para poder prever mi flujo de caja antes de que llegue el resumen real del banco.

**Criterios de aceptación:**
- Puedo ver, por tarjeta, el total que corresponde pagar este mes.
- El número coincide con lo que efectivamente va a figurar en el resumen del banco (incluye solo cuotas que ya "cerraron", no compromisos futuros).
- El "mes" de cada tarjeta se define por su propia fecha de corte, no por el mes calendario: dos tarjetas con fechas de corte distintas pueden tener ciclos de facturación distintos para una compra hecha el mismo día.
- Puedo ver por separado cuánto me queda comprometido a futuro en cuotas que todavía no llegaron.

*(RF-2)*

### US-3 — Cargar un gasto

> Como usuario, quiero cargar un gasto hecho con tarjeta, indicando en cuántas cuotas y si alguien más me debe una parte, para no tener que hacer ese cálculo a mano.

**Criterios de aceptación:**
- Puedo cargar un gasto con: monto, tarjeta, cantidad de cuotas, fecha de la compra.
- El sistema determina solo, a partir de la fecha de compra y la fecha de corte de esa tarjeta, a qué ciclo de facturación pertenece la primera cuota — no lo tengo que calcular ni indicar yo. Una compra hecha el mismo día del corte o antes cae en el ciclo que está cerrando; una compra posterior cae en el ciclo siguiente.
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

**Revisado por la decisión 15.** El segundo criterio de aceptación —"el gasto aparece automáticamente, sin acción manual"— describía el auto-cobro por scheduler que causaba el bug de "N ciclos salteados = N·monto"; se reemplaza por pago explícito (el usuario marca pagado cada período, o el alta lo asume pagado si el ciclo ya venció). El primer y tercer criterio (alta única, baja sin afectar cargos previos) siguen vigentes sin cambios.

### US-6 — Corregir un error de carga

> Como usuario, quiero poder anular un movimiento que cargué mal, para corregir el error sin perder el registro de que existió.

**Criterios de aceptación:**
- Puedo revertir un gasto cargado por error, incluidas las cuotas que ya fueron pagadas — el sistema no me bloquea la reversión solo porque ya haya un pago de tarjeta confirmado de por medio.
- La reversión es completa: corrige tanto las cuotas futuras como las ya pagadas, dejando un movimiento de reversión (no un borrado del original).
- Si el gasto revertido estaba repartido con un tercero, la deuda de esa persona se corrige automáticamente como parte de la misma acción — no tengo que ajustarla a mano en US-7.
- Después de revertirlo, mi saldo y el de la tarjeta correspondiente reflejan la corrección completa.
- Puedo ver en el historial que ese movimiento fue revertido y cuándo — no desaparece como si nunca hubiera existido.

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

## 9. Decisiones registradas

Estas eran preguntas abiertas; ya se resolvieron. Quedan documentadas acá para trazabilidad histórica — el detalle vivo de cada una vive en la historia de usuario correspondiente.

1. **Interfaz de consumo.** Por ahora, API pura (sin UI), pensada para probarse manualmente vía Swagger/Postman. Hay un cliente Angular planeado a futuro; la API se diseña compatible con ese consumo (respuestas consistentes, spec documentada) sin que eso comprometa la testeabilidad de los endpoints en el corto plazo.
2. **US-6 (revertir) — alcance de la reversión.** Es completa: incluye cuotas ya pagadas (vía movimiento de reversión, no borrado) y corrige automáticamente la deuda de un tercero si el gasto estaba repartido. La pregunta de si necesita una ceremonia de confirmación explícita en la interfaz ("¿estás seguro?") queda pendiente hasta que exista una UI — no aplica a nivel de API.
3. **Gastos en efectivo.** Entran en alcance: el efectivo es un tipo de instrumento propio (igual que débito o crédito), y se reporta junto con débito en US-1 porque ambos representan dinero ya gastado, sin ciclo de facturación futuro.
4. **Definición de "mes" para cuotas y resúmenes.** No es el mes calendario: cada tarjeta tiene su propia fecha de corte, registrada al dar de alta el instrumento (ver prerrequisito al inicio de la sección 6). Una compra hecha en la fecha de corte o antes pertenece al ciclo que está cerrando; una compra posterior pertenece al ciclo siguiente. Ver US-2, US-3 y US-4.
5. **Registro de a quién se le paga (acreedor).** Se identificó la necesidad de distinguir, al cargar un gasto, *a quién se le pagó y a qué cuenta* de *con quién se comparte* ese gasto (US-3, US-7) — son conceptos distintos: un acreedor no necesariamente es alguien con quien se reparten gastos, y una persona con quien se comparten gastos no necesariamente es a quien se le pagó. Se agrega una entidad de referencia liviana para dar de alta acreedores y, opcionalmente, sus cuentas de destino (una etiqueta libre + un dato de cuenta también libre, sin validarlo contra ningún banco real). La integración con el flujo de carga de un gasto (US-3) se especifica en `docs/creditor-expense-fields/slice-2-load-expense-integration.md`; la CRUD de acreedores en `slice-1-creditors-crud.md`. Ver `docs/creditor-expense-fields/` para el detalle completo.
6. **Etiqueta legible del gasto y desglose por tarjeta.** Se identificó que un gasto cargado en cuotas (US-3) no tenía ningún dato legible para el usuario más allá de un identificador interno — al confirmarlo, y después, al mirar cuánto debe por tarjeta (US-2), no había forma de saber *de qué* se trataba cada compra. Se agrega una descripción de texto libre, obligatoria, a cada gasto cargado (independiente de la categoría de US-1, que es una agrupación de reportes, no una etiqueta de la compra), y se extiende la vista de "cuánto debo por tarjeta" para poder desplegar cada tarjeta y ver la lista de compras pendientes que componen ese total, cada una con su descripción. El detalle está en `docs/expense-description/slice-1-description-field.md` (el campo), `slice-2-card-debt-drilldown.md` (el desglose) y `slice-3-recent-purchases-view.md` (una vista independiente de "compras recientes": un listado cronológico plano de todos los gastos cargados en cualquier tarjeta, con su descripción, sin agrupar por tarjeta ni filtrar por deuda pendiente o estado de pago — ya implementada).
7. **Gasto financiado por un acreedor (tarjeta opcional).** Un gasto en cuotas no siempre lo financia una tarjeta propia: puede ser el plan de cuotas de un comercio o una persona que adelantó la plata (US-3, en relación con US-7). Se vuelve **opcional** la tarjeta en un plan de pagos: cuando el gasto lo financia un acreedor, el plan no tiene tarjeta ni ciclo de facturación ni resumen mensual — es un cronograma de cuotas mensuales contadas desde el mes de la compra (la cuota *k* se debe en el mes de compra + k), y la deuda propia con el acreedor no se asienta en el libro mayor. Se exige tarjeta **o** acreedor, nunca ambos ni ninguno. El reparto entre terceros (US-7) sigue funcionando: la parte de cada co-deudor se asienta contra una cuenta pasivo `CreditorPayable` que se provisiona por plan, y la parte propia del titular nunca toca el libro. En el cliente, el checkbox "Diferente acreedor" (decisión 5) pasa a ser un selector de modo de pago de dos opciones (tarjeta propia / financiado por un acreedor), con lugar para un tercer modo débito-efectivo más adelante. El detalle está en `docs/expense-payment-modes/slice-1-creditor-financed.md`. Fuera de alcance por ahora: la lista de "deudas con acreedores" (Slice 2), el modo débito-efectivo (Slice 3) y el flujo de "pagarle al acreedor".
8. **Ver las cuotas futuras de un tercero en un gasto compartido con tarjeta.** En un gasto en cuotas con tarjeta propia dividido con un tercero (US-3, US-7), la parte del co-deudor se devenga recién cuando cierra cada ciclo de facturación (US-2, US-4); hasta entonces la línea de tiempo de ese tercero no muestra nada y su saldo figura en cero, aunque el compromiso ya exista. Se identificó la necesidad de que la vista de ese tercero anticipe cuánto va a deber por mes hasta que se paguen las N cuotas. Se agrega una proyección por tercero de las cuotas todavía no devengadas, calculada con el mismo reparto que usa el devengo real (sin diferencia de centavos por redondeo), y se muestra en el detalle del tercero como un bloque "Programado", separado de los movimientos ya asentados. No cambia el devengo, el libro mayor ni los reportes. El detalle está en `docs/parties-card-split/slice-2b-party-future-shares.md` (la proyección) y `slice-1-reconcile-loop-fix.md` (un arreglo de cliente: un gasto con tarjeta dividido se confirma y se marca "programado" en el acto, sin quedar esperando un cambio de saldo que en el modo tarjeta no llega sincrónicamente). **Resuelto (ver decisión 9).** El bloque "Programado" — y toda vista de pago — pasa a mostrar el mes de pago (ciclo de cierre + 1); las vistas de resumen siguen mostrando el mes de cierre.
9. **Mostrar el mes de pago, no el mes de cierre, en las vistas de pago.** El norte del usuario es *"abrir la app y ver qué tengo que pagar **este** mes"*. Hasta ahora toda vista de tarjeta mostraba el ciclo en el que el resumen *cierra* (decisión 4): una compra del 6 de septiembre con corte el 15 se veía como Sep/Oct/Nov, aunque ese resumen recién se pague en octubre. Se introduce un concepto derivado —*mes de pago = ciclo de cierre + 1*— y se enruta por él toda superficie **de pago**: el bloque "Programado" del tercero (decisión 8), el cronograma de cuotas futuras de la tarjeta y el "deuda de tarjeta por ciclo" del panel. Una compra en la fecha de corte o antes se paga el mes siguiente; una posterior al corte, dos meses después. Las superficies **de resumen** (los resúmenes mensuales y su detalle) siguen mostrando el mes de cierre, porque la identidad de un resumen *es* su mes de cierre. Además, en un gasto compartido con tarjeta la parte del co-deudor pasa a asentarse cuando llega el mes de pago, no cuando cierra el ciclo, para que el saldo y el cronograma nunca se contradigan. No cambia el cálculo del ciclo de facturación (decisión 4), el devengo del titular ni lo que se almacena — es una proyección de lectura más un cambio en el momento del asiento del reparto. El detalle está en `docs/cycle-due-month/slice-1-card-due-month.md` (+ `00-overview.md`). La paridad del modo "financiado por un acreedor" con el modo tarjeta se resuelve en la decisión 10; una lista de terceros consciente del cronograma se resuelve en la decisión 11.
10. **Un gasto financiado por un acreedor y dividido con un tercero se comporta igual que uno con tarjeta.** Hasta ahora, un gasto "financiado por un acreedor" (decisión 7) dividido con un tercero asentaba la parte del co-deudor **completa y por adelantado** al cargarlo, y no aparecía en el bloque "Programado" del tercero. Un gasto con tarjeta dividido, en cambio, muestra $0 debido ahora y las cuotas programadas hacia adelante. Se unifican: el plan del acreedor pasa a almacenar el **mes de la compra** como su ciclo (antes: mes de compra + 1), de modo que la misma proyección *mes de pago = ciclo + 1* (decisión 9) ubica la primera cuota el mes siguiente, sin ramas tarjeta-vs-acreedor en ningún lector; la parte del co-deudor arranca en $0 y **se devenga al llegar el mes de pago de cada cuota**, con el mismo mecanismo de devengo por vencimiento que las cuotas con tarjeta (un solo scheduler); y las cuotas del acreedor aparecen en el bloque "Programado" del tercero, fechadas por el mes de pago. Se revierte el asiento por adelantado que se había introducido para un bug de "aparece saldado": ese bug lo causaba la **falta de cronograma visible**, no la falta de plata, y la decisión 9 ya hizo visible el cronograma. La garantía de no-regresión de este slice cubre el **detalle** del tercero; el resumen de la **lista** de terceros se resuelve en la decisión 11. No hay cambio de esquema ni migración. El detalle está en `docs/cycle-due-month/slice-2-creditor-split-parity.md` (+ `00-overview.md`).

11. **La lista de terceros distingue "saldado" de "$0 ahora, N cuotas programadas".** El detalle de un tercero ya muestra el bloque "Programado" (decisiones 8–10), pero la **lista** de terceros decidía el rótulo solo por el saldo asentado: un tercero que hoy debe $0 pero tiene cuotas de un gasto compartido —con tarjeta o financiado por un acreedor— todavía sin devengar se veía igual que uno que de verdad no debe nada ("Saldado"). Se agrega una lectura que suma, por tercero, sus cuotas pendientes no devengadas —la misma población que el bloque "Programado", con el mismo reparto de centavos— y la lista pasa a mostrar "Nada debido aún · N programadas" cuando el saldo es cero pero hay cuotas por venir, reservando "Saldado" para cuando no hay ni saldo ni cronograma. Los estados "te deben" / "les debés" no cambian cuando hay saldo asentado real. No cambia el saldo, el devengo ni el libro mayor: es una lectura agregada más una combinación en el cliente (el roster de `GET /v1/parties` ya trae a todos los terceros). El detalle está en `docs/cycle-due-month/slice-3-schedule-aware-summary.md` (+ `00-overview.md`). Con esto se cierra la iniciativa `docs/cycle-due-month/`.

12. **Pagar cuotas puntuales de un resumen de tarjeta, no solo el resumen entero.** Hasta ahora un `MonthlyStatement` solo se podía saldar todo-o-nada (`PayStatement` cobraba el `AmountDue` completo y marcaba el resumen como pagado). Pero un resumen agrupa cuotas de varias compras, y muchas veces el usuario solo puede pagar algunas ahora. **Unidad de pago = una cuota, completa** — "parcial" describe al *resumen*, nunca a una fracción de cuota; no hay interés, remanente ni pago mínimo. "Próximo pago de esta compra" es una lectura *derivada* (la cuota impaga, no revertida, más temprana y su mes de pago), sin reprogramar nada. Se introduce en tres cortes: **(Slice 1 — hecho)** la cuota gana estado de pago propio (`Installment.PaidOnUtc`) y `PayStatement` pasa a cobrar la **suma de las cuotas devengadas, no revertidas e impagas** en lugar del `AmountDue` almacenado — corrección deliberada, idéntica para resúmenes sin reversiones, que además arregla un sobregiro latente cuando el resumen tenía una cuota revertida (el `AmountDue` solo se incrementa y todavía la incluía, aunque el storno ya había bajado el pasivo del Ledger). **(Slice 2 — hecho)** un endpoint dedicado `POST /v1/financing/installments/{id}/pay` que asienta un `Dr Pasivo:Tarjeta / Cr Activo:Banco` plano por el monto de esa cuota (sin netear el saldo a favor de la tarjeta), más el botón "Pagar" por fila en el detalle del resumen; el botón existente se re-rotula "Pagar resumen completo" y ambas acciones conviven. Pagar la última cuota impaga sella también el resumen. **(Slice 3 — hecho)** cada compra en "Compras recientes" muestra "N/M pagadas · próximo: `<mes>`" (o "Fully paid" cuando no queda ninguna cuota): `paidInstallmentCount` = cuántas cuotas tienen `PaidOnUtc`, y el "próximo pago" es la cuota impaga, no revertida, más temprana proyectada a su mes de pago (`BillingCycle.DueCycle`) — una lectura derivada, sin reprogramar nada. Solo cuotas de tarjeta; los planes financiados por un acreedor y los gastos débito/efectivo quedan fuera de alcance. El detalle está en `docs/individual-installment-payments/` (`00-overview.md` + tres slices). Con esto se cierra la iniciativa.

13. **Cargar una compra vieja reconoce lo ya pagado, no la trata como nueva.** Hasta ahora, al cargar un gasto el cronograma se armaba desde `purchaseDate` (eso ya estaba bien — el cálculo del ciclo nunca miró "hoy"), pero la app igual mostraba "0/N pagadas" y apuntaba el "próximo pago" a un mes que ya pasó. Regla común a ambos modos: la cuota cuyo **mes de pago** (ciclo de cierre + 1, decisión 9) es anterior al mes en curso está **vencida → se marca pagada**; la del mes en curso queda devengada y pendiente; las futuras no se tocan. **(Slice 1 — hecho, solo tarjeta)** al crear un plan de **tarjeta** con `purchaseDate` pasada, `CreatePaymentPlanHandler` devenga **en ese momento** cada ciclo ya cerrado (`Dr Gasto:Tarjeta / Cr Pasivo:Tarjeta`, fechado en la fecha de corte histórica, sobre su `MonthlyStatement`) y paga las cuotas cuyo mes de pago ya pasó (`Dr Pasivo:Tarjeta / Cr Activo:Banco`, fechado en la fecha de vencimiento histórica) — asientos contables reales, con fechas históricas, para que el reporte mes a mes refleje la realidad; que un saldo bancario de un mes pasado quede en negativo es aceptable (la app registra, no fuerza, saldos). El banco que fondea esos pagos es un dato nuevo del comando (`BankAccountId`, selector "Pagado desde" en Cargar gasto), obligatorio sólo cuando la compra retroactiva tiene al menos una cuota vencida — la ruta de pago no tiene banco por defecto. Se rechaza una `purchaseDate` futura (`FuturePurchaseDate`, 422; también validado en el cliente). No cambia el cálculo del ciclo (decisión 4) ni el esquema; no hay migración. El detalle está en `docs/backdated-expenses/slice-1-card-backdating.md` (+ `00-overview.md`). **(Slice 2 — hecho, modo acreedor)** `PaymentPlan.Create` resuelve el primer ciclo del plan de acreedor con `ResolveCycle(purchaseDate, 26)` de forma **uniforme** —una compra posterior al 26 rueda al ciclo siguiente, sea retroactiva o no—, y al crear un plan de acreedor con fecha pasada `CreatePaymentPlanHandler` sella `PaidOnUtc` en cada cuota ya vencida — **sólo el sello, sin asiento al libro mayor, sin devengo, sin `MonthlyStatement`, sin banco** (la deuda con un acreedor no tiene huella contable para el titular; no hay comando "pagar al acreedor" — un libro mayor de acreedores es una iniciativa aparte). El detalle está en `docs/backdated-expenses/slice-2-creditor-cutoff.md`. **(Slice 3 — hecho, opcional)** cada fila de "Compras recientes" muestra además el **monto pendiente** de esa compra = Σ del monto de sus cuotas impagas y no revertidas (mismo filtro que la "próxima cuota" de la decisión 12, así una cuota revertida no cuenta), junto al "N/M pagadas · próximo: `<mes>`" ya existente; se oculta cuando es cero. Es una lectura derivada más: no toca el esquema, el libro mayor ni el devengo. El detalle está en `docs/backdated-expenses/slice-3-pending-amount.md`. Con esto se cierra la iniciativa `docs/backdated-expenses/`.

14. **"Deuda con acreedores": lo que se debe *ahora*, no el histórico, con detalle y forma de saldar.** La vista "Deuda con acreedores" (`/financing/creditor-payables`) mostraba un único monto por acreedor: el histórico completo —toda cuota no revertida, paga o impaga, pasada o futura—, cuando lo que el usuario quiere ver es **qué debe hoy**: este ciclo más lo vencido. Tampoco había detalle (qué compras, qué cuotas) ni forma de pagar. La iniciativa lo reescribe en cuatro cortes; el pago es **sólo de visualización** —sella `Installment.PaidOnUtc`, sin banco, sin asiento al libro mayor, sin movimiento de plata—, coherente con que la deuda con un acreedor no tiene huella contable para el titular (decisión 7) y compatible hacia adelante (si algún día los acreedores van al libro, el comando de pago sólo *agrega* un asiento; el modelo de lectura —Σ de cuotas impagas— no cambia). **(Slice 1 — hecho)** cada fila pasa de un monto a **dos**: `dueNow` = Σ de las cuotas impagas y no revertidas cuyo **mes de pago** (`DueCycle`, decisión 9) es igual o anterior al ciclo de acreedor en curso —la mora de meses anteriores queda plegada en esta cifra, no enterrada— y `totalOwed` = Σ de todas las cuotas impagas y no revertidas, futuras incluidas; las pagas quedan fuera de ambas. El ciclo en curso es uniforme para todos los acreedores (corte 26, `PaymentPlan.CreditorCutoffDay`) y se resuelve del reloj (`TimeProvider`). El cliente muestra "Debe ahora $X · Total $Y". Sin cambio de esquema ni migración. `NextDueDate` y el desglose por cuenta quedan por ahora sobre todas las cuotas no revertidas (el sub-total por cuenta puede exceder `totalOwed` si hay cuotas pagas; cosmético, se ajusta después). **(Slice 2 — hecho)** una vista de detalle de sólo lectura en `/financing/creditor-payables/:creditorId` (`GET /v1/financing/creditor-payables/{creditorId}`) baja al detalle de un acreedor **agrupado por compra**: cada `PaymentPlan` es una sección con su descripción, fecha, total (Σ cuotas no revertidas) y saldo pendiente (Σ impagas no revertidas), y debajo sus cuotas — N de M, monto, mes de pago y un estado por cuota (`vencida` / `de este mes` / `futura` / `paga` / `revertida`, comparando el mes de pago con el ciclo de acreedor en curso). Las secciones se ordenan por fecha de compra descendente, como "Compras recientes". Un acreedor inexistente devuelve `404 Financing.CreditorNotFound` (el cliente lo muestra como un estado "no encontrado" amable). Sólo lectura: sin escrituras, sin Ledger, sin cambio de esquema ni migración; los botones "Pagar cuota" / "Deshacer" por fila y "Pagar la deuda completa" cuelgan de estas filas en los Slices 3–4. Cada fila de la lista "Deuda con acreedores" pasa a ser un enlace a este detalle. **(Slice 3 — hecho)** desde ese detalle, botones **"Pagar"** por cuota (cuando no está paga ni revertida) y **"Deshacer"** en las cuotas pagas: `POST /v1/financing/creditor-installments/{id}/pay` y `.../unpay` sellan y limpian `Installment.PaidOnUtc` con el reloj del servidor — **sólo visualización, sin banco, sin asiento al libro mayor, sin `MonthlyStatement`** (el espejo invocable por el usuario del sello retroactivo de la decisión 13). Ambas acciones mueven al instante las dos cifras de la lista (Slice 1) y los estados por cuota (Slice 2), vía un refetch en el cliente. Comandos nuevos `PayCreditorInstallmentCommand` / `UnpayCreditorInstallmentCommand` (ninguno lleva banco ni fecha) + `Installment.ClearPayment()`; una cuota de **tarjeta** se rechaza con 409 `Financing.NotACreditorInstallment`; deshacer una cuota ya impaga es un no-op exitoso; sin `ILedgerApi`, sin cambio de esquema ni migración, sin arista nueva entre módulos. Sin formulario ni selector de banco — pagar a un acreedor no toma ningún dato. **(Slice 4 — hecho)** desde ese mismo detalle, un botón **"Pagar la deuda completa"** en la cabecera: `POST /v1/financing/creditor-payables/{creditorId}/pay-full` sella `Installment.PaidOnUtc` con el reloj del servidor en **todas** las cuotas impagas y no revertidas de todas las compras del acreedor, en una sola transacción — **sólo visualización, sin banco, sin asiento al libro mayor, sin movimiento de plata**. Devuelve la cantidad de cuotas recién saldadas; cero por saldar → `0` (idempotente, no es error); un acreedor inexistente → `404 Financing.CreditorNotFound`. **No hay "deshacer" masivo** — el "Deshacer" por cuota del Slice 3 es la vía de recuperación. En el cliente el botón queda deshabilitado cuando no se debe nada y pide una confirmación en línea liviana antes de correr; al terminar, el detalle se refresca. Comando nuevo `PayCreditorFullDebtCommand : ICommand<int>` + handler; sin `ILedgerApi`, sin cambio de esquema ni migración, sin arista nueva entre módulos. El detalle está en `docs/owed-to-creditors/` (`00-overview.md` + `slice-1-current-cycle-outstanding.md` + `slice-2-creditor-detail-view.md` + `slice-3-pay-installment-and-undo.md` + `slice-4-pay-full-debt.md`). Con esto se cierra la iniciativa `docs/owed-to-creditors/`.

15. **Suscripciones: de auto-cobro a pago explícito.** El modelo de suscripciones cobraba automáticamente cada período vencido mediante un scheduler (`RenewDueSubscriptions`, cada minuto): si la app pasaba tiempo sin correr, al arrancar cobraba de una sola vez los N ciclos atrasados, inflando el "gastado este mes" del panel en N·monto — un bug de conteo, no una función. Se invierte el modelo a **pago explícito**: un período de suscripción pasa de pendiente a vencido con el tiempo, pero solo genera un asiento real cuando el usuario lo marca pagado a mano (Slice 2) o cuando, al dar de alta la suscripción, el ancla del ciclo en curso ya venció — ahí se asume pagado ese primer período y se asienta una única vez, nunca en cascada. El scheduler `RenewDueSubscriptions` y el comando `RenewSubscriptionCommand` se eliminan por completo, junto con el evento `SubscriptionRenewedIntegrationEvent` (nunca tuvo consumidor real — un huérfano de este mismo corte). El campo de estado de la suscripción pasa de "última renovación" (un instante) a `LastPaidPeriod` (una fecha) y gana dos comandos de dominio, `MarkCurrentPeriodPaid` / `RevertLastPayment`. `GET /v1/subscriptions/active` deriva y expone por fila un estado `paid | overdue | upcoming`, calculado contra el reloj del servidor, con una insignia en el cliente. **(Slice 1 — hecho)** exactamente lo anterior: purga única de los datos existentes (el modelo de datos cambia de forma incompatible, sin ruta de migración de los períodos ya cobrados), eliminación del auto-cobro, el cambio de campo + los dos comandos de dominio, la regla de alta condicional, y el estado derivado en la lista + su insignia. **(Slice 2 — hecho)** `POST /v1/subscriptions/{id}/pay`: liquida el próximo período no pagado — asienta una `X` real (`Dr Expense / Cr Funding`) fechada hoy vía `SubscriptionChargeCalculator` + `ILedgerApi`, invoca `MarkCurrentPeriodPaid` con el ancla del período que efectivamente se está pagando (el `NextDueDate` previo a la llamada, no el mes calendario actual), y avanza la fecha de vencimiento exactamente un mes — una suscripción varios meses atrasada se pone al día de a un período por click, nunca en lote. Nuevo código de conflicto `Subscriptions.SubscriptionAlreadyPaid` (idempotencia — el período de este mes ya está pagado). **(Slice 3 — hecho)** `POST /v1/subscriptions/{id}/unpay`: deshace un pago mal hecho — revierte la transacción real del Ledger vía `ILedgerApi.ReverseTransactionAsync` (una reversión dedicada, no un asiento compensatorio manual) y retrocede `LastPaidPeriod`/`NextDueDate` un mes vía `RevertLastPayment`. Como el comando de deshacer solo recibe el id de la suscripción (sin id de transacción), la propia agregación necesitó recordar cuál transacción revertir: `MarkCurrentPeriodPaid` gana un segundo parámetro `transactionId` y lo persiste en un nuevo campo `LastPaidTransactionId` (agregado también al alta "se asume pagado" — ambos caminos que pagan un período pasan por `MarkCurrentPeriodPaid`), y `RevertLastPayment` lo limpia. Nuevo código de conflicto `Subscriptions.SubscriptionNotPaid` (nada que deshacer si el período en curso no está pagado este mes calendario). Deshacer un pago de un mes calendario anterior al actual también se rechaza con este mismo código — deshacer solo cubre el pago recién hecho, no cualquier pago histórico. **(Slice 4 — hecho)** un panel "Active subscriptions" de solo lectura en el Dashboard, sin cambio de API (`status` ya viajaba en `GET /v1/subscriptions/active` desde la Slice 1) — cierra la iniciativa. El detalle está en `docs/subscriptions-rework/` (`00-overview.md` + `slice-1-explicit-pay-foundation.md` + `slice-2-pay-live-period.md` + `slice-3-undo-payment.md` + `slice-4-dashboard-subscriptions-block.md`).

16. **Soporte de dólares (USD), sin conversión.** El sistema asumía una única moneda de referencia (ARS) en todo lugar. Se agrega USD como segunda moneda soportada, **nunca convertible entre sí** — no hay tasa de cambio en ningún lado; cada operación pasa a *particionar por moneda* en lugar de asumir ARS (decisión de diseño en `docs/dollar-support/00-overview.md`, detalle técnico en D14). Se construye en cuatro cortes, cada uno probando el mismo patrón en un módulo distinto. **(Slice 1 — hecho)** la base: `Currency.Usd` + conjunto cerrado `{ARS, USD}`, la moneda persiste en el registro (nunca en la cuenta — una cuenta de banco/efectivo puede tener movimientos en ambas monedas), un gasto débito/efectivo puede cargarse en USD, y el panel deja de sumar montos de monedas distintas entre sí (el bug que esta iniciativa existe para prevenir). **(Slice 2 — hecho)** lo mismo para una compra con tarjeta: la división en cuotas es agnóstica a la moneda —mismo reparto exacto, la moneda solo viaja con el resultado—, y las vistas de tarjeta (resúmenes, cuotas, compras recientes) muestran cada una en la moneda correcta. **(Slice 3 — hecho)** lo mismo para una suscripción, **más la migración de los datos existentes**: como toda suscripción ya existente siempre se pagó en dólares, se ejecuta un único ajuste de datos —irreversible, decidido antes de escribir código— que pasa cada plantilla y todo su historial de cobros en el Ledger a USD; el resto de los datos (gastos, tarjetas) queda en ARS sin tocar. Consecuencia aceptada: la cuenta de banco/efectivo que fondea esas suscripciones pasa a mostrar dos saldos separados, uno en ARS y otro en USD — el mismo modelo poli-moneda de las slices anteriores, ahora con un caso real de una cuenta con movimientos en ambas. El detalle técnico (ambas migraciones, el mecanismo del flip) está en D14. La Slice 4 (terceros / Parties) queda por hacer — un tercero puede terminar con **dos** saldos de cuenta corriente, uno por moneda. El detalle está en `docs/dollar-support/` (`00-overview.md` + `slice-1-foundation-debit-expense.md` + `slice-2-card-expense.md` + `slice-3-subscriptions-and-migration.md` + slice 4).

---

## 10. Relación con el documento técnico

`diseno_definitivo.md` es la respuesta de ingeniería a este PRD: las decisiones D1–D11 ahí documentadas (Ledger como fuente de verdad contable, separación devengo/pago, orquestación eventual, etc.) existen para satisfacer las historias de usuario de la sección 6. Si el alcance de este documento cambia — se agrega, se saca, o se refasea un caso de uso — el documento técnico necesita revisarse en consecuencia; no son independientes.
