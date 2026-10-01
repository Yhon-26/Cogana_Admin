# Cogana Admin: estado y hoja de ruta

**Ubicación del proyecto:** `D:\Cogana_Admin\Cogana.Admin`  
**Actualizado:** 30 de septiembre de 2026

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
- Consulta y edición de datos de la tienda, canales de atención, métodos de pago, tiempo de preparación, horario semanal y fechas especiales. El guardado se realiza como una sola operación validada en Supabase.
- Exportación CSV del módulo de reportes con resumen, ventas diarias, estados de pedido, productos vendidos, métodos de pago, stock bajo y vencimientos; los errores de escritura se muestran sin cerrar la ventana.
- Interfaz propia del escritorio, separada de los estilos de la aplicación móvil.
- Panel de inicio configurable: las secciones disponibles permanecen en una bandeja dentro de Inicio y pueden agregarse con un clic o arrastrándolas al área de tarjetas, sin abrir otro módulo ni cubrir la pantalla. Las tarjetas se quitan con un clic, se reordenan desde su cabecera y la disposición se guarda por equipo (archivo local JSON).
- Login inmersivo: fondo aurora animado con rejilla y foco de luz que sigue el mouse, tarjeta glass con anillo de luz giratorio, campos con etiqueta flotante e íconos, estado EN LÍNEA, animación de entrada e inclinación de la tarjeta siguiendo el mouse.
- Pantalla de carga del acceso: red de 150 partículas conectadas, anillos expansivos, orbe con halo giratorio, barra de progreso con porcentaje y pasos ("Verificando credenciales...", "Bienvenido al sistema").
- Nitidez en pantallas escaladas: manifiesto PerMonitorV2, UseLayoutRounding y modo de texto Display.
- Control de versiones con git; publicación automática a GitHub Releases (`Publicar-Instalador.ps1 -Github`) como canal de actualizaciones de la app (variable COGANA_GITHUB_REPO), con el bucket de Supabase como respaldo.
- Administración segura de personal: directorio con nombre, correo, rol, estado y último acceso; alta con correo y contraseña inicial; edición de perfil, rol y acceso; protección del último propietario y auditoría de cambios mediante la Edge Function `admin-users`.
- Reportes administrativos por periodo: resumen de ventas y pedidos, ventas diarias, estados, productos más vendidos, métodos de pago, stock bajo y lotes vencidos o próximos a vencer; exportación consolidada a CSV.
- Visualización de reportes: pestaña de resumen con gráfico de líneas para ventas diarias, donas de estados de pedido y métodos de pago, barras de productos con mayores ventas y comparación del stock actual frente al mínimo configurado. El widget Reportes del Inicio reutiliza el gráfico de tendencia para mostrar los últimos 30 días sin abrir otra ventana; cuando no existe actividad presenta estados vacíos sin datos simulados.
- **Renovación automática de sesión:** `ClienteSupabaseRest` detecta respuestas 401, renueva el token con el token de renovación y reintenta la solicitud. Antes, tras una hora de uso la app mostraba errores genéricos en todos los módulos hasta reiniciarla.

La solución compiló correctamente al cerrar los bloques de usuarios, accesos, configuración y reportes: cero errores y cero advertencias. La compilación Release y el arranque desde la salida publicada también fueron comprobados. El inicio de sesión real con la cuenta propietaria y la carga del panel principal fueron confirmados. El recorrido autenticado de todas las pantallas y la instalación en un equipo limpio siguen pendientes.

## Revisión funcional autenticada (30/09/2026)

Recorrido completo con sesión real de propietario contra la base de datos de producción, automatizado sobre la interfaz y verificado paso a paso también por REST con el token de la sesión:

| Flujo | Resultado |
| --- | --- |
| Inicio (resumen, contadores, widgets con datos reales) | Correcto |
| Productos: consulta, alta, edición, detalle, desactivar/activar | Correcto (producto QA-REV-001) |
| Categorías: consulta, alta, desactivación | Correcto (slug autogenerado) |
| Presentaciones: consulta | Correcto (30 presentaciones con precios reales) |
| Inventario y lotes: alta de lote con producto y proveedor | Correcto (QA-LOTE-001, 100 unidades) |
| Proveedores: consulta, alta, desactivación | Correcto |
| Pedidos y Clientes | Vacíos reales (tienda sin actividad aún); estado vacío correcto |
| Promociones: alta con fechas vía DatePicker, desactivación | Correcto |
| Reportes: consulta 30 días, pestañas, exportación CSV | Correcto |
| Usuarios: invitación (Edge Function protegida), gestión, suspensión | Correcto (verificado en `store_memberships`) |
| Configuración: carga, guardado, persistencia y reversión | Correcto (dos ciclos verificados en BD) |

**Hallazgo corregido durante la revisión:** el token de sesión expiraba a la hora y todos los módulos pasaban a fallar con mensajes genéricos ("No fue posible cargar...") hasta reiniciar la app, porque el cliente REST no renovaba la sesión. Corregido con renovación automática transparente al recibir 401. Pendiente de confirmar en la práctica: dejar la app abierta más de una hora y verificar que los módulos siguen cargando.

**Hallazgos menores:** la cuenta propietaria aparece como "Sin nombre registrado" (no hay nombre completo en el perfil de Supabase Auth; se puede completar desde el dashboard); el lote QA-LOTE-001 quedó activo con 100 unidades del producto QA desactivado (borrar desde el dashboard si se desea, o usar el ajuste de stock); la cuenta de prueba `qa.prueba@cogana.test` quedó suspendida (su borrado definitivo requiere el dashboard de Supabase).

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
| Reportes | Periodos de 7 y 30 días, mes actual o rango personalizado; tablero visual de ventas, estados, productos, pagos y stock; tablas detalladas y exportación CSV. | Validar resultados y criterios contables con pedidos reales de la tienda. |
| Usuarios | Directorio del personal, alta con contraseña inicial, edición de nombre/teléfono, roles y suspensión/reactivación. Las operaciones sensibles están limitadas al propietario y auditadas en Supabase. | Verificar el primer ingreso y todos los cambios con cuentas reales controladas. |
| Configuración | Consulta y edición de datos comerciales, contacto, atención, pagos, preparación, horario semanal y fechas especiales. | Verificar los cambios con datos operativos reales y confirmar cómo se mostrarán las excepciones en la aplicación móvil. |

## Siguientes bloques recomendados

1. ~~**Revisión funcional autenticada**~~ **Completada el 30/09/2026** (ver sección "Revisión funcional autenticada"). Queda pendiente verificar el primer ingreso real del usuario invitado de prueba y el comportamiento tras una hora de sesión abierta (renovación de token).
2. **Instalador y publicación:** la versión 1.10.5 está compilada en Release, empaquetada con Velopack y publicada en GitHub Releases; incluye Setup, paquete completo, actualización delta desde 1.10.4 y portátil. La aplicación instalada puede detectarla desde **Buscar actualizaciones**. Pendiente: probar la instalación en un equipo limpio y decidir si se requiere firma de código.
3. **Validaciones operativas:** recorrer con datos reales las reglas del catálogo, equivalencias de presentaciones, vencimientos de inventario, cálculos de promociones y criterios de ventas completadas (ver tabla de módulos). La revisión funcional cubrió la mecánica de los formularios; estas validaciones de negocio siguen abiertas.
4. **Login en 3D (opcional):** el diseño de referencia gira la tarjeta con perspectiva real; se pospuso por una limitación del equipo actual. El plan completo está en el memo técnico "Login en 3D" más abajo.

## Memo técnico: login en 3D (bloque pospuesto)

El diseño de referencia (proyecto React de Figma Make) inclina la tarjeta del login con perspectiva real — el equivalente de CSS `rotateX/rotateY`. Se implementó en WPF y se desactivó el 28/09/2026 por una limitación del equipo de desarrollo, no del software.

**Qué se encontró:** en la máquina actual el pipeline 3D de WPF no renderiza nada, aunque `RenderCapability.Tier` reporta nivel 2 (aceleración disponible). Incluso una escena mínima — `Viewport3D` + `AmbientLight` + un cuadrado con `DiffuseMaterial` — no aparece en pantalla, mientras el 2D renderiza acelerado con normalidad. Causa probable: el controlador de GPU bloquea el Direct3D 9 (D3D9Ex) que WPF usa internamente para 3D. No es un defecto de la aplicación.

**Por eso la tarjeta usa inclinación 2D:** en `AlMoverMouse` de `VentanaInicioSesion.xaml.cs` se aplican `SkewTransform` (AngleX/AngleY), `RotateTransform`, `ScaleTransform` y `TranslateTransform` (nombres `TarjetaSesgo`, `TarjetaRotacion`, `TarjetaEscala`, `TarjetaT`) en función de la posición del mouse, y en `AlSalirMouse` se devuelven a cero animados. Se ve similar en movimiento, pero las líneas permanecen paralelas (no hay trapecio de perspectiva).

**Cómo implementar el 3D real cuando se desee** — primero verificar que el equipo destino renderiza WPF 3D con una escena mínima; si no lo hace, actualizar el controlador de GPU o probar otro equipo:

1. Envolver la tarjeta (`TarjetaAcceso`) en un `Viewport3D` dentro de `PantallaLogin`, dándole tamaño fijo (la geometría 3D exige dimensiones conocidas):

```xml
<Viewport3D Width="424" Height="600" HorizontalAlignment="Center" VerticalAlignment="Center">
    <Viewport3D.Camera>
        <PerspectiveCamera Position="0,0,540" LookDirection="0,0,-1"
                           UpDirection="0,1,0" FieldOfView="60"/>
    </Viewport3D.Camera>
    <ModelVisual3D>
        <ModelVisual3D.Content><AmbientLight Color="White"/></ModelVisual3D.Content>
    </ModelVisual3D>
    <Viewport2DVisual3D>
        <Viewport2DVisual3D.Geometry>
            <MeshGeometry3D Positions="-212,300,0 212,300,0 -212,-300,0 212,-300,0"
                            TriangleIndices="0 1 2 2 1 3"
                            TextureCoordinates="0,0 1,0 0,1 1,1"/>
        </Viewport2DVisual3D.Geometry>
        <Viewport2DVisual3D.Material>
            <DiffuseMaterial Viewport2DVisual3D.IsVisualHostMaterial="True" Brush="White"/>
        </Viewport2DVisual3D.Material>
        <Viewport2DVisual3D.Transform>
            <Transform3DGroup>
                <RotateTransform3D>
                    <RotateTransform3D.Rotation>
                        <AxisAngleRotation3D x:Name="InclinacionHorizontal" Axis="0,1,0" Angle="0"/>
                    </RotateTransform3D.Rotation>
                </RotateTransform3D>
                <RotateTransform3D>
                    <RotateTransform3D.Rotation>
                        <AxisAngleRotation3D x:Name="InclinacionVertical" Axis="1,0,0" Angle="0"/>
                    </RotateTransform3D.Rotation>
                </RotateTransform3D>
                <TranslateTransform3D x:Name="ParalajeTarjeta"/>
            </Transform3DGroup>
        </Viewport2DVisual3D.Transform>

        <!-- TarjetaAcceso aquí, con Width="424" Height="600" fijos -->
    </Viewport2DVisual3D>
</Viewport3D>
```

2. En `AlMoverMouse` (VentanaInicioSesion.xaml.cs), con la posición relativa al centro de la ventana:

```csharp
InclinacionHorizontal.Angle = relativoX * 14;   // giro lateral (±7°)
InclinacionVertical.Angle   = -relativoY * 12;  // giro vertical (±6°)
ParalajeTarjeta.OffsetX     = relativoX * -14;
ParalajeTarjeta.OffsetY     = relativoY * -12;
```

En `AlSalirMouse`, animar los cuatro valores de vuelta a 0 (350 ms). La animación de entrada de la tarjeta debe apuntar a `ParalajeTarjeta.OffsetY` (de 40 a 0) y a la opacidad de la tarjeta, porque los transformadores 2D ya no existen.

3. Comprobado en esta sesión: `RenderTargetBitmap` y `PrintWindow` no capturan el contenido 3D de forma fiable; para verificarlo visualmente usar captura de pantalla (`CopyFromScreen`) con la ventana `Topmost`.

4. Alternativa sin el pipeline 3D de WPF: dibujar la tarjeta con SkiaSharp aplicando una matriz de perspectiva manual (permite el trapecio real). Implica rehacer los campos como lienzo dibujado, con más trabajo de interacción.

## Conexión y seguridad

La aplicación lee `COGANA_SUPABASE_URL` y `COGANA_SUPABASE_PUBLIC_KEY` desde variables de entorno. El proyecto de Supabase configurado para Cogana es `mstcxtpjncozqztkawjg`. En la app de escritorio solo debe usarse la clave pública/publicable; nunca una clave `service_role` o secreta.

## Verificación de la versión 1.10.1

- Compilación Debug: cero errores y cero advertencias.
- Compilación Release `win-x64`: completada.
- Arranque desde la carpeta publicada: proceso respondiendo y ventana `Acceso administrativo · Cogana` visible.
- Integración Velopack: `VelopackApp.Run()` se ejecuta al inicio de `Main`, antes de crear WPF, y el empaquetador confirmó el punto de entrada.
- Paquetes generados: instalador, portable, paquete completo 1.10.1 y actualización delta 1.10.0 → 1.10.1.
- Firma digital: el instalador actual no está firmado.
- Inicio de sesión real confirmado con una membresía propietaria activa; el panel principal cargó la tienda, 25 productos y 11 categorías desde Supabase.
- Se corrigió el conteo de las tarjetas superiores del Inicio: las consultas ahora envían el rango de elementos requerido por PostgREST y leen el total exacto de `Content-Range`.
- Verificación segura del backend con el rol `authenticated`: la cuenta propietaria puede leer los módulos de la tienda bajo RLS y `get_admin_report` devuelve 25 productos activos, 25 productos sin existencia, 0 pedidos y 0 lotes para los datos actuales.
- La Edge Function `admin-users` está activa, exige JWT y valida una membresía `owner` o `admin`; las altas y modificaciones de acceso se reservan al propietario.
- Navegación autenticada confirmada visualmente en Reportes, Usuarios y las cuatro pestañas de Configuración. Se corrigieron la fecha al reanudar, el nombre de Domingo, el contraste de pestañas y las casillas de horarios.
- Pendiente de verificación manual: operaciones que modifican datos, instalación en un Windows limpio y actualización desde una versión instalada anterior.

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
