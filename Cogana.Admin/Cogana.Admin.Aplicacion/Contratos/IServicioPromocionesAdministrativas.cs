using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioPromocionesAdministrativas
{
    Task<PromocionDetalle?> ObtenerPromocionAsync(
        Guid tiendaId,
        Guid promocionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductoPromocionSeleccion>> ObtenerProductosAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> GuardarAsync(
        Guid tiendaId,
        Guid promocionId,
        EdicionPromocion edicion,
        CancellationToken cancellationToken = default);
}
