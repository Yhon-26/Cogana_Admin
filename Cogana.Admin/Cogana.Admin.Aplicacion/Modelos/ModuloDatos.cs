namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ModuloDatos(
    string Encabezado1,
    string Encabezado2,
    string Encabezado3,
    string Encabezado4,
    string Encabezado5,
    string AccionPrincipal,
    IReadOnlyList<FilaModuloDatos> Filas);
