namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record MovimientoInventarioResumen(
    Guid Id,
    string Tipo,
    long CambioCantidadBase,
    long CambioCantidadSuelta,
    long CambioUnidadesSelladas,
    string? Motivo,
    DateTimeOffset CreadoEn)
{
    public string Fecha => CreadoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string TipoVisible => Tipo switch
    {
        "opening" => "Apertura",
        "receipt" => "Recepción",
        "reservation" => "Reserva",
        "release" => "Liberación",
        "sale" => "Venta",
        "return" => "Devolución",
        "adjustment" => "Ajuste",
        "waste" => "Merma",
        "open_package" => "Apertura de paquete",
        "expire" => "Vencimiento",
        _ => Tipo
    };

    public string CambioVisible => CambioCantidadSuelta > 0
        ? $"+{CambioCantidadSuelta:N0}"
        : CambioCantidadSuelta.ToString("N0");
}
