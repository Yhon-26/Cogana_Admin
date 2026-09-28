namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ItemPedidoDetalle(
    Guid Id,
    string Producto,
    string Presentacion,
    string UnidadBase,
    string ModoSolicitud,
    long? CantidadSolicitada,
    long? ImporteSolicitadoCentimos,
    long CantidadEstimada,
    long? CantidadPreparada,
    long ImporteEstimadoCentimos,
    long DescuentoEstimadoCentimos,
    long? ImporteFinalCentimos,
    long? DescuentoFinalCentimos,
    string PoliticaSustitucion)
{
    public string Solicitud => ModoSolicitud switch
    {
        "money" => $"Por S/ {ImporteSolicitadoCentimos.GetValueOrDefault() / 100m:N2}",
        "presentation" => $"{CantidadSolicitada.GetValueOrDefault():N0} presentación(es)",
        _ => $"{CantidadSolicitada.GetValueOrDefault():N0} {AbreviarUnidad(UnidadBase)}"
    };

    public string Preparado => CantidadPreparada is null
        ? $"Estimado: {CantidadEstimada:N0} {AbreviarUnidad(UnidadBase)}"
        : $"Preparado: {CantidadPreparada:N0} {AbreviarUnidad(UnidadBase)}";

    public string Total => $"S/ {(ImporteFinalCentimos ?? ImporteEstimadoCentimos) / 100m:N2}";

    private static string AbreviarUnidad(string unidad) => unidad switch
    {
        "gram" => "g",
        "milliliter" => "ml",
        _ => "unid."
    };
}
