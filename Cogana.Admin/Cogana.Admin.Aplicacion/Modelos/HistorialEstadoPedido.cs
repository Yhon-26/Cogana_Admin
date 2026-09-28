namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record HistorialEstadoPedido(
    Guid Id,
    string? EstadoAnterior,
    string EstadoNuevo,
    string? Motivo,
    string? Nota,
    DateTimeOffset CreadoEn)
{
    public string Fecha => CreadoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
