using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Diagnostics;
using System.ComponentModel;
using Cogana.Admin.Escritorio.Servicios;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio.Controles;
using Cogana.Admin.Escritorio.ViewModels;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaInicioSesion : Window
{
    private static readonly Brush BrochaBotonNormal = new LinearGradientBrush(
        Color.FromRgb(0x16, 0x16, 0x16), Color.FromRgb(0x2B, 0x2B, 0x2B), 0);

    private static readonly Brush BrochaBotonActivo = new LinearGradientBrush(
        Color.FromRgb(0x25, 0x25, 0x25), Color.FromRgb(0x16, 0x16, 0x16), 45);

    private bool _cargando;
    private bool _cerrada;
    private bool _renderizando;
    private readonly ICuentaRecordada? _cuentaRecordada;
    private bool _restaurandoCuenta;
    private string? _correoRestaurado;
    private double _giroX, _giroY, _objetivoX, _objetivoY;
    private long _ultimoFotograma, _escrituraHasta;
    private readonly EfectoPerspectivaTarjeta? _perspectiva;
    private InicioSesionViewModel? _viewModel;
    private SesionUsuario? _sesionConfirmada;
    public event Action<SesionUsuario>? AccesoConfirmado;

    public VentanaInicioSesion() : this(null) { }

    public VentanaInicioSesion(ICuentaRecordada? cuentaRecordada)
    {
        _cuentaRecordada = cuentaRecordada;
        InitializeComponent();
        Loaded += AlCargarVentana;
        if (RenderCapability.IsPixelShaderVersionSupported(2, 0)) _perspectiva = new EfectoPerspectivaTarjeta();
        PreviewKeyDown += AlUsarTeclado;
        Activated += (_, _) => { if (IsLoaded) ActualizarMovimiento(); };
        Deactivated += (_, _) => { if (IsLoaded) ActualizarMovimiento(); };
        StateChanged += (_, _) => { if (IsLoaded) ActualizarMovimiento(); };

        BotonInicioSesion.Background = BrochaBotonNormal;
        BotonInicioSesion.Foreground = Brushes.White;

    }

    private void AlCargarVentana(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargarVentana;
        _viewModel = DataContext as InicioSesionViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CambioContrasenaRequerido += AlRequerirCambioContrasena;
            _viewModel.SesionIniciada += AlConfirmarSesion;
        }
        RestaurarCuenta();
        if (CampoContrasena.Password.Length > 0) BotonInicioSesion.Focus();
        else CampoCorreo.Focus();
        ActualizarMovimiento();
        (Resources["AnimacionEntrada"] as Storyboard)?.Begin(this, true);
    }

    private void AlConfirmarSesion(SesionUsuario sesion) => _sesionConfirmada = sesion;

    private async Task AlRequerirCambioContrasena()
    {
        OlvidarCuenta();
        await VolverALoginAsync();
        if (_cerrada) return;
        if (DataContext is InicioSesionViewModel viewModel)
        {
            new VentanaRecuperacionContrasena(viewModel.ServicioAutenticacion, viewModel.Correo, true)
                { Owner = this }.ShowDialog();
            CampoContrasena.Clear();
        }
    }

    private void AlRecuperarContrasena(object sender, RoutedEventArgs e)
    {
        if (_cargando || DataContext is not InicioSesionViewModel viewModel) return;
        OlvidarCuenta();
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
        if (!viewModel.ValidarAcceso(CampoContrasena.Password)) return;
        var contrasenaIntento = CampoContrasena.Password;

        _cargando = true;
        _sesionConfirmada = null;
        BotonInicioSesion.Content = "Accediendo...";
        BotonInicioSesion.Background = BrochaBotonActivo;
        BotonInicioSesion.Foreground = new SolidColorBrush(Colors.White);

        MostrarPantallaDeCarga();

        try
        {
            await viewModel.IniciarSesionAsync(contrasenaIntento);
            if (_cerrada) return;
            if (_sesionConfirmada is not null)
            {
                IndicadorAcceso.IsIndeterminate = false;
                IndicadorAcceso.Value = 100;
                TextoEstadoAcceso.Text = "ACCESO CONFIRMADO";
                TextoPaso.Text = "Bienvenido al sistema";
                var recordada = GuardarCuentaConfirmada(_sesionConfirmada.Correo, contrasenaIntento);
                if (!recordada) TextoPaso.Text = "Acceso correcto. Windows no pudo recordar la cuenta.";
                await Task.Delay(recordada ? 350 : 1500);
                if (!_cerrada) AccesoConfirmado?.Invoke(_sesionConfirmada);
            }
            else if (_cargando) await VolverALoginAsync();
        }
        finally
        {
            CampoContrasena.Clear();
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
        TextoEstadoAcceso.Text = "CONEXIÓN SEGURA";
        TextoPaso.Text = "Verificando tu acceso administrativo...";
        IndicadorAcceso.IsIndeterminate = true;
        IndicadorAcceso.Value = 0;
        RestablecerPerspectiva();
        ActualizarMovimiento();
        AnimarOpacidad(PantallaCarga, 1, TimeSpan.FromMilliseconds(220));
        AnimarOpacidad(TarjetaAcceso, 0, TimeSpan.FromMilliseconds(180));
        PantallaLogin.IsHitTestVisible = false;

        ContenidoCargaT.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(600))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        AnimarOpacidad(ContenidoCarga, 1, TimeSpan.FromMilliseconds(600));
    }

    private async Task VolverALoginAsync()
    {
        Lienzo.Animando = false;
        (Resources["DecoracionCarga"] as Storyboard)?.Remove(this);
        AnimarOpacidad(PantallaCarga, 0, TimeSpan.FromMilliseconds(180));
        AnimarOpacidad(TarjetaAcceso, 1, TimeSpan.FromMilliseconds(220));
        await Task.Delay(220);
        if (_cerrada) return;
        PantallaCarga.Visibility = Visibility.Collapsed;
        PantallaLogin.IsHitTestVisible = true;
        BotonInicioSesion.Content = "ENTRAR AL PANEL";
        BotonInicioSesion.Background = BrochaBotonNormal;
        BotonInicioSesion.Foreground = Brushes.White;
        _cargando = false;
        ActualizarMovimiento();
        CampoContrasena.Focus();
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
    private void AlCambiarCampoTexto(object sender, TextChangedEventArgs e)
    {
        if (!_restaurandoCuenta && _correoRestaurado is not null &&
            !string.Equals(CampoCorreo.Text.Trim(), _correoRestaurado, StringComparison.OrdinalIgnoreCase))
        {
            _correoRestaurado = null;
            CampoContrasena.Clear();
        }
        ActualizarCampo(sender);
    }

    private void RestaurarCuenta()
    {
        RecordarCuenta.IsEnabled = _cuentaRecordada is not null;
        if (_cuentaRecordada is null || _viewModel is null) return;
        _restaurandoCuenta = true;
        try
        {
            var cuenta = _cuentaRecordada.Leer();
            if (cuenta is null) return;
            _correoRestaurado = cuenta.Correo;
            _viewModel.Correo = cuenta.Correo;
            CampoContrasena.Password = cuenta.Contrasena;
            RecordarCuenta.IsChecked = true;
        }
        catch (Win32Exception) { AvisoCuenta.Text = "Windows no pudo cargar la cuenta recordada."; }
        finally { _restaurandoCuenta = false; }
    }

    private bool GuardarCuentaConfirmada(string correo, string contrasena)
    {
        if (_cuentaRecordada is null || RecordarCuenta.IsChecked != true) return true;
        try { _cuentaRecordada.Guardar(correo, contrasena); return true; }
        catch (Exception error) when (error is Win32Exception or ArgumentException) { return false; }
    }

    private void AlDesmarcarRecordarCuenta(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _restaurandoCuenta) return;
        OlvidarCuenta();
    }

    private void OlvidarCuenta()
    {
        try
        {
            _cuentaRecordada?.Olvidar();
            _correoRestaurado = null;
            _restaurandoCuenta = true;
            RecordarCuenta.IsChecked = false;
            _restaurandoCuenta = false;
            AvisoCuenta.Text = string.Empty;
        }
        catch (Win32Exception)
        {
            _restaurandoCuenta = true;
            RecordarCuenta.IsChecked = true;
            _restaurandoCuenta = false;
            AvisoCuenta.Text = "Windows no pudo eliminar la cuenta recordada. Inténtalo de nuevo.";
        }
    }

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

    private void AnimarCampo(
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
            ? Color.FromRgb(0x16, 0x16, 0x16)
            : activo
                ? Color.FromRgb(0x55, 0x55, 0x55)
                : Color.FromRgb(0x70, 0x70, 0x70);
        etiqueta.Foreground = new SolidColorBrush(color);

        icono.Stroke = new SolidColorBrush(enfocado
            ? Color.FromRgb(0x25, 0x25, 0x25)
            : Color.FromRgb(0x80, 0x80, 0x80));

        fondo.BorderBrush = new SolidColorBrush(enfocado
            ? Color.FromRgb(0x77, 0x77, 0x77)
            : Color.FromRgb(0xDD, 0xDD, 0xDD));
        fondo.Background = new SolidColorBrush(enfocado
            ? Color.FromRgb(0xFF, 0xFF, 0xFF)
            : Color.FromRgb(0xF7, 0xF7, 0xF7));

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
        if (_cargando || !IsActive || RaizLogin.ActualWidth <= 0 || RaizLogin.ActualHeight <= 0) return;
        var posicion = e.GetPosition(RaizLogin);
        FocoMouseT.X = posicion.X - RaizLogin.ActualWidth / 2;
        FocoMouseT.Y = posicion.Y - RaizLogin.ActualHeight / 2;
        // Al entrar en los campos, la tarjeta vuelve al plano para que escribir y seleccionar sea estable.
        var interactuando = TarjetaAcceso.IsMouseOver || Stopwatch.GetTimestamp() < _escrituraHasta;
        _objetivoY = interactuando ? 0 : Math.Clamp(posicion.X / RaizLogin.ActualWidth - 0.5, -0.5, 0.5) * 14;
        _objetivoX = interactuando ? 0 : -Math.Clamp(posicion.Y / RaizLogin.ActualHeight - 0.5, -0.5, 0.5) * 10;
        IniciarSeguimiento();
    }

    private void AlSalirMouse(object sender, MouseEventArgs e)
    {
        _objetivoX = _objetivoY = 0;
        IniciarSeguimiento();
    }

    private void AlUsarTeclado(object sender, KeyEventArgs e)
    {
        _escrituraHasta = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
        _objetivoX = _objetivoY = 0;
        IniciarSeguimiento();
    }

    private void IniciarSeguimiento()
    {
        if (_renderizando || _perspectiva is null || _cerrada || !IsActive) return;
        _ultimoFotograma = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += AlRenderizarMovimiento;
        _renderizando = true;
    }

    private void AlRenderizarMovimiento(object? sender, EventArgs e)
    {
        var ahora = Stopwatch.GetTimestamp();
        var segundos = Math.Clamp((ahora - _ultimoFotograma) / (double)Stopwatch.Frequency, 0, 0.05);
        _ultimoFotograma = ahora;
        var suavizado = 1 - Math.Exp(-segundos / 0.12);
        _giroX += (_objetivoX - _giroX) * suavizado;
        _giroY += (_objetivoY - _giroY) * suavizado;
        if (SuperficiePerspectiva.ActualWidth > 0 && SuperficiePerspectiva.ActualHeight > 0 && _perspectiva is not null)
        {
            _perspectiva.Proyeccion = TransformacionPerspectiva.Crear(SuperficiePerspectiva.ActualWidth,
                SuperficiePerspectiva.ActualHeight, _giroX, _giroY);
            SuperficiePerspectiva.Effect = Math.Abs(_giroX) + Math.Abs(_giroY) < 0.02 ? null : _perspectiva;
        }
        if (Math.Abs(_objetivoX - _giroX) + Math.Abs(_objetivoY - _giroY) < 0.01) DetenerSeguimiento();
    }

    private void DetenerSeguimiento()
    {
        CompositionTarget.Rendering -= AlRenderizarMovimiento;
        _renderizando = false;
    }

    private void RestablecerPerspectiva()
    {
        DetenerSeguimiento();
        _giroX = _giroY = _objetivoX = _objetivoY = 0;
        SuperficiePerspectiva.Effect = null;
    }

    private void ActualizarMovimiento()
    {
        var animar = IsActive && WindowState != WindowState.Minimized;
        foreach (var clave in new[] { "AnimacionAurora", "AnimacionBrilloTarjeta", "AnimacionVida" })
        {
            var historia = (Storyboard)Resources[clave];
            if (animar && !_cargando) historia.Begin(this, true);
            else historia.Remove(this);
        }
        var carga = (Storyboard)Resources["DecoracionCarga"];
        if (animar && _cargando) carga.Begin(this, true);
        else carga.Remove(this);
        Lienzo.Animando = animar && _cargando;
        IndicadorAcceso.IsIndeterminate = animar && _cargando && _sesionConfirmada is null;
        if (!animar)
        {
            RestablecerPerspectiva();
            (Resources["AnimacionEntrada"] as Storyboard)?.Remove(this);
            TarjetaAcceso.Opacity = _cargando ? 0 : 1;
            TarjetaT.Y = 0;
            TarjetaEscala.ScaleX = TarjetaEscala.ScaleY = 1;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cerrada = true;
        DetenerSeguimiento();
        Lienzo.Animando = false;
        if (_viewModel is not null)
        {
            _viewModel.SesionIniciada -= AlConfirmarSesion;
            _viewModel.CambioContrasenaRequerido -= AlRequerirCambioContrasena;
        }
        foreach (var clave in new[] { "AnimacionAurora", "AnimacionBrilloTarjeta", "AnimacionVida", "AnimacionEntrada", "DecoracionCarga" })
            (Resources[clave] as Storyboard)?.Remove(this);
        base.OnClosed(e);
    }

    // ============================ UTILIDADES ============================

    private void AnimarOpacidad(UIElement elemento, double hasta, TimeSpan duracion)
    {
        elemento.BeginAnimation(OpacityProperty, new DoubleAnimation(hasta, duracion)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
