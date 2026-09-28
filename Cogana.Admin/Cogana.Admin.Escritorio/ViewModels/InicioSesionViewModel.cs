using System.Net.Mail;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.ViewModels;

public sealed class InicioSesionViewModel : BaseViewModel
{
    private readonly IServicioAutenticacion _servicioAutenticacion;
    private string _correo = string.Empty;
    private string _mensaje = string.Empty;
    private bool _estaProcesando;

    public InicioSesionViewModel(IServicioAutenticacion servicioAutenticacion)
    {
        _servicioAutenticacion = servicioAutenticacion;
    }

    public event Action<SesionUsuario>? SesionIniciada;

    public string Correo
    {
        get => _correo;
        set => Establecer(ref _correo, value);
    }

    public string Mensaje
    {
        get => _mensaje;
        private set => Establecer(ref _mensaje, value);
    }

    public bool EstaProcesando
    {
        get => _estaProcesando;
        private set => Establecer(ref _estaProcesando, value);
    }

    public async Task IniciarSesionAsync(string contrasena)
    {
        Mensaje = string.Empty;

        if (!CorreoEsValido(Correo))
        {
            Mensaje = "Ingresa un correo electrónico válido.";
            return;
        }

        if (string.IsNullOrWhiteSpace(contrasena))
        {
            Mensaje = "Ingresa tu contraseña.";
            return;
        }

        EstaProcesando = true;
        Mensaje = "Verificando acceso...";

        try
        {
            var resultado = await _servicioAutenticacion.IniciarSesionAsync(
                Correo,
                contrasena);

            if (!resultado.EsExitoso || resultado.Sesion is null)
            {
                Mensaje = resultado.Mensaje;
                return;
            }

            Mensaje = "Acceso correcto.";
            SesionIniciada?.Invoke(resultado.Sesion);
        }
        finally
        {
            EstaProcesando = false;
        }
    }

    private static bool CorreoEsValido(string correo)
    {
        try
        {
            return new MailAddress(correo.Trim()).Address == correo.Trim();
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
