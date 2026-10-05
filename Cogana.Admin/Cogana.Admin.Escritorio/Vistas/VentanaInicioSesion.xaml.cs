using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Cogana.Admin.Escritorio.ViewModels;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaInicioSesion : Window
{
    /// <summary>Pasos que muestra la pantalla de carga, igual que el diseño de referencia.</summary>
    private static readonly (double Hasta, string Texto)[] PasosCarga =
    [
        (20, "Verificando credenciales..."),
        (42, "Autenticando sesión..."),
        (60, "Conectando al servidor..."),
        (78, "Cargando permisos..."),
        (93, "Preparando el panel..."),
        (100, "Bienvenido al sistema")
    ];

    private static readonly Brush BrochaBotonNormal = new LinearGradientBrush(
        Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xE6, 0xE6, 0xE6), 0);

    private static readonly Brush BrochaBotonActivo = new LinearGradientBrush(
        Color.FromRgb(0x22, 0xC5, 0x5E), Color.FromRgb(0x16, 0xA3, 0x4A), 45);

    private readonly Random _azar = new();
    private DispatcherTimer? _temporizadorProgreso;
    private double _progreso;
    private bool _cargando;
    private bool _autenticacionTerminada;
    private bool _sesionIniciada;

    public VentanaInicioSesion()
    {
        InitializeComponent();
        Loaded += AlCargarVentana;

        BotonInicioSesion.Background = BrochaBotonNormal;
        BotonInicioSesion.Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));

        if (DataContext is InicioSesionViewModel viewModel)
        {
            viewModel.SesionIniciada += _ => _sesionIniciada = true;
        }
    }

    private void AlCargarVentana(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargarVentana;
        CampoCorreo.Focus();
        if (DataContext is InicioSesionViewModel viewModel)
            viewModel.CambioContrasenaRequerido += AlRequerirCambioContrasena;
    }

    private Task AlRequerirCambioContrasena()
    {
        VolverALogin();
        if (DataContext is InicioSesionViewModel viewModel)
        {
            new VentanaRecuperacionContrasena(viewModel.ServicioAutenticacion, viewModel.Correo, true)
                { Owner = this }.ShowDialog();
            CampoContrasena.Clear();
        }
        return Task.CompletedTask;
    }

    private void AlRecuperarContrasena(object sender, RoutedEventArgs e)
    {
        if (_cargando || DataContext is not InicioSesionViewModel viewModel) return;
        new VentanaRecuperacionContrasena(viewModel.ServicioAutenticacion, viewModel.Correo)
            { Owner = this }.ShowDialog();
        CampoContrasena.Clear();
    }

    // ============================ FLUJO DE ACCESO ============================

    private async void AlIniciarSesion(object sender, RoutedEventArgs e)
    {
        if (_cargando || DataContext is not InicioSesionViewModel viewModel)
        {
            return;
        }

        _cargando = true;
        _autenticacionTerminada = false;
        _sesionIniciada = false;
        BotonInicioSesion.Content = "Accediendo...";
        BotonInicioSesion.Background = BrochaBotonActivo;
        BotonInicioSesion.Foreground = new SolidColorBrush(Colors.White);

        MostrarPantallaDeCarga();

        try
        {
            await viewModel.IniciarSesionAsync(CampoContrasena.Password);
        }
        finally
        {
            _autenticacionTerminada = true;
        }
    }

    private void AlPresionarTeclaEnCampo(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AlIniciarSesion(BotonInicioSesion, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // ========================= PANTALLA DE CARGA =========================

    /// <summary>Activa la fase de carga (partículas, anillos y progreso).</summary>
    private void MostrarPantallaDeCarga()
    {
        PantallaCarga.Visibility = Visibility.Visible;
        Lienzo.Animando = true;
        AnimarOpacidad(PantallaCarga, 1, TimeSpan.FromMilliseconds(450));

        AnimarOpacidad(TarjetaAcceso, 0, TimeSpan.FromMilliseconds(450));
        PantallaLogin.IsHitTestVisible = false;

        ContenidoCargaT.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(600))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        (Resources["DecoracionCarga"] as Storyboard)?.Begin(this, true);
        AnimarOpacidad(ContenidoCarga, 1, TimeSpan.FromMilliseconds(600));

        _progreso = 0;
        _temporizadorProgreso = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
        _temporizadorProgreso.Tick += AlAvanzarProgreso;
        _temporizadorProgreso.Start();
    }

    private void AlAvanzarProgreso(object? sender, EventArgs e)
    {
        _progreso += _azar.NextDouble() * 1.8 + 0.4;
        if (_autenticacionTerminada)
        {
            _progreso += 2.4;
        }

        if (!_autenticacionTerminada && _progreso > 92)
        {
            _progreso = 92;
        }

        if (_progreso >= 100)
        {
            _progreso = 100;
            _temporizadorProgreso?.Stop();
            _temporizadorProgreso = null;
            CompletarCarga();
        }

        TextoPorcentaje.Text = $"{(int)_progreso:000}%";
        BarraProgresoRelleno.Width = PistaProgreso.ActualWidth * _progreso / 100;
        TextoPaso.Text = PasosCarga.First(paso => _progreso <= paso.Hasta).Texto;
    }

    private async void CompletarCarga()
    {
        TextoPaso.Text = "Bienvenido al sistema";

        if (_sesionIniciada)
        {
            // La ventana se cierra sola: App abre el panel tras una pequeña pausa.
            return;
        }

        await Task.Delay(650);
        VolverALogin();
    }

    private void VolverALogin()
    {
        _temporizadorProgreso?.Stop();
        _temporizadorProgreso = null;
        Lienzo.Animando = false;
        (Resources["DecoracionCarga"] as Storyboard)?.Remove(this);

        AnimarOpacidad(PantallaCarga, 0, TimeSpan.FromMilliseconds(400));

        var regreso = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        regreso.Tick += (_, _) =>
        {
            regreso.Stop();
            PantallaCarga.Visibility = Visibility.Collapsed;
        };
        regreso.Start();

        PantallaLogin.IsHitTestVisible = true;
        AnimarOpacidad(TarjetaAcceso, 1, TimeSpan.FromMilliseconds(450));

        BotonInicioSesion.Content = "ENTRAR AL PANEL";
        BotonInicioSesion.Background = BrochaBotonNormal;
        BotonInicioSesion.Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));
        _cargando = false;
    }

    /// <summary>Muestra la pantalla de carga sin iniciar sesión (verificación visual).</summary>
    internal void IniciarPantallaDeCarga()
    {
        if (!_cargando)
        {
            _cargando = true;
            MostrarPantallaDeCarga();
        }
    }

    // ====================== CAMPOS CON ETIQUETA FLOTANTE ======================

    private void AlEnfocarCampo(object sender, RoutedEventArgs e) => ActualizarCampo(sender);
    private void AlDesenfocarCampo(object sender, RoutedEventArgs e) => ActualizarCampo(sender);
    private void AlCambiarCampo(object sender, RoutedEventArgs e) => ActualizarCampo(sender);
    private void AlCambiarCampoTexto(object sender, TextChangedEventArgs e) => ActualizarCampo(sender);

    private void ActualizarCampo(object sender)
    {
        var enfocado = sender is IInputElement { IsKeyboardFocusWithin: true };
        string texto;
        bool tieneTexto;

        if (sender is TextBox campoTexto)
        {
            texto = campoTexto.Text;
            tieneTexto = texto.Length > 0;
            AnimarCampo(EtiquetaCorreo, FondoCorreo, BrilloCorreo, LineaCorreo, IconoCorreo,
                CampoCorreo.ActualWidth, "Correo electrónico", "CORREO ELECTRÓNICO", enfocado, tieneTexto);
        }
        else if (sender is PasswordBox campoContrasena)
        {
            texto = campoContrasena.Password;
            tieneTexto = texto.Length > 0;
            AnimarCampo(EtiquetaContrasena, FondoContrasena, BrilloContrasena, LineaContrasena, IconoContrasena,
                CampoContrasena.ActualWidth, "Contraseña", "CONTRASEÑA", enfocado, tieneTexto);
        }
    }

    private static void AnimarCampo(
        TextBlock etiqueta,
        Border fondo,
        UIElement brillo,
        Border linea,
        System.Windows.Shapes.Path icono,
        double anchoCampo,
        string textoNormal,
        string textoActivo,
        bool enfocado,
        bool tieneTexto)
    {
        var activo = enfocado || tieneTexto;
        var transicion = TimeSpan.FromMilliseconds(240);

        etiqueta.Text = activo ? textoActivo : textoNormal;
        var margen = activo
            ? new Thickness(44, 8, 0, 0)
            : new Thickness(46, 18, 0, 0);
        var animacionMargen = new ThicknessAnimation(margen, transicion)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        etiqueta.BeginAnimation(MarginProperty, animacionMargen);

        var animacionTamano = new DoubleAnimation(activo ? 10 : 14, transicion);
        etiqueta.BeginAnimation(FontSizeProperty, animacionTamano);
        etiqueta.FontWeight = activo ? FontWeights.SemiBold : FontWeights.Normal;

        var color = enfocado
            ? Color.FromArgb(0xF2, 0x4A, 0xDE, 0x80)
            : activo
                ? Color.FromArgb(0x73, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x61, 0xFF, 0xFF, 0xFF);
        etiqueta.Foreground = new SolidColorBrush(color);

        icono.Stroke = new SolidColorBrush(enfocado
            ? Color.FromArgb(0xE6, 0x4A, 0xDE, 0x80)
            : Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF));

        fondo.BorderBrush = new SolidColorBrush(enfocado
            ? Color.FromArgb(0x59, 0x4A, 0xDE, 0x80)
            : Color.FromArgb(0x17, 0xFF, 0xFF, 0xFF));
        fondo.Background = new SolidColorBrush(enfocado
            ? Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x09, 0xFF, 0xFF, 0xFF));

        brillo.BeginAnimation(OpacityProperty, new DoubleAnimation(enfocado ? 1 : 0, transicion));

        var anchoLinea = enfocado ? Math.Max(anchoCampo * 0.88, 0) : 0;
        linea.BeginAnimation(WidthProperty, new DoubleAnimation(anchoLinea,
            TimeSpan.FromMilliseconds(350))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    // ====================== INTERACCIÓN CON EL MOUSE ======================

    private void AlMoverMouse(object sender, MouseEventArgs e)
    {
        if (sender is not Grid raiz)
        {
            return;
        }

        var posicion = e.GetPosition(raiz);
        FocoMouseT.X = posicion.X - raiz.ActualWidth / 2;
        FocoMouseT.Y = posicion.Y - raiz.ActualHeight / 2;

        // Inclinación de la tarjeta hacia la posición del mouse:
        // rotación + sesgo + escala + parallax (pseudo-3D sin pipeline 3D)
        if (PantallaLogin.IsHitTestVisible)
        {
            var relativoX = posicion.X / raiz.ActualWidth - 0.5;
            var relativoY = posicion.Y / raiz.ActualHeight - 0.5;

            // Libera las animaciones retenidas para poder fijar valores directos
            TarjetaSesgo.BeginAnimation(SkewTransform.AngleXProperty, null);
            TarjetaSesgo.BeginAnimation(SkewTransform.AngleYProperty, null);
            TarjetaRotacion.BeginAnimation(RotateTransform.AngleProperty, null);
            TarjetaEscala.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            TarjetaEscala.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            TarjetaT.BeginAnimation(TranslateTransform.XProperty, null);
            TarjetaT.BeginAnimation(TranslateTransform.YProperty, null);

            TarjetaSesgo.AngleX = relativoX * -5;
            TarjetaSesgo.AngleY = relativoY * 4;
            TarjetaRotacion.Angle = relativoX * -2.5;
            var alejamiento = Math.Min(Math.Abs(relativoX) + Math.Abs(relativoY), 1);
            TarjetaEscala.ScaleX = 1 - alejamiento * 0.03;
            TarjetaEscala.ScaleY = 1 - alejamiento * 0.03;
            TarjetaT.X = relativoX * -14;
            TarjetaT.Y = relativoY * -12;
        }
    }

    private void AlSalirMouse(object sender, MouseEventArgs e)
    {
        const int duracion = 400;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        TarjetaSesgo.BeginAnimation(SkewTransform.AngleXProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(duracion)) { EasingFunction = easing });
        TarjetaSesgo.BeginAnimation(SkewTransform.AngleYProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(duracion)) { EasingFunction = easing });
        TarjetaRotacion.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(duracion)) { EasingFunction = easing });
        TarjetaEscala.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(duracion)) { EasingFunction = easing });
        TarjetaEscala.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(duracion)) { EasingFunction = easing });
        TarjetaT.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(duracion)) { EasingFunction = easing });
        TarjetaT.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(duracion)) { EasingFunction = easing });
    }

    // ============================ UTILIDADES ============================

    private static void AnimarOpacidad(UIElement elemento, double hasta, TimeSpan duracion)
    {
        elemento.BeginAnimation(OpacityProperty, new DoubleAnimation(hasta, duracion)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
