# Consolidación de Cogana Admin — 24/10/2026

**Versión estable publicada:** [1.13.0](https://github.com/Yhon-26/Cogana_Admin/releases/tag/v1.13.0). **Código del paquete y tag:** `bf85a65e3c18a5da84f63c49355b897fa0ef783d`. Se verificaron los seis archivos públicos, sus huellas SHA-256 y el feed de actualización. Esto acredita la publicación; la instalación y una compra real completa siguen pendientes de aceptación manual.

## Entrega por bloques

### 1. Sesión y guardados

- La autorización se aplica por solicitud. Los reintentos usan el token renovado y conservan los bytes y encabezados originales del cuerpo.
- Consultas simultáneas comparten una renovación. Una sesión cerrada no puede ser restaurada por una renovación pendiente.
- El cliente anterior se retira al cerrar sesión y libera sus recursos cuando terminan las solicitudes activas; no se acumula hasta salir del programa.
- El alta de producto usa un UUID estable del formulario y recupera la misma fila cuando se repite. Si ya se creó y falla la fotografía, el reintento continúa con esa imagen.
- La fotografía nueva se sube a una ruta versionada. La referencia anterior se conserva hasta confirmar la nueva fila. Los metadatos usan comparación con la ruta anterior para detectar una edición concurrente.
- Se verifica que los PATCH hayan devuelto una fila antes de confirmar su guardado.
- Los fallos del actualizador y de la exportación sencilla tienen una salida recuperable.
- La introducción deja continuar al panel aunque el disco no permita guardar su preferencia.

Storage y Postgres siguen siendo servicios distintos: una interrupción con resultado desconocido puede dejar un archivo sin referencia. La limpieza posterior es conservadora y no borra objetos todavía referenciados. No se afirma una transacción distribuida ni limpieza total de objetos huérfanos.

### 2. Operación de pedidos, inventario y tienda

Desde **Pedidos → Gestionar pedido → Atender pedido** se incorporan:

1. Confirmación de cobros manuales de Yape/Plin después de verificarlos; rechazo de un cobro y cancelación conforme al servidor.
2. Registro de diferencias o reembolsos ya liquidados mediante su referencia.
3. Cantidades preparadas en unidades base; Postgres calcula importes y consume las reservas.
4. Propuesta de sustituciones y registro de la aceptación/rechazo confirmado por el cliente.
5. Asignación de personal responsable del delivery.
6. Cierre de recojo o entrega con el código del cliente. El menú de transiciones deja de ofrecer `completed` por una ruta que el servidor rechaza.

Las operaciones conservan identificadores de idempotencia durante los reintentos de la ventana, bloquean pulsaciones concurrentes, consultan el resultado y exigen confirmación del operador para movimientos sensibles. Los códigos se introducen en un campo de contraseña, no se registran en logs ni forman parte de las claves del diccionario de reintentos.

Desde el detalle del lote se pueden consultar y abrir paquetes sellados existentes; la conversión y el movimiento se realizan por `open_inventory_package`. Esto no amplía automáticamente la recepción de lotes a todos los casos de suministro de paquetes.

La pestaña **Configuración → Operación y pagos** permite editar destinatarios de Yape/Plin, plazo del pago, estado de atención y reglas de franjas/capacidad. Cada sección se guarda por separado. El botón de guardado general se oculta en esa pestaña para evitar atribuirle el guardado de otra sección.

Se reutilizan RPC y columnas comprobados en el backend. **No se desplegó una migración ni se cambiaron RLS o Edge Functions.** Las operaciones que el servidor no admite por pago, stock, reservas, sustituciones, rol o estado siguen siendo rechazadas.

### 3. Pruebas y reproducción

- Se añadió `Pruebas/Operaciones` para sesiones concurrentes, integridad de archivos, fallos de imagen, altas repetidas, edición sin filas y contratos de operaciones.
- Las firmas de 13 funciones públicas se leyeron de `pg_proc` el 24/10/2026 y se conservaron en `Pruebas/Operaciones/contratos-backend.json`; las llamadas nuevas se comparan con esa instantánea.
- El arnés WPF comprueba el guardado parcial de producto, reintentos, estados de atención, paginación, manejo del fallo del actualizador, foco, paleta de contraste y tamaño compacto.
- Se añadió CI para Windows que ejecuta explícitamente los arneses y Node. Se fija la familia del SDK .NET 10 mediante `global.json`, con avance a versiones estables de su misma familia, y se generaron archivos de bloqueo de dependencias.

La instantánea de firmas verifica los nombres de parámetros del cliente. Las respuestas HTTP de las pruebas son simuladas: no equivalen a ejecutar las políticas de Postgres ni a confirmar dinero, stock o una entrega real. Las primeras ejecuciones de GitHub detectaron dos dependencias del equipo en el arnés antiguo: animaciones de Windows habilitadas y bienvenida ya completada. Se corrigieron solo las pruebas para respetar ambas configuraciones y recorrer el cierre de sesión sin depender de la preferencia del usuario. La [ejecución remota final](https://github.com/Yhon-26/Cogana_Admin/actions/runs/40132579159), sobre `d2220b5`, aprobó restauración bloqueada, compilación, acceso, Edge, infraestructura, escritorio completo y onboarding. El paquete publicado conserva su código de producto original `bf85a65`.

### 4. Rendimiento y accesibilidad

- Los ocho listados principales consultan páginas de 50 filas, con total real del servidor, búsqueda remota y espera de 300 ms al escribir.
- Las tarjetas comunes de Inicio solicitan cinco filas y conservan el recuento real. Frente al límite anterior de 250, reducen el máximo solicitado por tarjeta a una cincuentava parte; esto describe filas solicitadas, no una medición de latencia de producción.
- Los selectores de catálogo/promociones recorren páginas para no truncarse en el límite del servidor. Siguen reuniendo opciones en memoria; para catálogos muy grandes conviene medir y evolucionar a selección con búsqueda remota.
- La barra de acciones se reorganiza al reducir el ancho. El panel admite 900 × 560 unidades lógicas en la prueba y las ventanas limitan sus dimensiones iniciales al área de trabajo.
- Se restaura un foco visible en los estilos y se añaden etiquetas accesibles a las operaciones nuevas.
- El texto secundario del tema supera 4,5:1 sobre el fondo principal. Esto no certifica todos los colores o estados de la aplicación.
- Se adapta la paleta al alto contraste de Windows y se restaura al desactivarlo. La introducción respeta la preferencia de animación del sistema, también cuando cambia durante la sesión.

No se crearon índices a ciegas ni se eliminó ninguno por falta de uso observado en una base sin actividad representativa. Las pruebas no incluyen medición P95, varias horas de sesión, varios monitores ni todos los tamaños/porcentajes de Windows.

## Validación local

Los resultados finales y capturas se guardan en [Evidencias/Consolidacion_2026-10-24](D:/Cogana_Admin/Cogana.Admin/Evidencias/Consolidacion_2026-10-24).

| Comprobación | Resultado comprobado localmente |
|---|---|
| Release | Cero errores y cero advertencias. |
| Acceso | 33 comprobaciones. |
| Edge con Auth/BD simulados | 12 comprobaciones. |
| Regresiones de infraestructura/contratos | 33 comprobaciones. |
| Escritorio completo | 168 verificaciones; incluye las pruebas anteriores y 19 comprobaciones nuevas. |
| Onboarding | 41 comprobaciones; incluye geometría repetida de la base del arnés. |
| Restore bloqueado | Correcto para solución y tres arneses, incluyendo el destino win-x64; siete lockfiles generados. |

Los resultados se presentan por suite; sus recuentos no se suman como casos de negocio independientes. Las capturas utilizan datos y cuentas de prueba, sin sesión de producción. La versión mostrada por el arnés puede corresponder al ejecutable de pruebas, no al instalador del producto.

## Paquete local para revisión

Se publicaron en [GitHub Releases](https://github.com/Yhon-26/Cogana_Admin/releases/tag/v1.13.0) el instalador 1.13.0, el portátil, el paquete completo, el delta desde 1.12.0 y los dos archivos del canal de actualización. Se verificó que `latest` apunta a esta versión estable y que las huellas SHA-256 remotas coinciden. Velopack genera los feeds públicos para esta release; no son idénticos al índice local que contiene versiones históricas. Los paquetes no tienen firma Authenticode. No se ejecutó el instalador ni se modificó el canal de Supabase.

Las huellas de los paquetes finales se registran en `Evidencias/Consolidacion_2026-10-24/paquetes.json`. La preparación utiliza el runtime .NET Desktop 10 del equipo; el Setup contempla su instalación si falta.

## Trazabilidad del diagnóstico

| Hallazgo del análisis | Estado de este bloque |
|---|---|
| H01, H02 | Corregidos y cubiertos por regresión de token/cuerpo. |
| H03 | Integradas las operaciones vigentes y la interfaz; pendiente aceptación completa con backend real aislado. |
| H04 | Corregido el alta/reintento; prueba de formulario y de servicio. |
| H05 | Protegida la referencia anterior y verificada la confirmación; limpieza de huérfanos sigue siendo mejor esfuerzo. |
| H06 | Corregidas las rutas de actualización y exportación identificadas; no es una revisión de todas las excepciones del programa. |
| H07 | Corregido el bucle por fallo del archivo. |
| H08 | Paginación/búsqueda principal y selectores completos; pendiente medición con catálogo grande. |
| H09 | Incorporados estado, destinatarios y reglas operativas. |
| H10 | Renovación compartida y liberación de clientes retirados; queda ampliar cancelación uniforme de todos los detalles antiguos. |
| H11 | Confirmación de fila afectada; no se incorporó control de versión para todas las ediciones concurrentes. |
| H12 | Pendiente: transacción/recuperación de cambios de personal y auditoría en backend. |
| H13 | Pendiente de decidir y diseñar garantía global de cambio temporal fuera del cliente actual. |
| H14 | Mejoras implementadas y pruebas locales; pendiente validación con Narrador, hardware y escalas reales. |
| H15 | Avisos de seguridad identificados; no se modificaron ajustes de Auth, privilegios de cron ni políticas compartidas. |
| H16 | Arneses, firmas, SDK, lockfiles y CI añadidos; ejecución remota registrada y corregida la suposición de animaciones en el arnés. Pendiente entorno de integración real. |
| H17 | Documentación de entrega actualizada; restauración, copia independiente, firma e instalación limpia siguen pendientes. |
| H18 | Se incorporaron contratos específicos; no se realizó una reescritura general de arquitectura ni traducciones. |

## Comprobaciones pendientes antes de declarar operación completa

- [ ] Backend aislado: permisos por rol/tienda, fallos parciales y reintentos con las migraciones reales.
- [ ] Recorrido completo de recojo: pedido móvil, cobro o efectivo, preparación, diferencias/sustituciones, código y reporte.
- [ ] Recorrido completo de delivery: los pasos anteriores, repartidor y código de entrega.
- [ ] Conservar la sesión real durante más de una hora y combinar consultas, guardados y cierre.
- [ ] Instalación limpia y actualización desde la versión instalada anterior.
- [ ] Narrador, teclado, alto contraste del sistema, 100/125/150/200 % y varios monitores.
- [ ] Medición de rendimiento con volumen representativo y restauración del respaldo en un destino separado.
- [ ] Cerrar las tareas de backend H12/H13/H15 según el alcance acordado para ambos clientes.

No se solicitaron ni guardaron contraseñas, códigos, enlaces de recuperación o credenciales reales. No se ejecutaron compras ni cambios de configuración en producción durante la implementación.
