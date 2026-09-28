namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ResumenInicio(
    string NombreTienda,
    string Distrito,
    string EstadoTienda,
    string? MensajeTienda,
    int ProductosActivos,
    int CategoriasActivas,
    int LotesPorVencer,
    int PedidosPendientes,
    int ProveedoresActivos,
    IReadOnlyList<PedidoRecienteResumen> PedidosRecientes,
    DateTimeOffset ActualizadoEn);
