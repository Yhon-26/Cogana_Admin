using System.Net.Mail;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaRecuperacionContrasena : Window
{
    private readonly IServicioAutenticacion _servicio;
    private readonly bool _cambioInicial;
    private readonly DispatcherTimer _temporizador = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _procesando;
    private int _paso = 1;
    private DateTimeOffset _proximoEnvio;

    public VentanaRecuperacionContrasena(IServicioAutenticacion servicio, string correo, bool cambioInicial = false)
    {
        InitializeComponent();
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 32);
        _servicio = servicio;
        _cambioInicial = cambioInicial;
        Correo.Text = correo;
        _temporizador.Tick += (_, _) => ActualizarAcciones();
        if (cambioInicial) MostrarPaso(3);
        Loaded += (_, _) => EnfocarPaso();
        Closing += (_, e) => e.Cancel = _procesando;
        Closed += (_, _) =>
        {
            _temporizador.Stop();
            _servicio.CancelarCambioContrasena();
            EnlaceOCodigo.Clear();
            NuevaContrasena.Clear();
            Confirmacion.Clear();
        };
    }

    private bool ValidarCorreo()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Correo.Text) && new MailAddress(Correo.Text.Trim()).Address == Correo.Text.Trim()) return true;
        }
        catch (FormatException) { }
        MostrarMensaje("Ingresa un correo electrónico válido.");
        Correo.Focus();
        return false;
    }

    private async void AlSolicitar(object sender, RoutedEventArgs e)
    {
        if (_procesando || _paso > 2 || !ValidarCorreo()) return;
        if (DateTimeOffset.UtcNow < _proximoEnvio)
        {
            MostrarMensaje("Espera a que termine la cuenta regresiva antes de solicitar otro correo.");
            return;
        }
        await EjecutarAsync("Enviando el correo de recuperación...", async () =>
        {
            var resultado = await _servicio.SolicitarRecuperacionAsync(Correo.Text.Trim());
            if (resultado.EsExitoso)
            {
                _proximoEnvio = DateTimeOffset.UtcNow.AddMinutes(1);
                EnlaceOCodigo.Clear();
                MostrarPaso(2);
                _temporizador.Start();
            }
            return resultado;
        });
    }

    private void AlTengoEnlace(object sender, RoutedEventArgs e)
    {
        if (_procesando || _paso != 1 || !ValidarCorreo()) return;
        MostrarPaso(2);
    }

    private void AlCambiarCorreo(object sender, RoutedEventArgs e)
    {
        if (_procesando || _paso != 2) return;
        _servicio.CancelarCambioContrasena();
        EnlaceOCodigo.Clear();
        MostrarPaso(1);
    }

    private async void AlVerificar(object sender, RoutedEventArgs e)
    {
        if (_procesando || _paso != 2) return;
        if (string.IsNullOrWhiteSpace(EnlaceOCodigo.Password))
        {
            MostrarMensaje("Pega el enlace original o el código del correo más reciente.");
            EnlaceOCodigo.Focus();
            return;
        }
        await EjecutarAsync("Verificando tu enlace de recuperación...", async () =>
        {
            var resultado = await _servicio.VerificarRecuperacionAsync(Correo.Text.Trim(), EnlaceOCodigo.Password);
            EnlaceOCodigo.Clear();
            if (resultado.EsExitoso) MostrarPaso(3);
            return resultado;
        });
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        if (_procesando || _paso != 3) return;
        if (NuevaContrasena.Password.Length < 10)
        {
            MostrarMensaje("La nueva contraseña debe tener al menos 10 caracteres.");
            NuevaContrasena.Focus();
            return;
        }
        if (NuevaContrasena.Password != Confirmacion.Password)
        {
            MostrarMensaje("Las contraseñas no coinciden. Revisa la confirmación.");
            Confirmacion.Focus();
            return;
        }
        await EjecutarAsync("Guardando tu nueva contraseña...", async () =>
        {
            var resultado = await _servicio.CambiarContrasenaAsync(NuevaContrasena.Password);
            NuevaContrasena.Clear();
            Confirmacion.Clear();
            if (resultado.EsExitoso) MostrarPaso(4);
            return resultado;
        });
    }

    private void MostrarPaso(int paso)
    {
        _paso = paso;
        if (WindowState == WindowState.Normal) Height = paso == 1 ? 720 : paso == 4 ? 680 : 840;
        PasoCorreo.Visibility = paso == 1 ? Visibility.Visible : Visibility.Collapsed;
        PasoRecuperacion.Visibility = paso == 2 ? Visibility.Visible : Visibility.Collapsed;
        PasoContrasena.Visibility = paso == 3 ? Visibility.Visible : Visibility.Collapsed;
        PasoCompletado.Visibility = paso == 4 ? Visibility.Visible : Visibility.Collapsed;
        Solicitar.Visibility = paso == 1 ? Visibility.Visible : Visibility.Collapsed;
        Verificar.Visibility = paso == 2 ? Visibility.Visible : Visibility.Collapsed;
        Guardar.Visibility = paso == 3 ? Visibility.Visible : Visibility.Collapsed;
        IndicadorPasos.Visibility = _cambioInicial || paso == 4 ? Visibility.Collapsed : Visibility.Visible;
        Titulo.Text = paso switch
        {
            1 => "Recupera tu acceso",
            2 => "Revisa tu correo",
            3 => _cambioInicial ? "Crea tu contraseña" : "Elige una nueva contraseña",
            _ => "Contraseña actualizada"
        };
        Descripcion.Text = paso switch
        {
            1 => "Te enviaremos un enlace para crear una nueva contraseña.",
            2 => $"Usa el mensaje más reciente enviado a {Correo.Text.Trim()}.",
            3 => _cambioInicial ? "Reemplaza la contraseña temporal para continuar al panel." : "Tu acceso fue verificado. Ahora crea tu nueva contraseña.",
            _ => "El cambio se completó correctamente."
        };
        var barras = new[] { BarraPaso1, BarraPaso2, BarraPaso3 };
        var etiquetas = new[] { EtiquetaPaso1, EtiquetaPaso2, EtiquetaPaso3 };
        for (var indice = 0; indice < barras.Length; indice++)
        {
            barras[indice].Background = new SolidColorBrush(indice < paso ? Color.FromRgb(22, 22, 22) : Color.FromRgb(231, 231, 231));
            etiquetas[indice].Foreground = new SolidColorBrush(indice < paso ? Color.FromRgb(22, 22, 22) : Color.FromRgb(136, 136, 136));
        }
        Volver.Style = (Style)Resources[paso == 4 ? "Accion" : "Enlace"];
        PanelMensaje.Visibility = Visibility.Collapsed;
        Mensaje.Text = string.Empty;
        ContenidoPaso.BeginAnimation(OpacityProperty, new DoubleAnimation(0.5, 1, TimeSpan.FromMilliseconds(160)));
        ActualizarAcciones();
        EnfocarPaso();
    }

    private void EnfocarPaso()
    {
        if (_procesando) return;
        if (_paso == 1) Correo.Focus();
        else if (_paso == 2) EnlaceOCodigo.Focus();
        else if (_paso == 3) NuevaContrasena.Focus();
        else Volver.Focus();
    }

    private void AlCambiarContrasena(object sender, RoutedEventArgs e)
    {
        if (NuevaContrasena is null || Confirmacion is null || ReglaLongitud is null || ReglaConfirmacion is null) return;
        ReglaLongitud.Text = NuevaContrasena.Password.Length >= 10 ? "✓ Mínimo de 10 caracteres cumplido." : "Usa al menos 10 caracteres.";
        ReglaConfirmacion.Text = Confirmacion.Password.Length == 0 ? "Repite la nueva contraseña." :
            NuevaContrasena.Password == Confirmacion.Password ? "✓ Las contraseñas coinciden." : "Las contraseñas todavía no coinciden.";
    }

    private void AlPresionarEnter(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (_paso == 1) AlSolicitar(Solicitar, new RoutedEventArgs());
        else if (_paso == 2) AlVerificar(Verificar, new RoutedEventArgs());
        else if (_paso == 3) AlGuardar(Guardar, new RoutedEventArgs());
        e.Handled = true;
    }

    private void MostrarMensaje(string texto)
    {
        Mensaje.Text = texto;
        PanelMensaje.Visibility = string.IsNullOrWhiteSpace(texto) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ActualizarAcciones()
    {
        var segundos = Math.Max(0, (int)Math.Ceiling((_proximoEnvio - DateTimeOffset.UtcNow).TotalSeconds));
        Solicitar.IsEnabled = Reenviar.IsEnabled = !_procesando && segundos == 0;
        Solicitar.Content = segundos == 0 ? "Enviar enlace de recuperación" : $"Enviar de nuevo en {segundos} s";
        Reenviar.Content = segundos == 0 ? "Enviar otro correo" : $"Reenviar en {segundos} s";
        Verificar.IsEnabled = Guardar.IsEnabled = Volver.IsEnabled = TengoEnlace.IsEnabled = CambiarCorreo.IsEnabled = !_procesando;
        Correo.IsEnabled = EnlaceOCodigo.IsEnabled = NuevaContrasena.IsEnabled = Confirmacion.IsEnabled = !_procesando;
        Progreso.Visibility = _procesando ? Visibility.Visible : Visibility.Collapsed;
        if (segundos == 0 || _paso >= 3) _temporizador.Stop();
    }

    private async Task EjecutarAsync(string estado, Func<Task<ResultadoOperacionAcceso>> operacion)
    {
        if (_procesando) return;
        _procesando = true;
        ActualizarAcciones();
        MostrarMensaje(estado);
        try
        {
            var resultado = await operacion();
            if (_paso != 4 && (!resultado.EsExitoso || _paso != 3)) MostrarMensaje(resultado.Mensaje);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            MostrarMensaje("No se pudo completar la operación. Revisa tu conexión e inténtalo nuevamente.");
        }
        finally
        {
            _procesando = false;
            ActualizarAcciones();
            EnfocarPaso();
        }
    }

    private void AlVolver(object sender, RoutedEventArgs e) { if (!_procesando) Close(); }
}
