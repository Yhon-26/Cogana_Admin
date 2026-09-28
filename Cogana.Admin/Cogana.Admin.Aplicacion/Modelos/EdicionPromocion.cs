namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record EdicionPromocion(
    Guid OperacionId,
    string Nombre,
    string? Descripcion,
    string Tipo,
    long Valor,
    long? CantidadMinima,
    long PedidoMinimoCentimos,
    string? CodigoCupon,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    int? LimiteUsos,
    int? LimitePorCliente,
    int Prioridad,
    bool Acumulable,
    IReadOnlyList<Guid> ProductoIds);
