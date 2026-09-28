namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record SesionUsuario(
    Guid UsuarioId,
    string Correo,
    string TokenAcceso,
    string TokenRenovacion,
    Guid TiendaId,
    string Rol);
