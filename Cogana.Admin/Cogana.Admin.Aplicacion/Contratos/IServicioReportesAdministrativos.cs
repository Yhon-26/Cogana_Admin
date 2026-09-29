using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioReportesAdministrativos
{
    Task<ReporteAdministrativo> ObtenerAsync(
        Guid tiendaId,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken cancellationToken = default);
}
