namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ProveedorDetalle(
    Guid Id,
    string Nombre,
    string? Ruc,
    string? Telefono,
    bool EstaActivo);
