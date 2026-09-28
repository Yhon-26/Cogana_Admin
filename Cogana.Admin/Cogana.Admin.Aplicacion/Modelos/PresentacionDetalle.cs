namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record PresentacionDetalle(
    Guid Id,
    Guid ProductoId,
    string Nombre,
    string? Sku,
    string Tipo,
    long CantidadBase,
    long PrecioCentimos,
    bool Sellada,
    bool EstaActiva);
