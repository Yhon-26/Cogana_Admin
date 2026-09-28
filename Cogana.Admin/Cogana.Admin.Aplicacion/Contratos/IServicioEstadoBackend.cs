using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioEstadoBackend
{
    Task<ResultadoConexion> ProbarConexionAsync(CancellationToken cancellationToken = default);
}
