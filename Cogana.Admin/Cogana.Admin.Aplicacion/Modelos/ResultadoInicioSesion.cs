namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ResultadoInicioSesion(
    bool EsExitoso,
    string Mensaje,
    SesionUsuario? Sesion = null)
{
    public static ResultadoInicioSesion Correcto(SesionUsuario sesion) =>
        new(true, "Acceso correcto.", sesion);

    public static ResultadoInicioSesion Fallido(string mensaje) =>
        new(false, mensaje);
}
