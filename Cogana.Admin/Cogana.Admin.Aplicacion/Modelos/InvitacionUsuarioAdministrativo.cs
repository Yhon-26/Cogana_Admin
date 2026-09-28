namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record InvitacionUsuarioAdministrativo(
    string Correo,
    string NombreCompleto,
    string? Telefono,
    string Rol,
    string ContrasenaInicial);
