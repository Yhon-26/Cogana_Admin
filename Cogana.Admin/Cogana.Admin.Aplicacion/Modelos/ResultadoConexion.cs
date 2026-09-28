namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ResultadoConexion(bool EsExitosa, string Mensaje)
{
    public static ResultadoConexion Correcta(string mensaje) => new(true, mensaje);
    public static ResultadoConexion Fallida(string mensaje) => new(false, mensaje);
}
