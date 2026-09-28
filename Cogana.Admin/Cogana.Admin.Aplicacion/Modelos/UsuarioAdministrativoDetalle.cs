namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record UsuarioAdministrativoDetalle(
    Guid Id,
    string Correo,
    string NombreCompleto,
    string? Telefono,
    string Rol,
    bool EstaActivo,
    DateTimeOffset CreadoEn,
    DateTimeOffset? UltimoAcceso);
