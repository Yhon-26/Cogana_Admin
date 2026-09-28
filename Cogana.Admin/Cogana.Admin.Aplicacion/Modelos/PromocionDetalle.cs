namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record PromocionDetalle(
    Guid Id,
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
    bool EstaActiva,
    IReadOnlySet<Guid> ProductosSeleccionados);
