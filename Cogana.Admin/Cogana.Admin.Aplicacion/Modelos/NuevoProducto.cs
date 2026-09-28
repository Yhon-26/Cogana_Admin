namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record NuevoProducto(
    string Nombre,
    string? Sku,
    string? Marca,
    string? Descripcion,
    Guid? CategoriaId,
    string UnidadBase,
    bool ControlaVencimiento,
    long StockMinimo);
