using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioUsuariosAdministrativosSupabase(ClienteSupabaseRest cliente)
    : IServicioUsuariosAdministrativos
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<UsuarioAdministrativoDetalle>> ObtenerUsuariosAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default)
    {
        using var respuesta = await EjecutarAsync(
            new { action = "list", store_id = tiendaId },
            cancellationToken);

        if (!respuesta.IsSuccessStatusCode)
        {
            throw new HttpRequestException(await LeerErrorAsync(respuesta, cancellationToken));
        }

        var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaListaUsuarios>(
            OpcionesJson,
            cancellationToken);

        return contenido?.Usuarios.Select(Mapear).ToList() ?? [];
    }

    public async Task<UsuarioAdministrativoDetalle?> ObtenerUsuarioAsync(
        Guid tiendaId,
        Guid usuarioId,
        CancellationToken cancellationToken = default) =>
        (await ObtenerUsuariosAsync(tiendaId, cancellationToken))
        .FirstOrDefault(usuario => usuario.Id == usuarioId);

    public Task<ResultadoOperacion> InvitarAsync(
        Guid tiendaId,
        InvitacionUsuarioAdministrativo invitacion,
        CancellationToken cancellationToken = default) =>
        EjecutarOperacionAsync(new
        {
            action = "invite",
            store_id = tiendaId,
            email = invitacion.Correo.Trim(),
            full_name = invitacion.NombreCompleto.Trim(),
            phone = LimpiarOpcional(invitacion.Telefono),
            role = invitacion.Rol,
            temporary_password = invitacion.ContrasenaInicial
        }, cancellationToken);

    public Task<ResultadoOperacion> ActualizarAsync(
        Guid tiendaId,
        EdicionUsuarioAdministrativo edicion,
        CancellationToken cancellationToken = default) =>
        EjecutarOperacionAsync(new
        {
            action = "update",
            store_id = tiendaId,
            user_id = edicion.UsuarioId,
            full_name = edicion.NombreCompleto.Trim(),
            phone = LimpiarOpcional(edicion.Telefono),
            role = edicion.Rol,
            is_active = edicion.EstaActivo
        }, cancellationToken);

    private async Task<ResultadoOperacion> EjecutarOperacionAsync(
        object datos,
        CancellationToken cancellationToken)
    {
        try
        {
            using var respuesta = await EjecutarAsync(datos, cancellationToken);
            var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaOperacionUsuario>(
                OpcionesJson,
                cancellationToken);

            return respuesta.IsSuccessStatusCode
                ? ResultadoOperacion.Correcta(contenido?.Mensaje ?? "Operación completada.")
                : ResultadoOperacion.Fallida(
                    contenido?.Error ?? "No fue posible completar la operación de acceso.");
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida(
                "No fue posible comunicarse con el servicio de usuarios de Supabase.");
        }
        catch (JsonException)
        {
            return ResultadoOperacion.Fallida(
                "Supabase devolvió una respuesta de usuarios que no se pudo procesar.");
        }
    }

    private async Task<HttpResponseMessage> EjecutarAsync(
        object datos,
        CancellationToken cancellationToken)
    {
        if (!cliente.EstaConfigurado)
        {
            throw new HttpRequestException("La conexión con Supabase no está configurada.");
        }

        var solicitud = new HttpRequestMessage(HttpMethod.Post, "/functions/v1/admin-users")
        {
            Content = JsonContent.Create(datos)
        };

        return await cliente.EnviarAsync(solicitud, cancellationToken);
    }

    private static async Task<string> LeerErrorAsync(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        try
        {
            var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaOperacionUsuario>(
                OpcionesJson,
                cancellationToken);
            return contenido?.Error ?? "No fue posible consultar los usuarios.";
        }
        catch (JsonException)
        {
            return "No fue posible consultar los usuarios.";
        }
    }

    private static UsuarioAdministrativoDetalle Mapear(UsuarioDto usuario) => new(
        usuario.Id,
        usuario.Correo,
        string.IsNullOrWhiteSpace(usuario.NombreCompleto) ? "Sin nombre registrado" : usuario.NombreCompleto,
        usuario.Telefono,
        usuario.Rol,
        usuario.EstaActivo,
        usuario.CreadoEn,
        usuario.UltimoAcceso);

    private static string? LimpiarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private sealed class RespuestaListaUsuarios
    {
        [JsonPropertyName("users")]
        public List<UsuarioDto> Usuarios { get; init; } = [];
    }

    private sealed class RespuestaOperacionUsuario
    {
        [JsonPropertyName("message")]
        public string? Mensaje { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    private sealed class UsuarioDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("email")]
        public string Correo { get; init; } = string.Empty;

        [JsonPropertyName("full_name")]
        public string NombreCompleto { get; init; } = string.Empty;

        [JsonPropertyName("phone")]
        public string? Telefono { get; init; }

        [JsonPropertyName("role")]
        public string Rol { get; init; } = string.Empty;

        [JsonPropertyName("is_active")]
        public bool EstaActivo { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreadoEn { get; init; }

        [JsonPropertyName("last_sign_in_at")]
        public DateTimeOffset? UltimoAcceso { get; init; }
    }
}
