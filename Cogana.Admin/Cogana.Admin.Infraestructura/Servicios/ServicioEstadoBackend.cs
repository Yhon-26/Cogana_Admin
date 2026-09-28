using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioEstadoBackend(ClienteSupabaseRest cliente) : IServicioEstadoBackend
{
    public async Task<ResultadoConexion> ProbarConexionAsync(
        CancellationToken cancellationToken = default)
    {
        if (!cliente.EstaConfigurado)
        {
            return ResultadoConexion.Fallida(
                "Falta configurar la URL o la clave pública de Supabase.");
        }

        try
        {
            using var respuesta = await cliente.ObtenerAsync("/auth/v1/settings", cancellationToken);

            return respuesta.IsSuccessStatusCode
                ? ResultadoConexion.Correcta("Conexión correcta con Cogana/Supabase.")
                : ResultadoConexion.Fallida(
                    $"Supabase respondió con el estado {(int)respuesta.StatusCode}.");
        }
        catch (OperationCanceledException)
        {
            return ResultadoConexion.Fallida("La comprobación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoConexion.Fallida(
                "No se pudo contactar con Supabase. Revisa internet y la URL configurada.");
        }
    }
}
