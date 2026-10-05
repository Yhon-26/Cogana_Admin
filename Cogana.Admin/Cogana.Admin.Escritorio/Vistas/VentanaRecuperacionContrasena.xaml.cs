using System.Windows;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaRecuperacionContrasena : Window
{
    private readonly IServicioAutenticacion _servicio;
    private bool _procesando;
    private DateTimeOffset _proximoEnvio;

    public VentanaRecuperacionContrasena(IServicioAutenticacion servicio, string correo, bool cambioInicial = false)
    {
        InitializeComponent();
        _servicio = servicio;
        Correo.Text = correo;
        if (cambioInicial)
        {
            Titulo.Text = "Cambia tu contraseña inicial";
            Descripcion.Text = "Para abrir el panel debes reemplazar la contraseña temporal por una contraseña propia.";
            MostrarCambio();
        }
        Closing += (_, e) => e.Cancel = _procesando;
        Closed += (_, _) =>
        {
            _servicio.CancelarCambioContrasena();
            EnlaceOCodigo.Clear();
            NuevaContrasena.Clear();
            Confirmacion.Clear();
        };
    }

    private async void AlSolicitar(object sender, RoutedEventArgs e)
    {
        if (DateTimeOffset.UtcNow < _proximoEnvio)
        {
            Mensaje.Text = "Espera un minuto antes de solicitar otro correo.";
            return;
        }
        await EjecutarAsync(async () =>
        {
            var resultado = await _servicio.SolicitarRecuperacionAsync(Correo.Text);
            if (resultado.EsExitoso) _proximoEnvio = DateTimeOffset.UtcNow.AddMinutes(1);
            return resultado;
        });
    }

    private async void AlVerificar(object sender, RoutedEventArgs e) =>
        await EjecutarAsync(async () =>
        {
            var resultado = await _servicio.VerificarRecuperacionAsync(Correo.Text, EnlaceOCodigo.Password);
            EnlaceOCodigo.Clear();
            if (resultado.EsExitoso) MostrarCambio();
            return resultado;
        });

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        if (NuevaContrasena.Password != Confirmacion.Password)
        {
            Mensaje.Text = "Las contraseñas no coinciden.";
            return;
        }
        await EjecutarAsync(async () =>
        {
            var resultado = await _servicio.CambiarContrasenaAsync(NuevaContrasena.Password);
            NuevaContrasena.Clear();
            Confirmacion.Clear();
            if (resultado.EsExitoso)
            {
                PasoContrasena.Visibility = Visibility.Collapsed;
                Descripcion.Text = "La contraseña se guardó. Vuelve al inicio de sesión para continuar.";
            }
            return resultado;
        });
    }

    private void MostrarCambio()
    {
        PasoRecuperacion.Visibility = Visibility.Collapsed;
        PasoContrasena.Visibility = Visibility.Visible;
        NuevaContrasena.Focus();
    }

    private async Task EjecutarAsync(Func<Task<ResultadoOperacionAcceso>> operacion)
    {
        if (_procesando) return;
        _procesando = true;
        Solicitar.IsEnabled = Verificar.IsEnabled = Guardar.IsEnabled = Volver.IsEnabled = false;
        Correo.IsEnabled = EnlaceOCodigo.IsEnabled = NuevaContrasena.IsEnabled = Confirmacion.IsEnabled = false;
        Mensaje.Text = "Procesando...";
        try { Mensaje.Text = (await operacion()).Mensaje; }
        finally
        {
            _procesando = false;
            Solicitar.IsEnabled = Verificar.IsEnabled = Guardar.IsEnabled = Volver.IsEnabled = true;
            Correo.IsEnabled = EnlaceOCodigo.IsEnabled = NuevaContrasena.IsEnabled = Confirmacion.IsEnabled = true;
        }
    }

    private void AlVolver(object sender, RoutedEventArgs e) => Close();
}
