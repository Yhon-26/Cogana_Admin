namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ClienteDetalle(
    Guid Id,
    string Nombre,
    string Telefono,
    DateTimeOffset? TelefonoVerificadoEn,
    string? Correo,
    string Estado,
    DateTimeOffset? EfectivoRestringidoHasta,
    DateTimeOffset CreadoEn);
