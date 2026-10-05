# Cogana Admin

Consulta [ESTADO_Y_HOJA_DE_RUTA.md](ESTADO_Y_HOJA_DE_RUTA.md) para ver qué está implementado, qué sigue pendiente y cómo abrir la solución en Visual Studio.

Aplicación de escritorio para administrar la tienda Cogana Santa Anita.

Este proyecto es independiente de la aplicación móvil ubicada en `C:\Cogana_app`.
No comparte interfaz, tipografías, recursos visuales ni código con ella. Ambos clientes
se conectan al mismo proyecto Supabase y respetan el mismo modelo de datos y RLS.

## Estructura

- `Cogana.Admin.Dominio`: entidades y reglas centrales.
- `Cogana.Admin.Aplicacion`: contratos y casos de uso.
- `Cogana.Admin.Infraestructura`: conexión con Supabase y servicios externos.
- `Cogana.Admin.Escritorio`: interfaz WPF y ViewModels.

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

La app se distribuye e instala con [Velopack](https://velopack.io) y se actualiza
desde la propia interfaz con el botón **Buscar actualizaciones** (tarjeta
"Conexión segura" del inicio). La versión en ejecución se muestra en esa misma tarjeta.

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
