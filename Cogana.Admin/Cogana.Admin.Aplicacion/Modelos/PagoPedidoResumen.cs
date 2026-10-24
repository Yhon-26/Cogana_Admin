namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record PagoPedidoResumen(
    Guid Id,
    string Tipo,
    string Metodo,
    string Proveedor,
    string? Referencia,
    long ImporteCentimos,
    string Estado,
    string? Error,
    DateTimeOffset CreadoEn)
{
    public string CodigoTipo { get; init; } = string.Empty;
    public string CodigoEstado { get; init; } = string.Empty;
    public string CodigoMetodo { get; init; } = string.Empty;
    public string Descripcion => $"{Tipo} · {Metodo} · {Importe} · {Estado} · {Referencia ?? "Sin referencia"}";
    public string Fecha => CreadoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string Importe => $"S/ {ImporteCentimos / 100m:N2}";
}
