namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record PedidoClienteResumen(
    Guid Id,
    string Numero,
    string Entrega,
    string Estado,
    string EstadoPago,
    long TotalCentimos,
    DateTimeOffset CreadoEn)
{
    public string Fecha => CreadoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string Total => $"S/ {TotalCentimos / 100m:N2}";
}
