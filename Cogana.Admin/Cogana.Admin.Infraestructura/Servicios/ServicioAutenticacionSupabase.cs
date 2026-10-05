using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioAutenticacionSupabase(ClienteSupabaseRest cliente)
    : IServicioAutenticacion
{
    private SesionUsuario? _sesionCambio;
    private bool _cambioInicial;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ResultadoInicioSesion> IniciarSesionAsync(
        string correo,
        string contrasena,
        CancellationToken cancellationToken = default)
    {
        CancelarCambioContrasena();
        if (!cliente.EstaConfigurado)
        {
            return ResultadoInicioSesion.Fallido(
                "La conexión con Supabase todavía no está configurada.");
        }

        try
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post,
                "/auth/v1/token?grant_type=password")
            {
                Content = JsonContent.Create(new
                {
                    email = correo.Trim(),
                    password = contrasena
                })
            };

            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                return ResultadoInicioSesion.Fallido(
                    "El correo o la contraseña no son correctos.");
            }

            var autenticacion = await respuesta.Content.ReadFromJsonAsync<RespuestaAutenticacion>(
                OpcionesJson,
                cancellationToken);

            if (autenticacion?.Usuario is null ||
                string.IsNullOrWhiteSpace(autenticacion.TokenAcceso) ||
                autenticacion.Usuario.EsAnonimo)
            {
                return ResultadoInicioSesion.Fallido(
                    "Supabase no devolvió una sesión válida.");
            }

            cliente.EstablecerSesion(autenticacion.TokenAcceso, autenticacion.TokenRenovacion ?? string.Empty);
            var membresia = await ObtenerMembresiaAdministrativaAsync(
                autenticacion.Usuario.Id,
                cancellationToken);

            if (membresia is null)
            {
                cliente.LimpiarTokenAcceso();
                return ResultadoInicioSesion.Fallido(
                    "Esta cuenta no tiene acceso administrativo activo a una tienda.");
            }

            var sesion = new SesionUsuario(
                autenticacion.Usuario.Id,
                autenticacion.Usuario.Correo ?? correo.Trim(),
                autenticacion.TokenAcceso,
                autenticacion.TokenRenovacion ?? string.Empty,
                membresia.TiendaId,
                membresia.Rol);

            if (autenticacion.Usuario.MetadatosAplicacion.TryGetValue(
                "cogana_admin_cambio_contrasena_pendiente", out var pendiente) &&
                pendiente.ValueKind == JsonValueKind.True)
            {
                _sesionCambio = sesion;
                _cambioInicial = true;
                return new ResultadoInicioSesion(true, "Cambia tu contraseña inicial para continuar.", null, true);
            }

            return ResultadoInicioSesion.Correcto(sesion);
        }
        catch (OperationCanceledException)
        {
            CancelarCambioContrasena();
            return ResultadoInicioSesion.Fallido("El inicio de sesión fue cancelado.");
        }
        catch (HttpRequestException)
        {
            CancelarCambioContrasena();
            return ResultadoInicioSesion.Fallido(
                "No fue posible comunicarse con Supabase. Revisa tu conexión.");
        }
        catch (JsonException)
        {
            CancelarCambioContrasena();
            return ResultadoInicioSesion.Fallido(
                "Supabase devolvió una respuesta que no se pudo procesar.");
        }
    }

    public void CancelarCambioContrasena()
    {
        _sesionCambio = null;
        _cambioInicial = false;
        cliente.LimpiarTokenAcceso();
    }

    public Task<ResultadoOperacionAcceso> SolicitarRecuperacionAsync(string correo, CancellationToken cancellationToken = default) =>
        EjecutarOperacionAsync(async () =>
        {
            CancelarCambioContrasena();
            if (!CorreoValido(correo)) return new(false, "Ingresa un correo electrónico válido.");
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/auth/v1/recover")
            {
                Content = JsonContent.Create(new { email = correo.Trim() })
            };
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            if ((int)respuesta.StatusCode == 429) return new(false, "Espera unos minutos antes de solicitar otro correo.");
            if (!respuesta.IsSuccessStatusCode) return new(false, "No se pudo solicitar el correo. Inténtalo más tarde.");
            return new(true, "Si el correo tiene una cuenta, recibirás instrucciones de recuperación. Revisa también el correo no deseado.");
        });

    public Task<ResultadoOperacionAcceso> VerificarRecuperacionAsync(string correo, string enlaceOCodigo, CancellationToken cancellationToken = default) =>
        EjecutarOperacionAsync(async () =>
        {
            CancelarCambioContrasena();
            object cuerpo;
            var entrada = enlaceOCodigo.Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(entrada, "^[0-9]{6,10}$"))
            {
                if (!CorreoValido(correo)) return new(false, "Ingresa el correo que recibió el código.");
                cuerpo = new { email = correo.Trim(), token = entrada, type = "recovery" };
            }
            else
            {
                // Nunca navegamos a una URL proporcionada por el usuario ni aceptamos tokens de sesión pegados.
                if (!Uri.TryCreate(entrada, UriKind.Absolute, out var enlace) ||
                    enlace.Scheme != Uri.UriSchemeHttps || enlace.Authority != cliente.UrlProyecto?.Authority ||
                    enlace.AbsolutePath != "/auth/v1/verify" || !string.IsNullOrEmpty(enlace.UserInfo) ||
                    !string.IsNullOrEmpty(enlace.Fragment))
                    return new(false, "Copia el enlace original de recuperación del correo, sin abrirlo. Si ya lo abriste, solicita otro.");
                var parametros = System.Web.HttpUtility.ParseQueryString(enlace.Query);
                if (parametros["type"] != "recovery" || string.IsNullOrWhiteSpace(parametros["token"]))
                    return new(false, "El enlace no corresponde a una recuperación de contraseña.");
                cuerpo = new { token_hash = parametros["token"], type = "recovery" };
            }
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/auth/v1/verify") { Content = JsonContent.Create(cuerpo) };
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            if (!respuesta.IsSuccessStatusCode) return new(false, "El enlace o código venció, ya se utilizó o no es válido. Solicita otro correo.");
            var autenticacion = await respuesta.Content.ReadFromJsonAsync<RespuestaAutenticacion>(OpcionesJson, cancellationToken);
            if (autenticacion?.Usuario is null || autenticacion.Usuario.EsAnonimo ||
                string.IsNullOrWhiteSpace(autenticacion.TokenAcceso)) return new(false, "No se recibió una sesión de recuperación válida.");
            cliente.EstablecerSesion(autenticacion.TokenAcceso, autenticacion.TokenRenovacion ?? string.Empty);
            var membresia = await ObtenerMembresiaAdministrativaAsync(autenticacion.Usuario.Id, cancellationToken);
            if (membresia is null)
            {
                CancelarCambioContrasena();
                return new(false, "Esta cuenta no tiene acceso administrativo activo.");
            }
            _sesionCambio = new(autenticacion.Usuario.Id, autenticacion.Usuario.Correo ?? correo.Trim(),
                autenticacion.TokenAcceso, autenticacion.TokenRenovacion ?? string.Empty, membresia.TiendaId, membresia.Rol);
            _cambioInicial = autenticacion.Usuario.MetadatosAplicacion.TryGetValue(
                "cogana_admin_cambio_contrasena_pendiente", out var pendiente) && pendiente.ValueKind == JsonValueKind.True;
            return new(true, "Recuperación verificada. Establece tu nueva contraseña.");
        });

    public Task<ResultadoOperacionAcceso> CambiarContrasenaAsync(string contrasena, CancellationToken cancellationToken = default) =>
        EjecutarOperacionAsync(async () =>
        {
            if (_sesionCambio is null) return new(false, "Primero verifica la recuperación o inicia sesión con tu contraseña inicial.");
            if (contrasena.Length < 10 || string.IsNullOrWhiteSpace(contrasena)) return new(false, "La contraseña debe tener al menos 10 caracteres.");
            using var solicitud = _cambioInicial
                ? new HttpRequestMessage(HttpMethod.Post, "/functions/v1/admin-users")
                {
                    Content = JsonContent.Create(new { action = "change-password", store_id = _sesionCambio.TiendaId, new_password = contrasena })
                }
                : new HttpRequestMessage(HttpMethod.Put, "/auth/v1/user") { Content = JsonContent.Create(new { password = contrasena }) };
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
                return new(false, "No se pudo guardar. Usa una contraseña diferente y segura. Si tu sesión venció, cierra y vuelve a verificar tu acceso.");
            // Revoca las renovaciones de sesión; exige un nuevo ingreso en lugar de abrir el panel con la sesión de recuperación.
            try { await CerrarSesionAsync(cancellationToken); }
            catch (HttpRequestException) { /* La contraseña ya se guardó; no permitir que se reenvíe por un error de cierre. */ }
            catch (OperationCanceledException) { }
            finally { CancelarCambioContrasena(); }
            return new(true, "Contraseña actualizada. Inicia sesión con tu nueva contraseña. También se utilizará en la aplicación móvil.");
        });

    private async Task<ResultadoOperacionAcceso> EjecutarOperacionAsync(Func<Task<ResultadoOperacionAcceso>> operacion)
    {
        if (!cliente.EstaConfigurado) return new(false, "La conexión con Supabase todavía no está configurada.");
        try { return await operacion(); }
        catch (HttpRequestException) { return new(false, "No fue posible comunicarse con Supabase. Revisa tu conexión."); }
        catch (OperationCanceledException) { return new(false, "La operación fue cancelada o tardó demasiado. Inténtalo nuevamente."); }
        catch (JsonException) { CancelarCambioContrasena(); return new(false, "No se pudo procesar la respuesta de Supabase."); }
    }

    private static bool CorreoValido(string correo) => System.Net.Mail.MailAddress.TryCreate(correo.Trim(), out var direccion) && direccion.Address == correo.Trim();

    public async Task CerrarSesionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/auth/v1/logout");
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
        }
        finally
        {
            cliente.LimpiarTokenAcceso();
        }
    }

    private async Task<RespuestaMembresia?> ObtenerMembresiaAdministrativaAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var ruta =
            "/rest/v1/store_memberships" +
            "?select=store_id,role,is_active" +
            $"&user_id=eq.{usuarioId:D}" +
            "&is_active=eq.true" +
            "&role=in.(owner,admin)" +
            "&limit=1";

        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        if (!respuesta.IsSuccessStatusCode)
        {
            return null;
        }

        var membresias = await respuesta.Content.ReadFromJsonAsync<List<RespuestaMembresia>>(
            OpcionesJson,
            cancellationToken);

        return membresias?.FirstOrDefault(m =>
            m.EstaActiva && (m.Rol is "owner" or "admin"));
    }

    private sealed class RespuestaAutenticacion
    {
        [JsonPropertyName("access_token")]
        public string TokenAcceso { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? TokenRenovacion { get; init; }

        [JsonPropertyName("user")]
        public UsuarioAutenticado? Usuario { get; init; }
    }

    private sealed class UsuarioAutenticado
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("email")]
        public string? Correo { get; init; }

        [JsonPropertyName("is_anonymous")]
        public bool EsAnonimo { get; init; }

        [JsonPropertyName("app_metadata")]
        public Dictionary<string, JsonElement> MetadatosAplicacion { get; init; } = [];
    }

    private sealed class RespuestaMembresia
    {
        [JsonPropertyName("store_id")]
        public Guid TiendaId { get; init; }

        [JsonPropertyName("role")]
        public string Rol { get; init; } = string.Empty;

        [JsonPropertyName("is_active")]
        public bool EstaActiva { get; init; }
    }
}
