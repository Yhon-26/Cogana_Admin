namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record DireccionClienteDetalle(
    Guid Id,
    string Etiqueta,
    string Direccion,
    string Distrito,
    string? Referencia,
    decimal? Latitud,
    decimal? Longitud,
    bool EsPrincipal)
{
    public string Titulo => EsPrincipal ? $"{Etiqueta} · Principal" : Etiqueta;
    public string Ubicacion => $"{Direccion}, {Distrito}";
    public string ReferenciaVisible => string.IsNullOrWhiteSpace(Referencia) ? "Sin referencia" : Referencia;
}
