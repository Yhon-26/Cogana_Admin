namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ResultadoResumenInicio(
    bool EsExitoso,
    string Mensaje,
    ResumenInicio? Resumen)
{
    public static ResultadoResumenInicio Correcto(ResumenInicio resumen) =>
        new(true, "Resumen actualizado.", resumen);

    public static ResultadoResumenInicio Fallido(string mensaje) =>
        new(false, mensaje, null);
}
