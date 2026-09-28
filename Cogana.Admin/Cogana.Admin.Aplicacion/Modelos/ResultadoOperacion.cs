namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ResultadoOperacion(bool EsExitoso, string Mensaje)
{
    public static ResultadoOperacion Correcta(string mensaje) => new(true, mensaje);
    public static ResultadoOperacion Fallida(string mensaje) => new(false, mensaje);
}
