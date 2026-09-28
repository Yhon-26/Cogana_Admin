namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record EntregaPedidoDetalle(
    Guid Id,
    string Estado,
    string? Destinatario,
    string? Notas,
    DateTimeOffset AsignadoEn,
    DateTimeOffset? IniciadoEn,
    DateTimeOffset? EntregadoEn,
    IReadOnlyList<EventoEntregaResumen> Eventos);

public sealed record EventoEntregaResumen(
    Guid Id,
    string Tipo,
    string? Nota,
    DateTimeOffset CreadoEn)
{
    public string Fecha => CreadoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
