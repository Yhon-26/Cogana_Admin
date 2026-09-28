using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioResumenInicio
{
    Task<ResultadoResumenInicio> ObtenerAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default);
}
