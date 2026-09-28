namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record LoteInventarioDetalle(
    Guid Id,
    Guid ProductoId,
    string Producto,
    string UnidadBase,
    string Codigo,
    string? Proveedor,
    DateTimeOffset RecibidoEn,
    DateOnly? VenceEl,
    long CantidadDisponible,
    string Estado,
    string? Notas);
