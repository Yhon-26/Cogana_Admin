using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioOperacionPedidos
{
    Task<ResultadoOperacion> ConfirmarPagoAsync(Guid tienda, Guid pedido, Guid pago, string referencia, Guid operacion, CancellationToken token = default);
    Task<ResultadoOperacion> RechazarPagoAsync(Guid tienda, Guid pago, string motivo, Guid operacion, CancellationToken token = default);
    Task<ResultadoOperacion> LiquidarDiferenciaAsync(Guid tienda, Guid pago, string referencia, Guid operacion, CancellationToken token = default);
    Task<ResultadoOperacion> FinalizarPreparacionAsync(Guid tienda, Guid pedido, IReadOnlyList<CantidadPreparada> cantidades, Guid operacion, CancellationToken token = default);
    Task<ResultadoOperacion> AsignarRepartidorAsync(Guid tienda, Guid pedido, Guid repartidor, Guid operacion, CancellationToken token = default);
    Task<ResultadoOperacion> CompletarRecojoAsync(Guid tienda, Guid pedido, string codigo, Guid operacion, CancellationToken token = default);
    Task<ResultadoOperacion> CompletarEntregaAsync(Guid tienda, Guid asignacion, string codigo, string destinatario, Guid operacion, CancellationToken token = default);
    Task<IReadOnlyList<UsuarioAdministrativoDetalle>> ObtenerRepartidoresAsync(Guid tienda, CancellationToken token = default);
    Task<IReadOnlyList<SustitucionPedido>> ObtenerSustitucionesAsync(Guid tienda, Guid pedido, CancellationToken token=default);
    Task<IReadOnlyList<PresentacionSustitucion>> BuscarReemplazosAsync(Guid tienda, string busqueda, CancellationToken token=default);
    Task<ResultadoOperacion> ProponerSustitucionAsync(Guid tienda, Guid pedido, Guid articulo, Guid presentacion, long cantidad, DateTimeOffset vence, Guid operacion, CancellationToken token=default);
    Task<ResultadoOperacion> DecidirSustitucionAsync(Guid tienda, Guid sustitucion, bool aceptar, Guid operacion, CancellationToken token=default);
}

public sealed record CantidadPreparada(Guid ArticuloId, long Cantidad);
public sealed record SustitucionPedido(Guid Id,Guid ArticuloId,string Estado,long Diferencia,DateTimeOffset? Vence)
{
    public string Articulo {get;init;}=string.Empty;
    public string Descripcion=>$"{Articulo} · {Estado} · Diferencia S/ {Diferencia/100m:N2} · Vence: {Vence?.ToLocalTime():dd/MM HH:mm}";
}
public sealed record PresentacionSustitucion(Guid Id,string Nombre,string Producto,string Unidad,long CantidadBase,long PrecioCentimos)
{
    public string Descripcion=>$"{Producto} · {Nombre} · {CantidadBase} {Unidad} · S/ {PrecioCentimos/100m:N2}";
}
