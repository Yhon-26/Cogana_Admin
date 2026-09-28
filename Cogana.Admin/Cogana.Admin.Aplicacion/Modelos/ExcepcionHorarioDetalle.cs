namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ExcepcionHorarioDetalle(
    Guid? Id,
    DateOnly Fecha,
    TimeSpan? Apertura,
    TimeSpan? Cierre,
    bool Cerrado,
    string? Mensaje);
