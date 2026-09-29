namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ReporteAdministrativo(
    DateOnly FechaInicio,
    DateOnly FechaFin,
    ResumenReporte Resumen,
    IReadOnlyList<VentaDiariaReporte> VentasDiarias,
    IReadOnlyList<EstadoPedidoReporte> EstadosPedido,
    IReadOnlyList<ProductoVendidoReporte> ProductosVendidos,
    IReadOnlyList<MetodoPagoReporte> MetodosPago,
    IReadOnlyList<StockBajoReporte> StockBajo,
    IReadOnlyList<LoteVencimientoReporte> LotesPorVencer);

public sealed record ResumenReporte(
    long Pedidos,
    long Completados,
    long Cancelados,
    long Activos,
    long VentasCentimos,
    long TicketPromedioCentimos,
    long DescuentosCentimos,
    long ProductosActivos,
    long ProductosStockBajo,
    long LotesPorVencer,
    long LotesVencidos);

public sealed record VentaDiariaReporte(DateOnly Fecha, int Pedidos, long VentasCentimos);
public sealed record EstadoPedidoReporte(string Estado, int Pedidos, long TotalCentimos);
public sealed record ProductoVendidoReporte(Guid ProductoId, string Producto, long Cantidad, long VentasCentimos, int Pedidos);
public sealed record MetodoPagoReporte(string Metodo, int Pedidos, long TotalCentimos);
public sealed record StockBajoReporte(Guid ProductoId, string Producto, string Unidad, long StockActual, long StockMinimo);
public sealed record LoteVencimientoReporte(Guid LoteId, string Lote, string Producto, DateOnly FechaVencimiento, long Cantidad, string Estado);
