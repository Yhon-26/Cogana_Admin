# Cogana Admin: estado y hoja de ruta

**Ubicación del proyecto:** `D:\Cogana_Admin\Cogana.Admin`  
**Actualizado:** 27 de septiembre de 2026

Esta guía resume el estado conocido del software de escritorio. El código fuente es la referencia para el comportamiento actual; las funciones indicadas como pendientes todavía no deben considerarse disponibles.

## Qué es este software

Cogana Admin es una aplicación administrativa para Windows, construida con C# y WPF. Su objetivo es gestionar la tienda desde una interfaz de escritorio y consultar/actualizar los datos del mismo proyecto Supabase que utiliza la aplicación móvil.

El escritorio y la aplicación móvil son proyectos separados: tienen interfaz, estilos, código y ciclo de publicación propios. Comparten la base de datos y sus reglas de acceso de Supabase. La ubicación móvil conocida es `C:\Cogana_app`; ese proyecto no forma parte de esta solución.

## Estructura del proyecto

La solución que se abre en Visual Studio es `Cogana.Admin.slnx`, en la carpeta raíz.

| Carpeta | Responsabilidad |
| --- | --- |
| `Cogana.Admin.Dominio` | Entidades centrales y reglas del negocio. |
| `Cogana.Admin.Aplicacion` | Modelos, contratos y operaciones que consume la interfaz. |
| `Cogana.Admin.Infraestructura` | Conexión REST con Supabase y servicios que consultan o guardan datos. |
| `Cogana.Admin.Escritorio` | Ventanas WPF, navegación y presentación para Windows. |
| `BaseDatos\Migraciones` | Scripts SQL asociados a cambios aplicados para las funciones administrativas. |

La configuración técnica y de inicio de sesión también está en el [README.md](README.md).

## Ya está construido

- Inicio de sesión con Supabase Auth y comprobación de membresía activa `owner` o `admin` para la tienda.
- Pantalla principal con conexión, indicadores del negocio y pedidos recientes.
- Consulta de productos, categorías, presentaciones, inventario/lotes, proveedores, pedidos, clientes y promociones.
- Alta y edición de productos, categorías, presentaciones, proveedores, promociones y lotes de inventario.
- Activación y desactivación para los registros compatibles.
- Edición de promoción y selección de productos vinculados.
- Detalle de producto, cliente, lote y pedido.
- Ajustes de stock con la operación transaccional existente en Supabase e historial de movimientos.
- Detalle de pedido con productos, importes, entrega, pagos e historial de estados. El cambio de estado usa la operación validada por el backend.
- Consulta del directorio de miembros de la tienda.
- Consulta de datos de la tienda y horarios de atención.
- Exportación CSV del módulo de reportes disponible actualmente.
- Interfaz propia del escritorio, separada de los estilos de la aplicación móvil.
- Empaquetado con Velopack: instalador `CoganaAdmin-win-Setup.exe` generado por `Publicar-Instalador.ps1`.
- Actualizaciones desde la app: botón "Buscar actualizaciones" que detecta, descarga (delta), instala y reinicia, usando un bucket público de Supabase Storage como repositorio.

La solución compiló correctamente al cerrar el bloque de actualizaciones: cero errores y cero advertencias. Esa comprobación confirma compilación, no una revisión funcional de cada pantalla ni una publicación instalable probada en un equipo limpio.

## Qué aparece en el menú y qué falta

| Módulo | Estado actual | Trabajo pendiente principal |
| --- | --- | --- |
| Inicio | Indicadores y actividad reciente conectados a Supabase. | Revisar métricas con datos reales y mejorar filtros/periodos si el negocio los requiere. |
| Productos | Consulta, alta, edición, estado y detalle. | Completar reglas y revisión de casos límite del catálogo. |
| Categorías | Consulta, alta, edición y activación. | Revisar ordenamiento y dependencias con el catálogo. |
| Presentaciones | Consulta, alta, edición y activación. | Validar equivalencias, precios y restricciones con operaciones reales. |
| Inventario y lotes | Consulta, alta, detalle, movimientos y ajuste controlado de stock. | Revisión operativa con casos reales, vencimientos y permisos. |
| Proveedores | Consulta, alta, edición y activación. | Revisar validaciones y datos fiscales/contacto. |
| Pedidos | Lista, detalle completo e intervención mediante cambios de estado permitidos. | Verificación funcional con pedidos reales, especialmente entrega y pagos. |
| Clientes | Consulta y detalle de perfil, direcciones, preferencias e historial. | Las modificaciones de información personal no están habilitadas. |
| Promociones | Consulta, alta, edición, estado y asignación de productos. | Validar cálculo y vigencia con casos comerciales reales. |
| Reportes | Consulta del reporte disponible y exportación CSV. | Definir reportes finales, filtros, periodos y formato solicitado por la tienda. |
| Usuarios | Consulta de membresías y roles visibles de la tienda. | Falta administrar invitaciones, roles y suspensión/reactivación desde una pantalla segura. |
| Configuración | Consulta de datos de tienda y horarios. | Falta edición y guardado de datos, horarios y canales desde el escritorio. |

## Siguientes bloques recomendados

1. **Usuarios y accesos:** resolver una forma segura de relacionar cada `user_id` con su identidad visible; implementar invitaciones y cambios de rol/estado mediante funciones protegidas en Supabase. La aplicación de escritorio no debe llevar una clave secreta o `service_role`.
2. **Configuración de tienda:** formularios de edición para datos comerciales, canales y horarios; guardar con validaciones y permisos.
3. **Reportes:** confirmar con la tienda las métricas y periodos necesarios, luego agregar filtros y exportaciones correspondientes.
4. **Revisión funcional:** recorrer cada flujo en Windows con la cuenta administrativa y datos de prueba controlados; confirmar errores de red, permisos, formularios, importes y estados.
5. **Instalador y publicación:** el empaquetado y las actualizaciones están implementados con Velopack (`Publicar-Instalador.ps1` + bucket `actualizaciones`). Pendiente: probar el instalador en un equipo limpio de Windows y decidir si se requiere firma de código.

## Conexión y seguridad

La aplicación lee `COGANA_SUPABASE_URL` y `COGANA_SUPABASE_PUBLIC_KEY` desde variables de entorno. El proyecto de Supabase configurado para Cogana es `mstcxtpjncozqztkawjg`. En la app de escritorio solo debe usarse la clave pública/publicable; nunca una clave `service_role` o secreta.

El acceso usa Supabase Auth y membresías activas de la tienda. La aplicación depende de RLS y de las operaciones protegidas ya existentes en el backend. Las acciones que afecten pedidos o inventario deben continuar pasando por esas operaciones controladas.

## Cómo abrir el proyecto

1. Abre Visual Studio.
2. Selecciona **Abrir un proyecto o una solución**.
3. Abre `D:\Cogana_Admin\Cogana.Admin\Cogana.Admin.slnx`.
4. Selecciona `Cogana.Admin.Escritorio` como proyecto de inicio y ejecuta la aplicación.
5. Para conectarte, configura `COGANA_SUPABASE_URL` y `COGANA_SUPABASE_PUBLIC_KEY` en el entorno desde el que se inicia Visual Studio. El README contiene la URL de proyecto y los requisitos de acceso.

## Dónde consultar cada detalle

- Diseño general, configuración local y acceso: [README.md](README.md).
- Pantallas y navegación: `Cogana.Admin.Escritorio\Vistas`, `MainWindow.xaml` y `ViewModels`.
- Consultas/guardados de módulos: `Cogana.Admin.Infraestructura\Servicios`.
- Contratos y modelos que usa cada función: `Cogana.Admin.Aplicacion\Contratos` y `Modelos`.
- Entidades principales: `Cogana.Admin.Dominio\Entidades`.
- Scripts SQL del escritorio: `BaseDatos\Migraciones`.

Esta hoja de ruta describe el estado a la fecha indicada. Debe actualizarse cuando se complete cada bloque o cambie el alcance acordado.
