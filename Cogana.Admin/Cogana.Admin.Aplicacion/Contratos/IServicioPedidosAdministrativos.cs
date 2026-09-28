using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioPedidosAdministrativos
{
    Task<PedidoDetalle?> ObtenerPedidoAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemPedidoDetalle>> ObtenerItemsAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistorialEstadoPedido>> ObtenerHistorialAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PagoPedidoResumen>> ObtenerPagosAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default);

    Task<EntregaPedidoDetalle?> ObtenerEntregaAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> CambiarEstadoAsync(
        Guid tiendaId,
        Guid pedidoId,
        string nuevoEstado,
        string? motivo,
        string? nota,
        CancellationToken cancellationToken = default);
}
