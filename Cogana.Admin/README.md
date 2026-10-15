# Cogana Admin

Consulta [ESTADO_Y_HOJA_DE_RUTA.md](ESTADO_Y_HOJA_DE_RUTA.md) para ver qué está implementado, qué sigue pendiente y cómo abrir la solución en Visual Studio.

Aplicación de escritorio para administrar la tienda Cogana Santa Anita.

Este proyecto es independiente de la aplicación móvil ubicada en `C:\Cogana_app`.
No comparte interfaz, tipografías, recursos visuales ni código con ella. Ambos clientes
se conectan al mismo proyecto Supabase y respetan el mismo modelo de datos y RLS.

## Estructura

La recuperación mantiene el mismo diseño blanco y negro y guía tres pasos: correo, verificación y nueva contraseña. Los botones principales permanecen visibles; el contenido se desplaza si falta espacio. Incluye reenvío con cuenta regresiva de 60 segundos, entrada directa si ya existe un enlace, validación de longitud y coincidencia, y confirmación final solo tras un guardado correcto. El cambio de contraseña temporal abre directamente el paso de contraseña. La recuperación sigue verificándose dentro de Windows con el enlace original, sin abrirlo en el navegador.

El login usa una tarjeta blanca con controles negros y reserva el verde para **EN LÍNEA**. Conserva perspectiva suave y foco visible por teclado, sin un control adicional de movimiento. El enlace de recuperación se subraya al pasar el cursor, sin fondo del sistema. El indicador de acceso espera la respuesta del servicio; no estima porcentajes. Las animaciones se pausan con la ventana inactiva o minimizada.

**Recordar cuenta** comienza desmarcado. Tras un acceso correcto guarda correo y contraseña en el administrador de credenciales de Windows para el usuario actual y este equipo, con un destino separado por proyecto Supabase. Al volver a abrir la aplicación rellena los campos; siempre requiere pulsar Entrar y comprobar acceso administrativo. Desmarcarlo elimina la credencial. Abrir recuperación o iniciar el cambio obligatorio elimina también la cuenta recordada. No escribe contraseñas en archivos del proyecto ni guarda tokens de sesión. Contrato nativo: [CREDENTIALW de Microsoft](https://learn.microsoft.com/windows/win32/api/wincred/ns-wincred-credentialw).

Para comprobar geometría, renderizado, interacción y estados de acceso sin conectar con Supabase: `dotnet run --project Pruebas/Login/Login.csproj -c Release`. El modo `-- --visual` abre una tarjeta de prueba interactiva. El shader precompilado está embebido en el escritorio; `Cogana.Admin.Escritorio/Recursos/Compilar-Perspectiva.ps1` permite regenerarlo desde el archivo HLSL en Windows.

- `Cogana.Admin.Dominio`: entidades y reglas centrales.
- `Cogana.Admin.Aplicacion`: contratos y casos de uso.
- `Cogana.Admin.Infraestructura`: conexión con Supabase y servicios externos.
- `Cogana.Admin.Escritorio`: interfaz WPF y ViewModels.

## Marco de ventanas y desplazamiento

Las 15 ventanas WPF del escritorio usan una cabecera común integrada con su propio color de fondo. `WindowChrome` conserva las zonas nativas de arrastre y redimensionamiento; los botones accesibles llaman a los comandos de minimizar, maximizar/restaurar y cerrar. La cabecera cambia el icono y nombre accesible a Restaurar cuando corresponde. Las ventanas que no admiten cambios de tamaño deshabilitan esos comandos. La comprobación local de maximización respeta el área de trabajo y la barra de tareas del monitor de prueba.

Los formularios, detalles, recuperación, imágenes, configuración y reportes muestran una sola superficie: se eliminó únicamente el margen y borde decorativo exterior duplicado. El login conserva su tarjeta central. Las barras de desplazamiento comparten un pulgar redondeado de 6 px, carril transparente y estados de puntero/arrastre, manteniendo las piezas nativas `PART_Track`, rueda, desplazamiento por página y automatización. Se verificaron ambas orientaciones y que Guardar siga visible en Nuevo producto. Los selectores de archivos y demás diálogos propios de Windows conservan la apariencia del sistema.

La revisión visual y la interacción manual se realizaron en ventanas aisladas, con respuestas simuladas y sin guardar registros. Queda pendiente comprobar varios monitores y escalas de pantalla en otros equipos.

## Panel de inicio y sesión

La navegación usa un único menú lateral que puede contraerse a íconos. La cuenta y Cerrar sesión permanecen disponibles en ambos modos. El tablero reparte las tarjetas en columnas iguales según el ancho disponible; conserva el arrastre y el orden guardado. El panel vacío ofrece Restaurar panel inicial y se conserva vacío al reabrir si fue guardado así. La conexión, la tienda y las actualizaciones aparecen en una franja fija inferior, también dentro de los módulos. No agrega datos de muestra ni cambia contratos de Supabase.

Las animaciones del menú se adaptaron a WPF desde `src/App.tsx` y `src/index.css` del ZIP de Figma «Agregar animación al menú». Contraer/expandir interpola el ancho durante 250 ms con la curva CSS ease. Seleccionar produce un destello, una onda, dos anillos y 28 partículas monocromáticas desde el punto del clic; al navegar con teclado nacen desde el centro. Los efectos duran hasta 1,19 s: la luz se recorta al botón y los anillos, halos y partículas se expanden por encima de las filas vecinas en una capa compartida del menú, sin interceptar la interacción. Se conserva el grosor del borde al escalar y la interpolación de opacidad por intervalos del CSS original. La opción seleccionada incluye sombra y punto activo. Un clic repetido sustituye el efecto anterior; cambiar de opción, desplazar el menú, redimensionar, descargar el control o terminar elimina los relojes. Se respeta `SystemParameters.ClientAreaAnimation` sin añadir una casilla a la interfaz. Se conserva el menú actual, los nombres, el arrastre y las funciones; no se importaron la página de demostración ni sus datos ficticios.

El menú izquierdo permite abrir un módulo con un clic o arrastrarlo al Inicio para agregar su tarjeta. Al comenzar el arrastre desde otro módulo aparece el Inicio; cancelar devuelve la selección anterior. No hay una bandeja duplicada de selecciones. Las tarjetas conservan su orden guardado, se reordenan desde la cabecera y se quitan con ✕; agregar un módulo existente no crea duplicados.

**Cerrar sesión**, debajo de la cuenta, elimina el acceso local y solicita el cierre de la sesión de este escritorio con alcance `local`. Vuelve al login sin cerrar la aplicación, con servicios nuevos. Conserva Recordar cuenta si el usuario lo eligió, pero no inicia sesión automáticamente. Un fallo de conexión también elimina los tokens locales. El ciclo real de ventanas, la salida normal, el fallo de red y la renovación pendiente se verifican con servicios de prueba; no equivale a comprobar una cuenta de producción.

Para una ventana aislada del panel: `dotnet run --project Pruebas/Login/Login.csproj -c Release -- --panel-visual`. No carga datos ni modifica la disposición real del usuario.

## Skeleton Screens durante las consultas

El resumen de Inicio, el panel vacío, las tarjetas de módulos, las listas administrativas y la ventana de Reportes muestran marcadores grises mientras esperan sus consultas reales. La animación se adaptó del ZIP de Figma: barrido de luz de 1,6 s con desfases de 100 ms entre indicadores, barrido del panel de 1,8 s y pista inferior de 1,3 s. Los datos aparecen con una entrada de opacidad de 450 ms cuando concluye la espera. No se importó el temporizador demostrativo de 1,1 s ni se agregaron esperas artificiales.

Durante la consulta se ocultan los valores y el estado vacío; se informa “Consultando datos…” y las acciones del módulo quedan deshabilitadas. Un error retira la carga y conserva un mensaje para intentar nuevamente. Cambiar de módulo cancela la consulta anterior e ignora cualquier respuesta tardía, de modo que no pueda terminar el Skeleton ni mostrar filas de otra sección. Una conexión fallida también finaliza las tarjetas pendientes de Inicio.

Los marcadores no interceptan clics ni se anuncian como datos. Los estados textuales de carga son accesibles. Se respeta la preferencia de animaciones de Windows; minimizar, ocultar o descargar los controles detiene sus relojes. No modifica contratos, tablas, políticas ni datos compartidos con el móvil. Las capturas de prueba corresponden a servicios simulados; falta comprobar la fluidez con consultas de producción en el equipo instalado.

## Imágenes de productos

Los archivos se suben al bucket público `product-images` de Supabase Storage, con ruta `{tiendaId}/{productoId}.{extensión}`. `product_images.storage_path` vincula el objeto con el producto; el catálogo móvil lee esa ruta mediante `image_path` y obtiene la URL pública del mismo bucket. No depende del archivo original de la computadora una vez guardado correctamente. El bucket desplegado fue comprobado con límite de 5 MiB; al 11/10/2026 había cero filas de imágenes asociadas, por lo que aún falta validar la primera carga real y su visualización en ambos clientes.

En Productos, selecciona una fila y pulsa **Ver imagen** para abrir una consulta de la foto actual sin editar. El visor distingue ausencia de foto, descarga fallida y formato incompatible, permite volver a cargar y cancela la consulta al cerrar. Usa los códecs disponibles de Windows; JPG y PNG se admiten de forma nativa. No cambia buckets, políticas ni el flujo de subida existente.

## Marca del escritorio

El logo monocromático aprobado se muestra en el login, recuperación y cabecera del menú. El símbolo compacto aparece en el carril oscuro, estados del acceso y ventanas. El ejecutable y el instalador incluyen el mismo ícono de Windows con nueve tamaños y transparencia. Los recursos están embebidos; su procedencia, prompts y usos están documentados en `Cogana.Admin.Escritorio/Recursos/Marca/README.md`.

## Configuración local

La aplicación lee dos variables de entorno:

- `COGANA_SUPABASE_URL`: URL pública del proyecto Supabase.
- `COGANA_SUPABASE_PUBLIC_KEY`: clave pública o publicable del proyecto.

Opcionalmente, `COGANA_ACTUALIZACIONES_URL` cambia el repositorio de actualizaciones
(si no se define, se usa el bucket público `actualizaciones` del proyecto Cogana).

No debe utilizarse una clave `service_role` o secreta en la aplicación de escritorio.

Ejemplo de URL para el proyecto Cogana:

```text
https://mstcxtpjncozqztkawjg.supabase.co
```

Después de establecer ambas variables, inicia `Cogana.Admin.Escritorio` y usa
el botón **Verificar conexión**.

## Instalador y actualizaciones

Cada mejora terminada de este proyecto debe verificarse, incrementar su versión y publicarse en el canal de GitHub para que la aplicación instalada pueda actualizarse. Esta continuidad de publicación fue autorizada por el usuario el 11/10/2026.

La app se distribuye e instala con [Velopack](https://velopack.io) y se actualiza
desde la propia interfaz con el botón **Buscar actualizaciones** de la barra inferior. La versión en ejecución se muestra en esa misma barra.

Para generar una versión instalable:

1. Sube el número de versión en `Cogana.Admin.Escritorio\Cogana.Admin.Escritorio.csproj` (`<Version>`).
2. Ejecuta en PowerShell: `.\Publicar-Instalador.ps1` (publica en Release, win-x64, framework-dependent, y empaqueta con `vpk`).
3. Sube el contenido de la carpeta `Releases` a la raíz del bucket `actualizaciones` de Supabase Storage.

Cada equipo instala con `CoganaAdmin-win-Setup.exe`. Al publicar una versión nueva,
la app la detecta, descarga solo los archivos cambiados (paquete delta), reinstala
y se reinicia; las versiones antiguas se limpian automáticamente.

## Git y GitHub (canal de actualizaciones recomendado)

El proyecto usa git para el código y GitHub Releases como canal de actualizaciones:
publicar una versión queda en un solo comando, sin subir archivos a mano.

**Configuración única:**

1. Instala GitHub CLI y autentícate:
   ```powershell
   winget install GitHub.cli
   gh auth login
   ```
2. Crea el repositorio y sube el código (recomendado público, para que las apps
   descarguen actualizaciones sin tokens):
   ```powershell
   git remote add origin https://github.com/TU_USUARIO/cogana-admin.git
   git push -u origin main
   ```
3. Define la variable de entorno del usuario y reinicia la app:
   ```powershell
   setx COGANA_GITHUB_REPO "TU_USUARIO/cogana-admin"
   ```

**Cada versión nueva:**

```powershell
git add -A
git commit -m "Descripción del cambio"
.\Publicar-Instalador.ps1 -Github "TU_USUARIO/cogana-admin"
```

El script compila, empaqueta y publica la versión en GitHub Releases. Los equipos
con `COGANA_GITHUB_REPO` definido la detectan con "Buscar actualizaciones" y se
actualizan solos (delta incluido). Sin esa variable, la app sigue usando el bucket
de Supabase (útil como respaldo o durante la transición).

## Acceso administrativo

El escritorio inicia en `VentanaInicioSesion`. El acceso requiere:

1. Una cuenta válida de Supabase Auth con correo y contraseña.
2. Una membresía activa en `store_memberships`.
3. Rol `owner` o `admin` para la tienda.

El token se conserva solamente en memoria mientras la aplicación está abierta.
Las cuentas de cliente, anónimas o con rol `driver` no pueden abrir el panel.

## Recuperación y contraseña inicial (30/09/2026)

En el login, **¿Olvidaste tu contraseña?** abre el formulario de recuperación:

1. Introduce el correo y solicita el mensaje. La respuesta no revela si existe una cuenta.
2. Copia el enlace original del botón del correo **sin abrirlo** y pégalo en el formulario. También se admite un código si la plantilla del correo lo muestra.
3. Verifica la recuperación, introduce y confirma una contraseña nueva de al menos 10 caracteres.
4. Vuelve al login e ingresa con la contraseña nueva. La contraseña de Supabase Auth es compartida con el móvil; el cambio también afecta a ese acceso.

El escritorio intercambia el hash o código con `POST /auth/v1/verify`, siempre con tipo `recovery`.
Solo acepta enlaces HTTPS del proyecto configurado, con ruta `/auth/v1/verify` y tipo `recovery`;
no navega a enlaces suministrados ni importa tokens de acceso pegados. Un enlace abierto previamente,
consumido por un filtro de correo o vencido exige solicitar otro. No se han cambiado las redirecciones,
el Site URL, las plantillas ni el SMTP compartidos. Las plantillas personalizadas con otra ruta y los
enlaces reescritos por un proveedor de correo todavía no se admiten: debe usarse el enlace original
de Supabase o el código mostrado en el mensaje. El envío efectivo y la entrega dependen de Auth/SMTP.

La sesión de recuperación solo permite establecer la contraseña después de comprobar una membresía
administrativa activa; no abre el panel. Al terminar se solicita el cierre global de sesiones de Auth
y se borran los tokens locales. Los JWT ya emitidos pueden seguir siendo válidos hasta expirar.
Cancelar el formulario elimina la sesión local pendiente, sin modificar contraseñas ni revocar
otras sesiones. El formulario conserva enlaces y contraseñas solo en memoria y los oculta en pantalla.

La Edge Function `admin-users`, desplegada como versión 4 con verificación JWT habilitada, incorpora
el contrato `app_metadata.cogana_admin_cambio_contrasena_pendiente`:

- Solo las **cuentas nuevas** creadas mediante contraseña temporal reciben la marca `true`.
- El escritorio exige cambiarla antes de abrir el panel; cancelar nunca abre una sesión administrativa.
- La acción autenticada `change-password` recibe `store_id` y `new_password`. Valida la membresía
  activa `owner`/`admin`, actualiza la contraseña a través de Auth con el JWT del usuario y solo
  después retira la marca mediante la API administrativa del servidor. Conserva los otros metadatos.
- Si Auth rechaza la contraseña o falla retirar la marca, el cambio pendiente no habilita el panel.
- Las cuentas existentes vinculadas a una tienda conservan contraseña y metadatos. No se marca
  retroactivamente a las cuentas creadas por las versiones anteriores.
- La función bloquea el directorio y la administración de personal mientras existe un cambio pendiente.
  Este bloqueo no es una restricción global de RLS: versiones antiguas y otros endpoints conservan
  sus permisos existentes. No se modificaron las políticas compartidas ni el proyecto móvil.
- `user_metadata` no autoriza retirar la marca. No se añadieron columnas a la base de datos ni
  credenciales privilegiadas al cliente.

Comprobaciones locales del contrato, con servicios simulados y sin enviar correos:

```powershell
dotnet run --project Pruebas\Acceso\Acceso.csproj
node Pruebas\Acceso\funcion-acceso.mjs
```

El segundo comando requiere Node 22.22 o posterior y utiliza su transformación de TypeScript
para ejecutar el código real de la función con dependencias simuladas. No sustituye la validación
de un primer ingreso real, del correo recibido ni de una sesión abierta más de una hora.
