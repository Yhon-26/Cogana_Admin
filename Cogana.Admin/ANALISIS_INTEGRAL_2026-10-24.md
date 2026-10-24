# Análisis integral de Cogana Admin

**Fecha:** 24 de octubre de 2026, America/Lima.
**Solución:** `D:\Cogana_Admin\Cogana.Admin\Cogana.Admin.slnx`.
**Código analizado:** commit `4b9fa93`, versión `1.12.0`.
**Alcance:** diagnóstico técnico y funcional previo a mejoras. No se aplicaron correcciones, migraciones, cambios de datos, commits ni publicaciones.

## 1. Dictamen

Cogana Admin tiene una base funcional importante: acceso administrativo, once módulos de administración más Inicio, consultas reales, mantenimiento de catálogo, lotes, promociones, usuarios, configuración, reportes y distribución mediante Velopack. La solución compila en Release y las suites disponibles pasan.

**El siguiente paso debería ser consolidar la operación del negocio.** La principal brecha es que el backend compartido evolucionó y el escritorio todavía no integra todas las operaciones necesarias para atender un pedido completo. También se reprodujeron defectos en la renovación de sesión y en los reintentos de imágenes. Estas cuestiones tienen más impacto inmediato que ampliar las animaciones o rediseñar pantallas.

No se asigna un porcentaje de avance: faltan criterios de aceptación acordados para la operación completa y una validación actual con datos de negocio. La madurez observada es la de una aplicación administrativa avanzada, con cobertura funcional parcial del ciclo comercial completo.

## 2. Cómo se realizó la revisión

Se revisaron la estructura de los cuatro proyectos, arranque y sesión, contratos, servicios REST, ventanas y ViewModels, scripts SQL, Edge Function, empaquetado, documentación y pruebas. Se ejecutaron las comprobaciones locales existentes y seis escenarios adicionales con el código real y un manejador HTTP simulado. Se inspeccionaron tres capturas nuevas generadas por el arnés WPF.

Se consultó Supabase exclusivamente para lectura: estado del proyecto, tablas y recuentos, políticas, definiciones y firmas de funciones, restricciones de inventario, buckets, migraciones y avisos de seguridad/rendimiento. Se consultó también la publicación pública más reciente de GitHub.

Se diferencian tres niveles de evidencia:

- **Comprobado ahora:** compilación, resultados locales, estado/metadatos leídos del backend y publicación pública.
- **Deducido del código:** una ruta de ejecución concreta tiene un comportamiento problemático, sin provocar deliberadamente ese fallo en producción.
- **Pendiente:** recorrido comercial real, comportamiento en otros equipos, pruebas de carga, instalación limpia o recuperación operativa.

Las notas históricas y el README sirvieron como orientación; no sustituyeron las verificaciones actuales. No se inició sesión usando contraseñas del usuario, no se enviaron correos y no se crearon pedidos de prueba en producción.

## 3. Arquitectura y organización

| Capa | Estado observado | Evaluación |
|---|---|---|
| Dominio | Entidades y enumeraciones sencillas. | Ligera; muchas reglas efectivas viven en Postgres y en formularios. |
| Aplicación | Contratos de servicios y modelos de transferencia. | Separación útil para pruebas; predominan contratos y DTO, con pocos casos de uso coordinados. |
| Infraestructura | Cliente HTTP propio, Supabase REST/Auth/Storage/Functions y Velopack. | Dependencias externas concentradas, pero el cliente HTTP es un punto crítico compartido. |
| Escritorio | WPF, ViewModels, comandos, controles personalizados y lógica en code-behind. | MVVM parcial. La navegación, formularios y composición de servicios aún concentran responsabilidades. |

Inventario: **147 archivos C#/XAML de producto: 130 C# y 17 XAML**. Hay **16 ventanas** incluyendo la principal; el README aún menciona 15. Las pruebas están en dos ejecutables independientes, fuera de la solución principal.

La dirección de referencias entre proyectos es razonable. `App` compone los servicios explícitamente y entrega interfaces a ventanas y ViewModels, permitiendo sustitutos de prueba. No es necesario reemplazar de inmediato esta composición por un contenedor de dependencias.

Los puntos de mantenimiento más sensibles son el cliente REST común, `VentanaPrincipalViewModel`, `MainWindow.xaml.cs`, el servicio de catálogo y la función `admin-users`. Los módulos se identifican con textos como `Productos` o `Inventario y lotes`, y varias decisiones se apoyan en etiquetas traducidas. Por ejemplo, las transiciones usan el texto `Recojo` para distinguir modalidad. Conviene separar progresivamente identificadores de negocio y etiquetas visibles.

El dominio y `IRepositorioProductos` conservan elementos que no dirigen actualmente el flujo principal. No se justifica una reescritura completa de capas; sí extraer casos de uso concretos cuando se incorporen pagos, preparación, entrega y recuperación de operaciones parciales.

## 4. Estado funcional por módulo

“Implementado” significa que se encontró código y contratos; no equivale a validar hoy una operación real de extremo a extremo.

| Módulo | Implementado | Brecha principal |
|---|---|---|
| Inicio | Resumen, pedidos recientes, tarjetas configurables, menú plegable y actualización manual. | Indicadores y tarjetas pueden representar consultas de momentos distintos; no hay suscripción Realtime encontrada. |
| Productos | Listado, alta, edición, activación, subida y visor de imagen. | Recuperar fallos entre alta y fotografía; consistencia entre Storage y filas. |
| Categorías | Alta, edición, orden y activación. | Paginación y pruebas de dependencias con catálogo real. |
| Presentaciones | Tipos, cantidad base, precio, estado y condición sellada. | Recorrido operativo de paquetes sellados y apertura de paquetes. |
| Inventario y lotes | Registrar lote por RPC, detalle, movimientos y ajuste con identificador de operación. | La interfaz se centra en cantidad suelta; no integra la apertura de paquetes disponible en backend. |
| Proveedores | Alta, edición, consulta y activación. | Validación de datos fiscales/contacto y recorridos con abastecimiento real. |
| Pedidos | Detalle, artículos, pagos, entrega, historial y cambio de estado. | No incorpora confirmación/rechazo de pagos, finalización de cantidades, asignación de repartidor ni cierre con código. |
| Clientes | Consulta de perfil comercial, direcciones, preferencias e historial. | Consulta principalmente; no confundir con gestión integral de clientes o identidad personal. |
| Promociones | Alta, edición, activación y selección de productos; guardado de edición por RPC. | Selector limitado a 500; falta validar aplicación y límites dentro de compras completas. |
| Reportes | Consulta por periodo, indicadores, gráficos nativos y CSV. | Validación con pedidos completados y conciliación; endurecer exportaciones. |
| Usuarios | Directorio, alta con contraseña temporal, roles y suspensión/reactivación. | Escrituras múltiples sin transacción común; cobertura de fallos parciales de auditoría. |
| Configuración | Datos, métodos aceptados, horarios y excepciones por RPC. | No permite administrar todos los parámetros operativos y destinatarios de pago presentes en el backend. |

## 5. Estado actual comprobado del backend y distribución

### 5.1 Supabase

Proyecto Cogana `mstcxtpjncozqztkawjg`: **ACTIVE_HEALTHY**, Postgres 17. Se encontraron **37 tablas en `public`, todas con RLS habilitado**. Habilitar RLS no basta para acreditar toda la autorización, por lo que también se inspeccionaron políticas y auxiliares.

| Grupo | Filas observadas |
|---|---:|
| Categorías / productos / presentaciones / imágenes de producto | 0 en cada tabla |
| Lotes / paquetes / movimientos de inventario | 0 en cada tabla |
| Pedidos / artículos / pagos | 0 en cada tabla |
| Clientes | 2 |
| Proveedores | 2 |
| Promociones | 1 |
| Membresías de tienda | 3 |
| Auditoría de accesos de personal | 3 |

Estos vacíos son coherentes con la limpieza de catálogo documentada el 23/10/2026. No prueban que falte conectar la aplicación. El estado operativo leído es `accepting_orders`; aun así, un catálogo vacío impide comprobar una venta real con productos disponibles.

Existen reglas de franjas para recojo y delivery. Los buckets actuales incluyen `product-images` público con límite de 5 MiB, `payment-proofs` privado, `delivery-evidence` privado y `actualizaciones` público.

La Edge Function `admin-users` está activa en **versión 4 con `verify_jwt=true`**. El código desplegado coincide con el local ignorando finales de línea. Esto da relevancia a sus pruebas locales, aunque no reemplaza pruebas contra Auth y Postgres reales.

### 5.2 Distribución

La API pública de GitHub confirmó **v1.12.0**, estable, sin borrador ni marca de prerelease, publicada el **24/10/2026 a las 13:50:07 de Lima**. Hay seis recursos: Setup, Portable, paquete completo, delta, `RELEASES` y `releases.win.json`.

En este entorno están configuradas la URL de Cogana, una clave **publicable** y el repositorio `Yhon-26/Cogana_Admin`. No se imprimió el contenido de la clave. El instalador local revisado devuelve **NotSigned** en Authenticode. La existencia de recursos publicados no acredita su instalación limpia ni la actualización completa en otro equipo.

## 6. Comprobaciones ejecutadas

| Comprobación | Resultado actual | Límite de la evidencia |
|---|---|---|
| `dotnet build Cogana.Admin.slnx -c Release --no-restore` | 0 errores, 0 advertencias. | Compilación con dependencias presentes en este equipo. |
| `dotnet run --project Pruebas/Acceso/Acceso.csproj -c Release` | 33 comprobaciones correctas. | Auth y red simulados; no envía correos. |
| `node Pruebas/Acceso/funcion-acceso.mjs` | 12 comprobaciones correctas. | Ejecuta código real de Edge con Auth/BD simulados. |
| `dotnet run --project Pruebas/Login/Login.csproj -c Release` | 149 verificaciones correctas, Tier gráfico 2. | Ventanas y servicios aislados; sin conexión a Supabase. |
| `dotnet run --project Pruebas/Login/Login.csproj -c Release --no-build -- --onboarding` | 41 comprobaciones correctas. | Incluye comprobaciones de geometría repetidas de la base del arnés. |
| `dotnet list Cogana.Admin.slnx package --vulnerable --include-transitive` | Sin paquetes vulnerables reportados por los orígenes actuales. | No acredita ausencia total de vulnerabilidades en la aplicación. |
| Contraste de colores del tema | Texto suave: 4,34:1 en blanco; 4,05:1 sobre superficie; 3,53:1 sobre fondo. | Cálculo con colores del código; no evaluación completa de accesibilidad. |

Los recuentos se presentan por suite porque comparten comprobaciones; no son 235 casos de negocio distintos.

Se revisaron las capturas recién generadas de panel mínimo, recuperación mínima y segundo acto del onboarding. Muestran identidad visual consistente y jerarquía legible a la escala probada. Las cuentas y datos visibles en esas imágenes son simulados.

El arnés adicional de diagnóstico está fuera del repositorio, en [Auditoria.csproj](C:/Users/Yhon/.codex/visualizations/2026/10/24/01a16f78-7a83-7ce0-bcba-e4c2a324d4e4/auditoria-cogana/Auditoria.csproj), con su [salida reproducible](C:/Users/Yhon/.codex/visualizations/2026/10/24/01a16f78-7a83-7ce0-bcba-e4c2a324d4e4/auditoria-cogana/hallazgos-reproducidos.txt). Los seis escenarios no realizan conexiones de red ni usan credenciales reales.

## 7. Hallazgos prioritarios

Prioridades: **P1** debe resolverse antes de ampliar uso operativo; **P2** corresponde a consolidación o un fallo condicionado; **P3** es mantenimiento. La prioridad no significa que el fallo se haya provocado en producción.

### H01 · P1 · El reintento conserva la autorización vencida

**Ubicación:** [ClienteSupabaseRest.cs:56](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Infraestructura/Servicios/ClienteSupabaseRest.cs:56), clonación desde línea 132.

`EnviarAsync` recibe 401, renueva los valores predeterminados del cliente y clona la solicitud original. La copia incluye el encabezado Authorization que ya tenía el token viejo. El arnés A1 observó: autorización inicial `viejo`, reintento `viejo`, respuesta final 401, aun cuando la renovación había devuelto `nuevo`.

**Impacto:** guardados, RPC y reportes que usan `EnviarAsync` pueden seguir fallando después de renovar. La ruta `ObtenerAsync` crea otra consulta y no usa esa misma clonación, por lo que la experiencia puede variar entre lectura y escritura. **Recomendación:** preparar la autorización por solicitud y por intento, sin copiar el bearer anterior; conservar la protección ya existente frente al cierre concurrente. Verificar GET, POST, PATCH y subida con expiración.

### H02 · P1 · Los reintentos pueden alterar contenido binario

**Ubicación:** [ClienteSupabaseRest.cs:137](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Infraestructura/Servicios/ClienteSupabaseRest.cs:137).

La clonación lee cualquier cuerpo como texto y lo reconstruye como UTF-8, incluso un `ByteArrayContent` de imagen. El escenario A2 pasó de **8 a 14 bytes**, con contenido diferente.

**Impacto:** una foto reenviada después de un 401 deja de ser el mismo archivo. H01 puede impedir actualmente que el reintento sea aceptado; corregir únicamente la autorización expondría este segundo defecto. **Recomendación:** conservar bytes y encabezados de contenido, y probar integridad completa del archivo antes/después del reintento.

### H03 · P1 · Pedidos no cubre el contrato operativo vigente

**Ubicación:** [VentanaCambioEstadoPedido.xaml.cs:34](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/Vistas/VentanaCambioEstadoPedido.xaml.cs:34), [contrato de pedidos](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Aplicacion/Contratos/IServicioPedidosAdministrativos.cs).

La interfaz ofrece `completed` como transición desde `ready_for_pickup` y `out_for_delivery`. La definición desplegada de `private.transition_order` rechaza expresamente completar por esa ruta: exige código de recojo o entrega.

Además, el servidor exige pago válido para avanzar, preparación finalizada para dejar listo y un repartidor asignado para reparto. `assert_order_prepared` comprueba cantidades/importes finales y diferencias pendientes. El escritorio consulta estos datos, pero no integra las operaciones para resolver esas condiciones.

| Operación desplegada | Integración encontrada en escritorio |
|---|---|
| `confirm_manual_payment` / `reject_manual_payment` | No encontrada. |
| `finalize_weighted_order` | No encontrada. |
| `assign_delivery` | No encontrada. |
| `complete_pickup` / `complete_delivery` | No encontrada. |
| `open_inventory_package` | No encontrada. |

**Impacto:** el panel permite consultar y avanzar parcialmente, pero no puede completar por sí solo el ciclo comercial actual. **Recomendación:** diseñar los pasos del operador usando estos contratos reales, incluidos ajustes y sustituciones. La comprobación de aceptación debe terminar en entrega/recojo válido, pagos conciliados, stock y reportes coherentes. No retirar validaciones del servidor para que coincida con el menú actual.

### H04 · P1 · Reintentar la fotografía puede repetir el alta del producto

**Ubicación:** [VentanaNuevoRegistro.xaml.cs:134](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/Vistas/VentanaNuevoRegistro.xaml.cs:134), método `GuardarAsync` desde línea 214.

Se crea primero el producto. Si falla la imagen, la ventana permanece abierta y se habilita Guardar. El siguiente clic vuelve a ejecutar `CrearProductoAsync`; el identificador anterior no se utiliza para continuar exclusivamente la subida.

**Impacto deducido del flujo:** sin un SKU que imponga unicidad pueden aparecer duplicados; con una restricción de unicidad el reintento puede quedar bloqueado en lugar de recuperar la foto. No se crearon duplicados reales durante esta revisión. **Recomendación:** mantener el producto ya creado y ofrecer reintento solo de la imagen, con un resultado parcial explícito.

### H05 · P1 · Reemplazar una imagen no garantiza consistencia

**Ubicación:** [ServicioImagenesProducto.cs:56](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Infraestructura/Servicios/ServicioImagenesProducto.cs:56).

El servicio sube el objeto, borra las filas anteriores y crea una nueva fila en solicitudes separadas. El resultado del DELETE no se comprueba ni se dispone. El escenario A4 devolvió éxito aunque el DELETE respondió HTTP 500 y luego se aceptó el INSERT. Si el borrado funciona y el INSERT falla, se pierde la asociación anterior. Cambiar extensión tampoco elimina el objeto anterior.

**Impacto:** referencias inconsistentes, orden de imágenes ambiguo o archivos huérfanos; el móvil comparte esas referencias. **Recomendación:** garantizar una actualización consistente de metadatos, validar cada respuesta y definir compensación/limpieza para Storage. La eliminación de una foto guardada debe diferenciarse de cancelar una selección local; el botón actual de quitar imagen trabaja sobre esa selección.

### H06 · P1 · Hay rutas de errores que pueden escapar a la interfaz

**Ubicación:** [VentanaPrincipalViewModel.cs:536](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/ViewModels/VentanaPrincipalViewModel.cs:536), [ComandoAsincrono.cs:14](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/ViewModels/ComandoAsincrono.cs:14), [MainWindow.xaml.cs:678](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/MainWindow.xaml.cs:678).

Búsqueda y descarga de actualizaciones no tienen captura de errores en el servicio o el ViewModel. El comando `async void` tiene `finally`, pero no `catch`. La exportación sencilla escribe el CSV desde el evento sin manejar IO/permisos, mientras que la ventana detallada de reportes sí lo hace. No se encontró un manejador global de excepciones en el código de producto inspeccionado.

**Impacto deducido del código:** un fallo de red del actualizador o de escritura de esa exportación puede propagarse al Dispatcher y terminar la aplicación. No se provocó una caída de la instalación real. **Recomendación:** tratar errores esperables en la operación concreta, restaurar botones y permitir reintento; añadir diagnóstico técnico seguro para fallos imprevistos.

### H07 · P2 · El onboarding puede repetirse sin permitir entrar si no puede guardar

**Ubicación:** [App.xaml.cs:84](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/App.xaml.cs:84), [EstadoPrimerIngreso.cs:54](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/Servicios/EstadoPrimerIngreso.cs:54).

Al finalizar se intenta escribir la marca y se llama otra vez a `AbrirPanelPrincipal`, que crea y carga una nueva instancia de estado. A6 confirmó que un destino sin posibilidad de escritura deja `Visto=false` también en la nueva instancia. El flujo vuelve entonces a abrir la introducción.

**Impacto:** ante problemas de permisos o disco, completar/saltar puede abrir otra introducción en la misma sesión. **Recomendación:** permitir continuar al panel después de finalizar en memoria; guardar la preferencia no debería ser requisito para acceder. Añadir al arnés el fallo de persistencia, además del camino correcto ya probado.

### H08 · P2 · Los listados y búsquedas se limitan silenciosamente

**Ubicación:** [ServicioModulosAdministrativosSupabase.cs:63](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Infraestructura/Servicios/ServicioModulosAdministrativosSupabase.cs:63), [VentanaPrincipalViewModel.cs:675](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/ViewModels/VentanaPrincipalViewModel.cs:675).

Ocho listados usan `limit=250`, el selector de promociones usa 500, y la búsqueda filtra únicamente las filas descargadas. `TotalModulo` y los totales de tarjetas toman el tamaño de esa lista. Otros selectores tampoco recorren páginas y quedan sujetos al límite del servidor.

**Impacto:** al crecer el negocio, un registro existente puede parecer inexistente y los totales de las tarjetas parecer completos sin serlo. **Recomendación:** búsqueda y paginación de servidor, total explícito y orden estable; distinguir filas cargadas de total real. Probar más de 250 registros.

### H09 · P2 · Configuración no administra todos los parámetros del comercio

**Ubicación:** [EdicionConfiguracionTienda.cs](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Aplicacion/Modelos/EdicionConfiguracionTienda.cs), [ServicioConfiguracionTiendaSupabase.cs:95](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Infraestructura/Servicios/ServicioConfiguracionTiendaSupabase.cs:95).

El backend contiene `manual_payment_window_minutes`, destinatarios/teléfonos de Yape y Plin, reglas de franjas/cupo/repartidores y estado operativo. La edición del escritorio no incluye esos parámetros ni integra `set_fulfillment_rules`; el estado operativo se muestra en consultas.

**Impacto:** aceptar un método de pago en una casilla no permite administrar toda su configuración desde el panel. **Recomendación:** definir qué parámetros gestiona el propietario y conectar las operaciones existentes, con validaciones de consistencia entre horario, modalidad, capacidad y pagos.

### H10 · P2 · La renovación concurrente y el cierre necesitan consolidación

**Ubicación:** [ClienteSupabaseRest.cs:88](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Infraestructura/Servicios/ClienteSupabaseRest.cs:88), [App.xaml.cs:163](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/App.xaml.cs:163).

El semáforo serializa las renovaciones, pero cada solicitud que recibió 401 renueva de nuevo aun cuando otra ya cambió el token. A3 observó **cuatro renovaciones para cuatro GET simultáneos**. El resumen ya ejecuta varias consultas en paralelo. Además, los clientes de sesiones anteriores se retienen hasta salir de la aplicación, y los detalles/formularios no tienen una política uniforme de cancelación al cerrar.

**Impacto:** trabajo adicional, más exposición a errores de renovación y acumulación de recursos tras repetidos cierres/ingresos. No se midió una fuga de memoria en producción. **Recomendación:** compartir una renovación por versión de token, cancelar operaciones al cerrar y disponer clientes cuando terminen las solicitudes pendientes. Preservar la prueba existente que impide restaurar una sesión cerrada.

### H11 · P2 · Un HTTP correcto se interpreta como actualización efectiva

**Ubicación:** [ServicioCatalogoAdministrativoSupabase.cs:235](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Infraestructura/Servicios/ServicioCatalogoAdministrativoSupabase.cs:235), métodos genéricos desde línea 561.

Los PATCH usan `return=minimal` y confirman éxito solo con `IsSuccessStatusCode`. A5 muestra que el servicio acepta HTTP 204 sin evidencia de filas modificadas. No detecta por sí mismo que un registro haya desaparecido, dejado de ser visible o no coincida con el filtro. Tampoco hay control uniforme de cambios concurrentes mediante una versión esperada.

**Impacto:** mensajes de guardado exitoso sin confirmar el cambio y posible sobrescritura de una edición más reciente. **Recomendación:** comprobar la fila devuelta/afectada o usar un contrato de guardado que distinga inexistencia y conflicto. A5 prueba la interpretación del cliente; no se indujo un falso guardado real en producción.

### H12 · P2 · Usuarios puede quedar modificado aunque falle su auditoría

**Ubicación:** [admin-users/index.ts:190](D:/Cogana_Admin/Cogana.Admin/BaseDatos/Funciones/admin-users/index.ts:190), auditoría desde líneas 208 y 274.

Crear/actualizar cuenta, perfil, membresía y auditoría son pasos separados. Un error tardío retorna fallo después de que uno o más pasos ya se confirmaron. No se encontró una compensación que revierta lo anterior. El listado también recorre todas las cuentas de Auth para cruzarlas con el personal de una tienda.

**Impacto deducido:** cambios de acceso sin su registro esperado o reintentos que responden que la cuenta ya existe. El recorrido completo de Auth puede escalar con clientes móviles que no pertenecen al personal. **Recomendación:** transacción para cambios relacionados de BD/auditoría, idempotencia y recuperación explícita de la frontera con Auth; consultar únicamente las identidades necesarias.

### H13 · P2 · La obligación de cambiar contraseña no cubre toda la API administrativa

**Ubicación:** [ServicioAutenticacionSupabase.cs](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Infraestructura/Servicios/ServicioAutenticacionSupabase.cs), [admin-users/index.ts:81](D:/Cogana_Admin/Cogana.Admin/BaseDatos/Funciones/admin-users/index.ts:81), definición desplegada de `private.has_store_role`.

El cliente actual bloquea el panel y la Edge Function bloquea sus acciones normales cuando existe la marca de cambio pendiente. Sin embargo, el auxiliar compartido de permisos comprueba identidad, no anonimato, membresía activa y rol; no consulta esa marca. Las políticas de catálogo usan dicho auxiliar.

**Impacto:** el bloqueo observado es efectivo en el escritorio actual y en esa función, pero no constituye por sí mismo una prohibición global sobre cualquier cliente/API. Esta limitación ya estaba reconocida en la hoja de ruta. **Recomendación:** decidir si se requiere esa garantía global y, en tal caso, diseñarla en el backend compartido considerando clientes móviles y versiones anteriores. No se intentó eludir el cliente con una cuenta real.

### H14 · P2 · Accesibilidad y escalado tienen brechas concretas

**Ubicación:** [MainWindow.xaml:11](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/MainWindow.xaml:11), [App.xaml:63](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/App.xaml:63), [VentanaOnboarding.xaml.cs:274](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.Escritorio/Vistas/VentanaOnboarding.xaml.cs:274).

El panel exige al menos **1180 × 720 unidades lógicas**. En una pantalla 1366 × 768 al 125 %, el espacio lógico aproximado es 1093 × 614 antes de descontar la barra de tareas: existe riesgo de acceso incompleto. El manifiesto PerMonitorV2 mejora nitidez, pero no corrige dimensiones mínimas.

`BrochaTextoSuave` queda por debajo de 4,5:1 incluso en blanco y se usa en texto pequeño. Se suprime el foco nativo en numerosos estilos y no se verificó un sustituto uniforme. Hay nombres accesibles y avisos de carga, lo que conviene conservar. No se encontró adaptación explícita a alto contraste. El onboarding inicia animaciones sin comprobar `SystemParameters.ClientAreaAnimation`, a diferencia del menú/skeleton.

**Recomendación:** probar 100/125/150/200 %, teclado, Narrador y alto contraste; corregir dimensiones, contraste y foco; respetar la preferencia de movimiento del sistema. Se utiliza [WCAG 1.4.3](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) como referencia medible de contraste, sin afirmar certificación WCAG del programa de Windows.

### H15 · P2 · Los avisos de seguridad requieren interpretación y seguimiento

El advisor devolvió **39 advertencias de políticas para usuarios anónimos**, **1 de protección de contraseñas filtradas desactivada** y **2 avisos informativos de RLS sin políticas en tablas privadas**.

No se interpreta la cifra 39 como 39 accesos abiertos: en las políticas administrativas muestreadas se invoca `has_store_role`, cuya definición rechaza explícitamente usuarios anónimos y exige membresía activa. Las tablas privadas sin políticas pueden estar cerradas deliberadamente y ser accedidas solo desde funciones controladas. Las políticas de cron también necesitan revisión de privilegios/esquemas expuestos antes de concluir exposición.

**Recomendación:** validar por rol y endpoint lo efectivamente accesible, y registrar qué avisos son esperados. Revisar la [protección de contraseñas filtradas](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection) según las capacidades del plan. Esta revisión no cambió Auth ni políticas. No equivale a un pentest completo.

### H16 · P2 · Las pruebas pasan, pero faltan regresiones de contratos e integración continua

**Ubicación:** [solución](D:/Cogana_Admin/Cogana.Admin/Cogana.Admin.slnx), [Pruebas](D:/Cogana_Admin/Cogana.Admin/Pruebas).

Los arneses ofrecen cobertura valiosa de acceso, interacción, estados de carga, cierre y controles. Sin embargo, son ejecutables propios, no están incluidos en la solución y no se encontró una canalización de CI en el repositorio. Tampoco se encontraron `global.json`, bloqueo de paquetes o política uniforme de análisis estático.

El fallo H03 ilustra una carencia concreta: las pruebas de interfaz pasan aunque las transiciones ofrecidas difieran del backend vigente. **Recomendación:** ejecutar explícitamente ambos arneses y Node en una CI Windows, incluir escenarios de expiración y errores parciales, y comprobar contratos contra un entorno aislado reproducible. No basta con invocar `dotnet test` esperando que descubra estos ejecutables.

### H17 · P2 · Reproducibilidad, respaldo y documentación deben consolidarse

Se encontraron nueve scripts SQL locales de ampliaciones del escritorio y **35 migraciones registradas en el backend compartido**. Las diferencias incluyen la fundación y evolución móvil; esta solución por sí sola no contiene un procedimiento completo, comprobado en este análisis, para reconstruir todo ese backend.

El respaldo de catálogo de 78.149 bytes existe en `D:\Cogana_Admin\BaseDatos\Respaldos\respaldo_catalogo_2026-10-23.json`, fuera de la carpeta de la solución. La carpeta `BaseDatos` de la raíz ya aparecía como no seguida por Git al iniciar la revisión. No se restauró ni se acreditó una copia independiente. No debe asumirse restauración probada solo porque existe el JSON, ni incorporarse el respaldo inadvertidamente a una publicación de código.

La hoja de ruta combina la versión actual con pendientes históricos; aún indica fecha/publicación vigente 23/10 aunque la release consultada es del 24/10. El README conserva el recuento anterior de ventanas. **Recomendación:** separar estado vigente de historial, documentar propiedad/versiones del backend compartido y ensayar restauración en un entorno aislado. Completar instalación limpia y actualización desde una versión anterior. El Setup local no está firmado; decidir firma según la distribución prevista.

### H18 · P3 · Deuda de mantenimiento localizada

La presentación mezcla lógica de formularios, decisiones de negocio, consultas y coordinación; hay identificadores por texto, traducciones de estados duplicadas y excepciones genéricas que borran contexto técnico. También existe una copia de `IServicioClientesAdministrativos` en una carpeta de aplicación al nivel de la raíz que no participa en la solución.

**Impacto:** aumenta el riesgo de corregir una copia equivocada y de que una nueva regla se aplique de forma desigual. **Recomendación:** refactorizar al tocar cada caso de uso: identificadores estables, políticas de error y servicios de coordinación. Añadir registros técnicos sin tokens, contraseñas ni datos personales innecesarios. Evitar una migración general de arquitectura sin una necesidad comprobada.

## 8. Rendimiento: oportunidades comprobables y límites

El resumen realiza ocho consultas en paralelo y luego refresca tarjetas. Las tarjetas comunes llaman al mismo servicio de listados de hasta 250 filas para mostrar cinco. Hay oportunidad concreta de limitar consultas de resumen y compartir una lectura cuando coincidan datos. La búsqueda local vuelve a poblar la colección por pulsación; su comportamiento con una paginación real requerirá revisión.

El advisor de rendimiento devolvió **40 claves foráneas sin índice de cobertura y 9 índices sin uso observado**, todos de nivel informativo. No conviene crear 40 índices o eliminar los 9 automáticamente: la base está prácticamente vacía de operaciones y el uso observado puede no representar el negocio futuro. Priorizar consultas de pedidos, artículos, lotes y reportes con planes y datos representativos.

Hay decisiones positivas: consultas independientes concurrentes, control de duplicación de comandos, cancelación/versionado de carga de módulos y descarga limitada en el visor. El visor de imágenes acota la descarga, define timeout y reduce decodificación a 1600 px. Las animaciones principales liberan relojes al descargar/minimizar en los caminos probados.

No se midieron latencias P95, memoria, CPU/GPU, consumo durante horas, carga concurrente o rendimiento con miles de registros. No se atribuye un cuello de botella de producción a una medición que no se hizo. El uso de fecha/hora local de Windows en resumen y promociones también debe alinearse con la zona horaria de la tienda para instalaciones en otra zona.

## 9. Aspectos que deben preservarse

- Separación del escritorio y el móvil en interfaz y código; contratos compartidos del backend tratados de forma coordinada.
- Clave privilegiada exclusivamente en el servidor; cliente configurado con clave publicable.
- Membresías activas y roles comprobados; `has_store_role` e `is_store_member` rechazan anonimato.
- Recuperación con validación del enlace del proyecto y sin reutilizar la sesión de recuperación para abrir el panel.
- Contraseña recordada mediante credenciales de Windows y alcance por proyecto, con elección explícita del usuario.
- Cierre que elimina tokens locales aun con fallo de red; protección frente a renovación pendiente después del cierre.
- RPC para ajustes, lotes, configuración y promoción; conservar sus validaciones e identificadores de operación.
- Estados de carga, vacío, fallo y reintento, sin confundir datos simulados con métricas reales.
- Gráficos WPF que reutilizan contratos de reportes sin añadir dependencias innecesarias.
- Identidad visual y marco de ventanas comunes ya consolidados.

## 10. Orden recomendado de trabajo

| Orden | Bloque | Criterio concreto para cerrarlo |
|---|---|---|
| 1 | Cliente REST y sesión: H01, H02, parte de H10. | Una renovación compartida; reintento con token vigente; JSON y binarios íntegros; cierre no resucita sesión. |
| 2 | Guardados y errores: H04, H05, H06, H07, H11. | Reintentar foto no repite producto; fallos parciales se recuperan; error de red/disco no cierra ni bloquea la app. |
| 3 | Operación de pedidos: H03 y H09. | Atender recojo y delivery completos con pago, preparación, ajustes, repartidor y códigos usando RPC existentes. |
| 4 | Integración, accesos y recuperación: H12, H13, H15, H16, H17. | Pruebas de permisos y fallos parciales, CI explícita, backend reproducible y restauración ensayada. |
| 5 | Escala y uso diario: H08, H10, H14 y H18. | Más de 250 filas, búsqueda completa, escalas de Windows, teclado/Narrador, medición con actividad representativa. |
| 6 | Refinamiento visual. | Ajustes puntuales que preserven identidad y mejoren tareas reales después de estabilizar los pasos anteriores. |

En el trabajo de interfaz, las guías de Impeccable pueden apoyar `harden`, `adapt`, `optimize` y, al final, `polish`; los cambios de negocio deben regirse por los contratos comprobados del proyecto. Este informe no ejecuta esas mejoras.

## 11. Lista de validación pendiente

- [x] Inspeccionar solución, arquitectura, servicios, contratos, ventanas y documentación vigente.
- [x] Compilar Release y ejecutar suites locales de acceso, Edge, escritorio y onboarding.
- [x] Reproducir escenarios de sesión/imágenes/estado con código real y red simulada.
- [x] Contrastar metadatos, RLS, auxiliares de autorización y operaciones desplegadas en Supabase.
- [x] Confirmar publicación pública v1.12.0 y revisar firma del Setup local.
- [x] Revisar capturas actuales de ventanas aisladas y calcular contrastes concretos.
- [ ] Validar entrada/recuperación completa con correo real y controles externos de Auth, sin compartir secretos.
- [ ] Mantener una sesión real abierta más de una hora y operar simultáneamente consultas y guardados.
- [ ] Recorrer una compra completa actual de recojo y otra de delivery, con conciliación de pagos, reservas, stock e informes.
- [ ] Probar fallos parciales, reintentos e idempotencia de guardados en un backend aislado.
- [ ] Comprobar permisos por rol y rechazo de accesos cruzados entre tiendas con casos representativos.
- [ ] Probar instalación en Windows limpio, actualización desde la instalada, recuperación y eventual reversión.
- [ ] Verificar restauración del respaldo en un destino separado y confirmar estrategia de copias independientes.
- [ ] Medir uso prolongado y rendimiento con volumen representativo, incluyendo paquetes sellados y promociones.
- [ ] Validar 100/125/150/200 %, varios monitores, teclado, Narrador, alto contraste y movimiento reducido.

## 12. Referencias y evidencias

Las ubicaciones enlazadas arriba corresponden al código revisado; sus números de línea pueden cambiar después de implementar mejoras. Las consultas de metadatos y advisors se realizaron durante esta revisión el 24/10/2026; sus cifras son una instantánea.

- [Publicación v1.12.0](https://github.com/Yhon-26/Cogana_Admin/releases/tag/v1.12.0).
- [Microsoft: DefaultRequestHeaders](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.defaultrequestheaders?view=net-10.0), referencia para encabezados compartidos; H01 se acreditó además mediante reproducción local.
- [Microsoft: ReadAsStringAsync](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcontent.readasstringasync?view=net-10.0), referencia del tratamiento como texto; H02 se acreditó comparando bytes.
- [Supabase: usuarios anónimos y roles](https://supabase.com/docs/guides/auth/users), apoyo para interpretar los avisos; no sustituye las políticas efectivamente inspeccionadas.
- [Supabase: seguridad de contraseñas](https://supabase.com/docs/guides/auth/password-security).
- [Supabase: aviso de RLS sin políticas](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy).
- [Supabase: avisos de políticas para anonimato](https://supabase.com/docs/guides/database/database-advisors?queryGroups=lint&lint=0012_auth_allow_anonymous_sign_ins).
- [W3C: contraste mínimo](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html).

**Entrega de este trabajo:** diagnóstico e informe. El código funcional, los datos compartidos, el respaldo preexistente y las publicaciones no se modificaron. No se dejó una migración, release ni commit preparado como parte de una mejora.
