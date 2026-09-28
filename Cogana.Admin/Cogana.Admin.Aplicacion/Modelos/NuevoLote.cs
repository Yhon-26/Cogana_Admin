namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record NuevoLote(
    Guid OperacionId,
    Guid ProductoId,
    Guid? ProveedorId,
    string Codigo,
    DateOnly? Vencimiento,
    long Cantidad,
    string? Notas);
