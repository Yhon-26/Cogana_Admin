namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ProductoDetalle(
    Guid Id,
    string Nombre,
    string? Sku,
    string? Marca,
    string? Descripcion,
    Guid? CategoriaId,
    string UnidadBase,
    bool ControlaVencimiento,
    long StockMinimo,
    bool EstaActivo,
    int PresentacionesActivas,
    int LotesDisponibles,
    long StockTotal);
