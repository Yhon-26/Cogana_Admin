namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record HorarioAtencionDetalle(
    int DiaSemana,
    TimeSpan? Apertura,
    TimeSpan? Cierre,
    bool Cerrado);
