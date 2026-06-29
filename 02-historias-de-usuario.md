# Observability Lab — Historias de usuario por epica

Diez epicas (EP-01 a EP-10), cada una con: objetivo, alcance (que incluye y
que NO incluye a proposito) y dependencias de otras epicas. Dentro de cada
epica, las historias originales del backlog (HU-01 a HU-30) en formato
Como/Quiero/Para, con criterios de aceptacion y una definicion de hecho
(DoD).

Los IDs de historia (`HU-XX`) son estables y se mantienen igual que en la
version anterior — son los mismos que se referencian en
`03-diagrama-base-de-datos.md`, `04-contrato-api-rest.yaml` y
`05-contrato-eventos.yaml`.

## Regla de consistencia: el DoD de cada historia solo exige lo que ya existe

Esta version corrige un problema real de la version anterior: varias
historias de EP-02 a EP-05 tenian como Definicion de Hecho cosas como "se ve
un span en Jaeger" o "el contador incrementa en Prometheus", cuando esa
infraestructura no se construye hasta EP-06 y EP-07. Eso es circular — no se
puede exigir como completada una señal que depende de una epica futura.

La regla, de aqui en adelante:

- El DoD de una historia solo puede exigir señales verificables con lo que
  **ya existe hasta esa epica** (base de datos, consola de RabbitMQ, Mailpit,
  archivos en disco, logs de consola). Nada de Jaeger antes de EP-06, nada de
  Prometheus antes de EP-07.
- EP-06 y EP-07 no son capas que se agregan "al lado" del pipeline — **modifican
  el codigo ya construido** en EP-02 a EP-05 (los publishers y consumers) para
  agregarle instrumentacion y propagacion de trazas. Eso se indica
  explicitamente en el alcance de EP-06.
- Por eso, una historia como HU-06 (publicar `order.created`) no incluye
  todavia el header `traceparent` — eso lo agrega HU-15 cuando vuelve sobre
  ese mismo codigo. No es que se olvide; es que construirlo antes de tener
  OpenTelemetry configurado no tiene como verificarse.

---

## Indice de epicas

| Epica | Nombre | Historias | Fase del backlog original |
|---|---|---|---|
| EP-01 | Fundacion tecnica | HU-01, HU-02 | Semana 1 |
| EP-02 | Gestion de ordenes (API REST) | HU-03, HU-04 | Semana 1 |
| EP-03 | Mensajeria y procesamiento asincrono | HU-05, HU-06, HU-07 | Semana 2 |
| EP-04 | Facturacion | HU-08, HU-09 | Semana 3 |
| EP-05 | Notificacion al cliente por correo | HU-10, HU-11, HU-12 | Semana 4 |
| EP-06 | Trazabilidad distribuida (logs y trazas) | HU-13, HU-14, HU-15 | Semana 5 |
| EP-07 | Metricas tecnicas y de negocio | HU-16, HU-17, HU-18 | Semana 6 |
| EP-08 | Resiliencia ante fallos | HU-19, HU-20, HU-21 | Semana 7 (Fase 2) |
| EP-09 | Dashboards | HU-22, HU-23 | Semana 8 (Fase 3) |
| EP-10 | Backlog de extension (bonus) | HU-24 a HU-30 | Bonus |

---

## EP-01 — Fundacion tecnica

**Objetivo:** tener la base sobre la cual se construye todo lo demas: la
solucion .NET, la conexion a PostgreSQL y el modelo de dominio.

**Alcance — incluye:**
- Estructura de la solucion y proyecto de API.
- Conexion a PostgreSQL y migraciones de Entity Framework Core.
- Entidades de dominio: `Customer`, `Product`, `Order`, `OrderItem`,
  `Invoice` (ver `03-diagrama-base-de-datos.md`).

**Alcance — NO incluye:**
- Ningun endpoint HTTP (eso es EP-02).
- Ninguna logica de negocio, mensajeria u observabilidad.

**Depende de:** —

### HU-01 — Base de la solucion .NET
**Rol:** Equipo de desarrollo
**Historia:** Como desarrollador del proyecto, quiero tener la solucion
.NET, el proyecto de API, PostgreSQL y Entity Framework Core configurados,
para contar con una base sobre la cual construir el resto del sistema.
**Criterios de aceptacion:**
- Existe una solucion `.sln` con un proyecto de API ejecutable.
- La API se conecta a una instancia de PostgreSQL levantada por Docker Compose.
- `dotnet ef migrations add InitialCreate` genera una migracion sin errores.
**DoD:** la API levanta sin errores y aplica las migraciones contra Postgres
en un entorno limpio (`docker compose up` + `dotnet run`).
**Depende de:** —

### HU-02 — Modelo de dominio
**Rol:** Equipo de desarrollo
**Historia:** Como desarrollador, quiero modelar `Order`, `OrderItem`,
`Product`, `Customer` e `Invoice` con sus campos minimos, para representar
el dominio de ordenes de forma consistente en toda la aplicacion.
**Criterios de aceptacion:**
- Las entidades y campos coinciden con `03-diagrama-base-de-datos.md` y el
  contrato OpenAPI.
- `Order.Status` es un enum (`Pending`, `Processing`, `Processed`, `Failed`).
**DoD:** el modelo compila; las migraciones reflejan exactamente las 5
tablas del diagrama de base de datos.
**Depende de:** HU-01

---

## EP-02 — Gestion de ordenes (API REST)

**Objetivo:** que un cliente pueda crear una orden y consultar su estado via
HTTP, usando solo la API y PostgreSQL. Esta epica es deliberadamente
"sorda y muda": no conoce RabbitMQ ni nada de lo que pasa despues.

**Alcance — incluye:**
- `POST /orders` (crear) y `GET /orders/{id}` (consultar), exactamente como
  estan definidos en `04-contrato-api-rest.yaml`.
- Validacion de cliente y productos existentes.
- Persistencia de la orden con estado `Pending`.

**Alcance — NO incluye:**
- Publicar el evento `order.created` — eso es HU-06 (EP-03), que **extiende**
  el endpoint construido aqui una vez que RabbitMQ existe. Antes de EP-03,
  `POST /orders` simplemente no notifica a nadie; el flujo se queda en
  `Pending` para siempre, y eso es un resultado esperado y correcto en este
  punto del proyecto, no un defecto.
- Calculo del total de la orden (lo hace el Order Processing Worker en EP-03).
- Generacion de factura o envio de correo (EP-04 y EP-05).
- Autenticacion o autorizacion de clientes (fuera del alcance del proyecto
  completo, ver `01-arquitectura-y-alcance.md` seccion 2).

**Depende de:** EP-01

### HU-03 — Crear ordenes
**Rol:** Cliente
**Historia:** Como cliente, quiero crear una orden con uno o varios
productos, para iniciar el proceso de compra.
**Criterios de aceptacion (ver `04-contrato-api-rest.yaml` para el detalle exacto):**
- `POST /orders` valida que existan el cliente y todos los productos.
- La orden se persiste con estado `Pending` y se responde `201 Created` con
  el `orderId`.
- Si el request es invalido (sin items, cliente o producto inexistente), se
  responde `400` con un cuerpo `ProblemDetails`.
**DoD:** la orden y sus items quedan en PostgreSQL con estado `Pending`, y
`GET /orders/{id}` la devuelve con esos mismos datos. (La publicacion del
evento llega en HU-06; no es parte del DoD de esta historia.)
**Depende de:** HU-02

### HU-04 — Consultar una orden
**Rol:** Cliente
**Historia:** Como cliente, quiero consultar el estado de mi orden, para
saber en que punto del proceso se encuentra (pendiente, procesada,
facturada, enviada).
**Criterios de aceptacion:**
- `GET /orders/{id}` devuelve la orden con sus items, estado actual y la
  factura asociada si ya existe.
- Si la orden no existe, responde `404` con `ProblemDetails`.
**DoD:** el endpoint responde en menos de 200ms con datos de prueba locales.
**Depende de:** HU-03

---

## EP-03 — Mensajeria y procesamiento asincrono de ordenes

**Objetivo:** desacoplar la creacion de la orden de su procesamiento usando
RabbitMQ, y calcular el total de la orden de forma asincrona. Esta epica es
la primera que toca codigo de EP-02 ya existente: extiende `POST /orders`
para que publique, ademas de persistir.

**Alcance — incluye:**
- Topologia de RabbitMQ: exchange `orders.events` y las 3 colas del flujo
  feliz (ver `05-contrato-eventos.yaml`).
- Modificar el endpoint de HU-03 para publicar `order.created` despues de
  persistir la orden.
- El Order Processing Worker: consume `order.created`, calcula el total,
  cambia el estado de la orden y publica `order.processed`.

**Alcance — NO incluye:**
- El header `traceparent` ni ningun span de OpenTelemetry — los mensajes
  viajan sin contexto de traza hasta EP-06 (HU-15), que vuelve sobre este
  mismo codigo para agregarlo. Verificar "la traza" todavia no es posible:
  lo unico que se puede verificar aqui es que el mensaje llega a la cola
  correcta con el payload correcto.
- Reintentos ni Dead Letter Queue (eso es EP-08, Fase 2).
- Generacion de factura o envio de correo (EP-04, EP-05).

**Depende de:** EP-01, EP-02

### HU-05 — Infraestructura de RabbitMQ
**Rol:** Equipo de desarrollo
**Historia:** Como desarrollador, quiero tener RabbitMQ levantado con el
exchange y las colas del contrato de eventos, para que los procesos puedan
comunicarse de forma asincrona.
**Criterios de aceptacion:**
- El exchange `orders.events` (topic, durable) existe.
- Las colas `order-processing-worker`, `invoice-worker` y `email-worker`
  existen y estan ligadas a su routing key segun `05-contrato-eventos.yaml`.
**DoD:** la topologia es visible y correcta en la consola de administracion
de RabbitMQ (`localhost:15672`).
**Depende de:** HU-01

### HU-06 — Publicar `order.created`
**Rol:** Equipo de desarrollo
**Historia:** Como API, quiero publicar el evento `order.created` al crear
una orden, para que el resto del pipeline pueda reaccionar sin que la API
tenga que conocer a sus consumidores.
**Criterios de aceptacion:**
- Extiende el endpoint `POST /orders` de HU-03: despues de persistir la
  orden, publica el evento en el mismo flujo del request.
- El payload cumple el esquema `OrderCreatedPayload` del contrato AsyncAPI.
**DoD:** el mensaje aparece en la cola `order-processing-worker`, visible
desde la consola de RabbitMQ, con el payload correcto. (Sin `traceparent`
todavia — eso lo agrega HU-15.)
**Depende de:** HU-03, HU-05

### HU-07 — Order Processing Worker
**Rol:** Equipo de desarrollo
**Historia:** Como sistema, quiero un worker que consuma `order.created`,
calcule el total de la orden y actualice su estado, para completar el
procesamiento sin bloquear la respuesta de la API.
**Criterios de aceptacion:**
- Cambia el estado a `Processing` y luego a `Processed`.
- Calcula `TotalAmount` como la suma de `Quantity * UnitPrice` de los items.
- Publica `order.processed` al terminar.
- Si la orden no existe, descarta el mensaje (ack) y registra un log de
  advertencia sin reintentar indefinidamente.
**DoD:** consultando la orden por `GET /orders/{id}` despues de unos
segundos, su estado es `Processed` y `TotalAmount` es correcto; el mensaje
`order.processed` aparece en la cola `invoice-worker`.
**Depende de:** HU-06

---

## EP-04 — Facturacion

**Objetivo:** generar un comprobante en PDF de cada orden procesada y
dejarlo listo para ser enviado.

**Alcance — incluye:**
- El Invoice Worker: consume `order.processed`, genera el PDF, lo guarda en
  almacenamiento local y publica `invoice.generated`.
- Registro de la factura en la tabla `Invoices`.

**Alcance — NO incluye:**
- Almacenamiento en MinIO (mejora bonus, HU-25/EP-10).
- Envio del correo (EP-05).
- `traceparent` o spans — igual que en EP-03, esto se agrega en EP-06.

**Depende de:** EP-03

### HU-08 — Generar la factura en PDF
**Rol:** Cliente
**Historia:** Como cliente, quiero que se genere una factura en PDF de mi
orden procesada, para tener un comprobante de mi compra.
**Criterios de aceptacion:**
- El Invoice Worker consume `order.processed`.
- Genera un PDF con cliente, items, cantidades, precios y total.
- Guarda el archivo en almacenamiento local (`Invoices/`).
- Registra la factura en PostgreSQL (`OrderId`, `FilePath`, `GeneratedAt`).
**DoD:** el archivo PDF existe en disco con el contenido esperado (cliente,
items, total correctos) y el registro en la tabla `Invoices` le corresponde.
**Depende de:** HU-07

### HU-09 — Publicar `invoice.generated`
**Rol:** Equipo de desarrollo
**Historia:** Como Invoice Worker, quiero publicar `invoice.generated` al
terminar de generar la factura, para que el Email Worker pueda continuar el
flujo.
**Criterios de aceptacion:** el payload cumple `InvoiceGeneratedPayload`.
**DoD:** el mensaje aparece en la cola `email-worker`, visible desde la
consola de RabbitMQ, con el payload correcto.
**Depende de:** HU-08

---

## EP-05 — Notificacion al cliente por correo

**Objetivo:** cerrar el ciclo de la orden notificando al cliente, con la
factura adjunta, sin enviar correos reales durante el desarrollo.

**Alcance — incluye:**
- El Email Worker: consume `invoice.generated`, envia el correo y publica
  `email.sent`.
- Un servidor SMTP de pruebas (Mailpit) en el entorno local.

**Alcance — NO incluye:**
- Plantillas de correo enriquecidas (HTML, marca, etc.) — el correo es
  texto simple con el PDF adjunto.
- Reintentos de envio ante fallo de SMTP (eso es EP-08).
- `traceparent` o spans — igual que en EP-03 y EP-04, esto se agrega en EP-06.

**Depende de:** EP-04

### HU-10 — Email Worker
**Rol:** Cliente
**Historia:** Como cliente, quiero recibir un correo con mi factura
adjunta, para tener constancia de mi compra sin tener que entrar a ningun
sistema.
**Criterios de aceptacion:**
- El Email Worker consume `invoice.generated`.
- Construye un correo con el PDF adjunto y lo envia.
- Marca la factura como `EmailSent = true` con su `EmailSentAt`.
**DoD:** el correo aparece en Mailpit (entorno local) con el adjunto y el
asunto correctos; el registro de la factura en PostgreSQL queda con
`EmailSent = true`.
**Depende de:** HU-09

### HU-11 — Entorno SMTP de pruebas
**Rol:** Equipo de desarrollo
**Historia:** Como desarrollador, quiero un servidor SMTP de pruebas
(Mailpit), para poder validar el envio de correos sin riesgo de enviar
correos reales durante el desarrollo.
**Criterios de aceptacion:** Mailpit esta levantado en Docker Compose y
accesible en su UI web.
**DoD:** un correo enviado por el Email Worker es visible en la UI de
Mailpit.
**Depende de:** HU-01

### HU-12 — Publicar `email.sent`
**Rol:** Equipo de desarrollo
**Historia:** Como Email Worker, quiero publicar `email.sent` al confirmar
el envio, para cerrar el flujo y dejar registro de que el ciclo de la orden
termino.
**Criterios de aceptacion:** el payload cumple `EmailSentPayload`.
**DoD:** el mensaje `email.sent` se publica con el payload correcto,
visible desde la consola de RabbitMQ. (Todavia no hay nada que lo consuma
ni ninguna traza que cerrar — eso llega en EP-06.)
**Depende de:** HU-10

---

## EP-06 — Trazabilidad distribuida (logs y trazas)

**Objetivo:** poder seguir una orden de extremo a extremo en Jaeger, y
saltar de cualquier log sospechoso a su traza completa en un solo paso. Esta
es la epica que justifica todo el proyecto — y es la primera que tiene
permitido mencionar Jaeger en su DoD, porque es la primera que lo construye.

**Importante — esta epica modifica codigo ya existente, no solo agrega
codigo nuevo:** instrumentar significa volver sobre los 4 puntos de
publicacion (HU-06, HU-07, HU-09, HU-12) y los 3 puntos de consumo (HU-07,
Invoice Worker, Email Worker) construidos en EP-03, EP-04 y EP-05, para
agregarles la inyeccion/extraccion del header `traceparent`. No es una capa
aparte; es el mismo codigo, ahora instrumentado.

**Alcance — incluye:**
- Logs estructurados (Serilog) en los 4 procesos.
- Enriquecimiento de logs con `TraceId`/`SpanId`.
- Instrumentacion OpenTelemetry de API, EF Core/Npgsql, RabbitMQ y los 3
  workers, con propagacion de `traceparent` via headers AMQP (agregado ahora
  a los publishers/consumers de EP-03 a EP-05).
- Exportacion OTLP a Jaeger.

**Alcance — NO incluye:**
- Metricas (eso es EP-07, aunque comparten el mismo SDK de OpenTelemetry).
- OpenTelemetry Collector como intermediario (bonus, HU-27/EP-10) — se
  exporta directo a Jaeger via OTLP nativo.

**Depende de:** EP-03, EP-04, EP-05 (no se puede instrumentar lo que no
existe; antes de esto no habia nada end-to-end que trazar).

### HU-13 — Logs estructurados
**Rol:** Ingeniero de observabilidad
**Historia:** Como ingeniero de observabilidad, quiero logs estructurados
(no texto libre) en cada servicio, para poder filtrar y correlacionar
eventos sin parsear strings.
**Criterios de aceptacion:**
- Serilog configurado en los 4 procesos (API + 3 workers).
- Los logs incluyen propiedades estructuradas (`OrderId`, `CustomerId`,
  `Status`, etc.), no solo un mensaje de texto.
**DoD:** un log de ejemplo se puede filtrar por `OrderId` sin usar regex
sobre texto libre.
**Depende de:** HU-01

### HU-14 — Correlacion por TraceId
**Rol:** Ingeniero de observabilidad
**Historia:** Como ingeniero de observabilidad, quiero que cada log incluya
el `TraceId`/`SpanId` de la traza activa, para poder saltar de un log
sospechoso a su traza completa en Jaeger en un solo paso.
**Criterios de aceptacion:** todos los logs de una misma orden, sin importar
en que proceso se generaron, comparten el mismo `TraceId`.
**DoD:** dado un `TraceId` de un log, se puede pegar en Jaeger y encontrar
la traza completa correspondiente.
**Depende de:** HU-13, HU-15

### HU-15 — Trazas distribuidas con OpenTelemetry
**Rol:** Ingeniero de observabilidad
**Historia:** Como ingeniero de observabilidad, quiero instrumentar la API,
EF Core/Npgsql, RabbitMQ y los 3 workers con OpenTelemetry, agregando la
propagacion del header `traceparent` a los publishers y consumers de HU-06,
HU-07, HU-09, HU-12 y los workers de EP-04/EP-05, para poder seguir una
orden completa de extremo a extremo.
**Criterios de aceptacion:**
- El contexto de traza se propaga via el header AMQP `traceparent` en los 4
  eventos del contrato (`order.created`, `order.processed`,
  `invoice.generated`, `email.sent`).
- Se exporta via OTLP a Jaeger.
**DoD:** una orden creada genera una traza visible en Jaeger con al menos 5
spans (API, 3 workers, mas los spans de base de datos), sin saltos de
contexto.
**Depende de:** HU-07, HU-09, HU-12

---

## EP-07 — Metricas tecnicas y de negocio

**Objetivo:** cuantificar la salud y el rendimiento del pipeline que ya
viene funcionando desde EP-02 a EP-05, sin depender solo de leer trazas
individuales. Igual que EP-06, esta epica es la primera autorizada a exigir
Prometheus en su DoD.

**Alcance — incluye:**
- Endpoint/puerto de metricas en cada uno de los 4 procesos.
- Contadores de negocio: ordenes creadas/procesadas, facturas generadas,
  correos enviados, errores — instrumentando los mismos eventos que ya
  ocurren desde EP-02 a EP-05.
- Histogramas de duracion por etapa.

**Alcance — NO incluye:**
- Visualizacion en dashboards (eso es EP-09 — aqui solo se exponen y
  almacenan las metricas, no se construyen paneles).
- Alertas (bonus, HU-24/EP-10).

**Depende de:** EP-02, EP-03, EP-04, EP-05

### HU-16 — Endpoint de metricas Prometheus
**Rol:** Ingeniero de operaciones
**Historia:** Como ingeniero de operaciones, quiero que cada proceso
exponga un endpoint `/metrics` (o puerto dedicado en el caso de los
workers), para que Prometheus pueda scrapearlos.
**Criterios de aceptacion:** Prometheus muestra los 4 targets (API + 3
workers) como `UP` en `/targets`.
**DoD:** las metricas tecnicas por defecto (requests, GC, etc.) son
visibles en Prometheus.
**Depende de:** HU-01

### HU-17 — Metricas de negocio (contadores)
**Rol:** Responsable de negocio
**Historia:** Como responsable de negocio, quiero ver cuantas ordenes se
crean, procesan, facturan y notifican, para entender la salud del flujo sin
tener que leer logs.
**Criterios de aceptacion:** existen los contadores `orders_created_total`,
`orders_processed_total`, `invoices_generated_total`, `emails_sent_total` y
`processing_errors_total`.
**DoD:** los 5 contadores son consultables en Prometheus y cambian al crear
una orden de prueba.
**Depende de:** HU-03, HU-07, HU-09, HU-12, HU-16

### HU-18 — Metricas de negocio (histogramas)
**Rol:** Responsable de negocio
**Historia:** Como responsable de negocio, quiero saber cuanto tarda cada
etapa del pipeline, para detectar cuellos de botella antes de que se
conviertan en quejas de clientes.
**Criterios de aceptacion:** existen los histogramas
`order_processing_duration_seconds`, `invoice_generation_duration_seconds` y
`email_send_duration_seconds`.
**DoD:** los histogramas muestran percentiles (p50/p95) coherentes con el
tiempo real observado al probar manualmente.
**Depende de:** HU-17

---

## EP-08 — Resiliencia ante fallos (Fase 2)

**Objetivo:** que un fallo transitorio (timeout de SMTP, caida puntual de
RabbitMQ, fallo de generacion de PDF) no se convierta en un mensaje
perdido.

**Alcance — incluye:**
- Politica de reintentos (Polly) en los 3 workers.
- Mecanismo para simular fallos en un entorno de pruebas.
- Dead Letter Queue para mensajes que agotan sus reintentos.

**Alcance — NO incluye:**
- Alertas automaticas cuando algo cae en la DLQ (bonus, HU-24/EP-10).
- Reprocesamiento automatico de la DLQ — el reproceso es manual en esta
  version.

**Importante:** esta epica queda explicitamente fuera del contrato de
eventos v1.0.0 (ver nota de evolucion en `05-contrato-eventos.yaml`). No se
implementa hasta tener el flujo feliz completamente observable, para no
construir manejo de errores sobre un sistema del que todavia no se sabe como
falla.

**Depende de:** EP-06 (sin trazas, un reintento es invisible; no se puede
validar que la resiliencia funcione si no se puede ver).

### HU-19 — Reintentos ante fallos temporales
**Rol:** Operador del sistema
**Historia:** Como operador del sistema, quiero que los workers reintenten
automaticamente ante fallos transitorios (timeout de SMTP, fallo puntual de
RabbitMQ), para que un error momentaneo no se convierta en un mensaje
perdido.
**Criterios de aceptacion:** los workers usan una politica de reintento
(ej. Polly) con backoff antes de enviar un mensaje a la Dead Letter Queue.
**DoD:** un fallo simulado se recupera solo dentro del numero de reintentos
configurado, visible como spans de error seguidos de un span exitoso en
Jaeger.
**Depende de:** HU-15

### HU-20 — Simulacion de fallos
**Rol:** Operador del sistema
**Historia:** Como operador del sistema, quiero poder simular fallos
(timeout de SMTP, fallo de generacion de PDF, desconexion de RabbitMQ),
para validar que las metricas y trazas de error se vean como se espera
antes de que ocurra un incidente real.
**Criterios de aceptacion:** existe un mecanismo (flag/variable de entorno)
para forzar cada tipo de fallo en un entorno de pruebas.
**DoD:** cada fallo simulado incrementa `processing_errors_total` y se ve
como un span con estado `Error` en Jaeger.
**Depende de:** HU-19

### HU-21 — Dead Letter Queue
**Rol:** Operador del sistema
**Historia:** Como operador del sistema, quiero que los mensajes que
agoten sus reintentos terminen en una Dead Letter Queue, para poder
inspeccionarlos y reprocesarlos manualmente en lugar de perderlos.
**Criterios de aceptacion:** existe el exchange `orders.events.dlx` y la
cola `orders.dead-letter`; un mensaje que agota reintentos aparece ahi.
**DoD:** un mensaje fallido de prueba es visible en `orders.dead-letter`
con su `traceparent` original intacto.
**Depende de:** HU-19, HU-20

---

## EP-09 — Dashboards (Fase 3)

**Objetivo:** visualizar de forma centralizada el estado tecnico y de
negocio del sistema, sin tener que consultar Prometheus directamente.

**Alcance — incluye:**
- Un dashboard tecnico (requests/min, tasa de error, latencia).
- Un dashboard de negocio (ordenes, facturas, correos, tiempo promedio).
- Ambos en Grafana, sobre la fuente de datos Prometheus.

**Alcance — NO incluye:**
- Alertas (bonus, HU-24/EP-10).
- Dashboard de saturacion de colas de RabbitMQ (bonus, HU-30/EP-10).

**Depende de:** EP-07

### HU-22 — Dashboard tecnico en Grafana
**Rol:** Ingeniero de operaciones
**Historia:** Como ingeniero de operaciones, quiero un dashboard tecnico
(requests/min, tasa de error, latencia), para monitorear la salud del
sistema en un solo lugar.
**Criterios de aceptacion:** el dashboard lee de la fuente de datos
Prometheus ya provisionada.
**DoD:** el dashboard se actualiza en tiempo real al generar trafico de
prueba.
**Depende de:** HU-16, HU-17, HU-18

### HU-23 — Dashboard de negocio en Grafana
**Rol:** Responsable de negocio
**Historia:** Como responsable de negocio, quiero un dashboard con ordenes
creadas, facturas generadas, correos enviados y tiempo promedio por orden,
para tener visibilidad sin pedirle el dato a un desarrollador.
**Criterios de aceptacion:** el dashboard usa exclusivamente los contadores
e histogramas de negocio (EP-07).
**DoD:** los numeros del dashboard coinciden con una verificacion manual
contra Prometheus.
**Depende de:** HU-22

---

## EP-10 — Backlog de extension (bonus)

**Objetivo:** extensiones opcionales que no forman parte del compromiso de
aprendizaje inicial. Se evaluan segun tiempo e interes disponible despues de
EP-01 a EP-09.

**Alcance — incluye:** las siete historias bonus listadas abajo,
unicamente.

**Alcance — NO incluye:** nada de esta epica es bloqueante para considerar
el proyecto "terminado" segun el objetivo de aprendizaje original. Si una
historia de esta epica empieza a sumar complejidad de negocio en lugar de
complejidad observable, se descarta (ver `01-arquitectura-y-alcance.md`
seccion 2).

**Depende de:** EP-01 a EP-09 (cada historia bonus depende especificamente
de la epica que extiende; ver el detalle en cada una).

### HU-24 — Alertas en Grafana
**Historia:** Como operador del sistema, quiero alertas cuando la tasa de
error supere un umbral, para reaccionar antes de que un cliente se queje.
**Depende de:** EP-09 (HU-22)

### HU-25 — MinIO para las facturas
**Historia:** Como desarrollador, quiero mover el almacenamiento de PDFs de
disco local a MinIO, para practicar integraciones tipo S3 y observabilidad
de llamadas externas.
**Depende de:** EP-04 (HU-08)

### HU-26 — Health checks
**Historia:** Como operador del sistema, quiero endpoints de health check
en los 4 procesos, para integrarlos con orquestadores o monitoreo externo.
**Depende de:** EP-01 (HU-01)

### HU-27 — OpenTelemetry Collector
**Historia:** Como ingeniero de observabilidad, quiero centralizar la
telemetria en un OTel Collector antes de Jaeger/Prometheus, para practicar
el patron de produccion recomendado.
**Depende de:** EP-06 (HU-15), EP-07 (HU-16)

### HU-28 — Docker Compose completo
**Historia:** Como desarrollador, quiero un unico `docker compose up` que
levante absolutamente todo (infra + los 4 procesos), para onboarding
instantaneo.
**Depende de:** EP-01 a EP-09

### HU-29 — Load testing con k6
**Historia:** Como ingeniero de operaciones, quiero generar carga sintetica
con k6, para validar que el pipeline se comporta bien bajo presion y que
las metricas reflejan ese comportamiento.
**Depende de:** EP-09 (HU-22)

### HU-30 — Dashboard de saturacion de colas
**Historia:** Como ingeniero de operaciones, quiero ver la profundidad de
las colas de RabbitMQ en Grafana, para detectar cuando un worker no puede
mantener el ritmo de los mensajes entrantes.
**Depende de:** EP-09 (HU-22)
