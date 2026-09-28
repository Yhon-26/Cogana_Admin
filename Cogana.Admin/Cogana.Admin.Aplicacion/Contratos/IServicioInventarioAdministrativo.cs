using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioInventarioAdministrativo
{
    Task<LoteInventarioDetalle?> ObtenerLoteAsync(
        Guid tiendaId,
        Guid loteId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MovimientoInventarioResumen>> ObtenerMovimientosAsync(
        Guid tiendaId,
        Guid loteId,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> AjustarExistenciaAsync(
        Guid tiendaId,
        Guid loteId,
        AjusteInventario ajuste,
        CancellationToken cancellationToken = default);
}
