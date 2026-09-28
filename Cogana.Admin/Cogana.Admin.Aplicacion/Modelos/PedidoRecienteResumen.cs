namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record PedidoRecienteResumen(
    Guid Id,
    string Numero,
    string Cliente,
    string Estado,
    decimal Total,
    DateTimeOffset Fecha);
