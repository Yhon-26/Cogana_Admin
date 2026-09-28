namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record EdicionUsuarioAdministrativo(
    Guid UsuarioId,
    string NombreCompleto,
    string? Telefono,
    string Rol,
    bool EstaActivo);
