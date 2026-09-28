using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioClientesAdministrativos
{
    Task<ClienteDetalle?> ObtenerClienteAsync(
        Guid tiendaId,
        Guid clienteId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DireccionClienteDetalle>> ObtenerDireccionesAsync(
        Guid tiendaId,
        Guid clienteId,
        CancellationToken cancellationToken = default);

    Task<PreferenciasClienteDetalle?> ObtenerPreferenciasAsync(
        Guid tiendaId,
        Guid clienteId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PedidoClienteResumen>> ObtenerPedidosAsync(
        Guid tiendaId,
        Guid clienteId,
        CancellationToken cancellationToken = default);
}
