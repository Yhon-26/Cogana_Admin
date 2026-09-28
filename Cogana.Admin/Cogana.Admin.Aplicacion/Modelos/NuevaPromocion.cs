namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record NuevaPromocion(
    string Nombre,
    string? Descripcion,
    string Tipo,
    long Valor,
    DateTimeOffset Inicio,
    DateTimeOffset Fin);
