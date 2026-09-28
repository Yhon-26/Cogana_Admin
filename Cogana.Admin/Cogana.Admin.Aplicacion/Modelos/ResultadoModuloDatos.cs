namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ResultadoModuloDatos(
    bool EsExitoso,
    string Mensaje,
    ModuloDatos? Datos)
{
    public static ResultadoModuloDatos Correcto(ModuloDatos datos) =>
        new(true, "Información actualizada.", datos);

    public static ResultadoModuloDatos Fallido(string mensaje) =>
        new(false, mensaje, null);
}
