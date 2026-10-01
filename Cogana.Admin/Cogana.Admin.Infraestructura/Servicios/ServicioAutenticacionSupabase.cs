using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioAutenticacionSupabase(ClienteSupabaseRest cliente)
    : IServicioAutenticacion
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ResultadoInicioSesion> IniciarSesionAsync(
        string correo,
        string contrasena,
        CancellationToken cancellationToken = default)
    {
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

            return ResultadoInicioSesion.Correcto(sesion);
        }
        catch (OperationCanceledException)
        {
            return ResultadoInicioSesion.Fallido("El inicio de sesión fue cancelado.");
        }
        catch (HttpRequestException)
        {
            return ResultadoInicioSesion.Fallido(
                "No fue posible comunicarse con Supabase. Revisa tu conexión.");
        }
        catch (JsonException)
        {
            return ResultadoInicioSesion.Fallido(
                "Supabase devolvió una respuesta que no se pudo procesar.");
        }
    }

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
