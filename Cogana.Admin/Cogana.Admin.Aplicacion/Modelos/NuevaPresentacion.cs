namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record NuevaPresentacion(
    Guid ProductoId,
    string Nombre,
    string? Sku,
    string Tipo,
    long CantidadBase,
    long PrecioCentimos,
    bool Sellada);
