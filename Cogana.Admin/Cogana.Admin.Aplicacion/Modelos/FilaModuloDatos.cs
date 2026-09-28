namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record FilaModuloDatos(
    Guid Id,
    string Columna1,
    string Columna2,
    string Columna3,
    string Columna4,
    string Columna5,
    string Estado,
    string CodigoEstado = "")
{
    public string TextoBusqueda => string.Join(
        ' ',
        Columna1,
        Columna2,
        Columna3,
        Columna4,
        Columna5,
        Estado);
}
