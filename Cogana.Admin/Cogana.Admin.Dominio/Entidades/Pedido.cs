namespace Cogana.Admin.Dominio.Entidades;

public sealed class Pedido
{
    public Guid Id { get; init; }
    public Guid TiendaId { get; init; }
    public Guid ClienteId { get; init; }
    public string Numero { get; init; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoPago { get; set; } = string.Empty;
    public int TotalCentimos { get; set; }
    public DateTimeOffset CreadoEn { get; init; }
}
