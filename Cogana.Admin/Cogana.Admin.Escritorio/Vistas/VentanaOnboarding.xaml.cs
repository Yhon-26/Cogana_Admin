using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio.Controles;

namespace Cogana.Admin.Escritorio.Vistas;

/// <summary>
/// Introducción animada que saluda al propietario en su primer ingreso al panel.
/// Cuatro actos con la misma atmósfera del login: aurora, partículas, tarjeta con
/// brillo giratorio y parallax suave. Se muestra una sola vez por equipo; el cierre
/// por cualquier vía (botón, teclado o X) abre el panel normalmente.
/// </summary>
public partial class VentanaOnboarding : Window
{
    private static readonly Brush BrochaPuntoActivo = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16));
    private static readonly Brush BrochaPuntoInactivo = new SolidColorBrush(Color.FromArgb(0x40, 0x16, 0x16, 0x16));

    private readonly Grid[] _paginas;
    private readonly Border[] _puntos;
    private readonly string[] _clavesEntrada =
        ["EntradaPagina1", "EntradaPagina2", "EntradaPagina3", "EntradaPagina4"];
    private int _indice;
    private int _versionNavegacion;
    private bool _cerrada, _completado, _renderizando, _buclesActivos;
    private double _parallaxX, _parallaxY, _objetivoX, _objetivoY;
    private long _ultimoFotograma;

    /// <summary>Se dispara exactamente una vez, sea al terminar o al saltar/cerrar.</summary>
    public event Action? Finalizado;

    public VentanaOnboarding() : this(null) { }

    public VentanaOnboarding(SesionUsuario? sesion)
    {
        InitializeComponent();
        _paginas = [Pagina1, Pagina2, Pagina3, Pagina4];
        _puntos = [Punto1, Punto2, Punto3, Punto4];
        _indice = 0;
        Loaded += AlCargarVentana;
        PreviewKeyDown += AlUsarTeclado;
        Activated += (_, _) => { if (IsLoaded) ActualizarMovimiento(); };
        Deactivated += (_, _) => { if (IsLoaded) ActualizarMovimiento(); };
        StateChanged += (_, _) => { if (IsLoaded) ActualizarMovimiento(); };
    }

    private void AlCargarVentana(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargarVentana;
        ActualizarMovimiento();
        (Resources["AnimacionEntrada"] as Storyboard)?.Begin(this, true);
        // El acto 1 se reproduce en la misma vía diferida que los demás: los
        // storyboards con BeginTime no aplican bien antes del primer render.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            () => ReproducirEntrada(_indice));
        BotonContinuar.Focus();
    }

    // ============================ NAVEGACIÓN ============================

    private void AlAvanzar(object sender, RoutedEventArgs e)
    {
        if (_indice >= _paginas.Length - 1)
        {
            Completar();
            return;
        }
        IrA(_indice + 1);
    }

    private void AlRetroceder(object sender, RoutedEventArgs e) => IrA(_indice - 1);

    private void AlSaltar(object sender, RoutedEventArgs e) => Completar();

    private void AlUsarTeclado(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Completar();
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            AlAvanzar(BotonContinuar, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Left && _indice > 0)
        {
            IrA(_indice - 1);
            e.Handled = true;
        }
    }

    private async void IrA(int destino)
    {
        if (destino < 0 || destino >= _paginas.Length || destino == _indice) return;
        var version = ++_versionNavegacion;
        var origen = _indice;
        var haciaAdelante = destino > origen;
        var paginaSalida = _paginas[origen];
        var salida = TimeSpan.FromMilliseconds(180);

        // Salida: desvanecer y deslizar hacia el lado contrario del avance.
        var transformacionSalida = (TranslateTransform)paginaSalida.RenderTransform;
        transformacionSalida.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(
            haciaAdelante ? -36 : 36, salida)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        paginaSalida.BeginAnimation(OpacityProperty, new DoubleAnimation(0, salida)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        await Task.Delay(salida);
        if (_cerrada || _completado || version != _versionNavegacion) return;

        paginaSalida.Visibility = Visibility.Collapsed;
        // Retirar las animaciones de salida antes de reutilizar la página en otra visita.
        transformacionSalida.BeginAnimation(TranslateTransform.XProperty, null);
        transformacionSalida.X = 0;
        paginaSalida.BeginAnimation(OpacityProperty, null);

        _indice = destino;
        var paginaEntrada = _paginas[destino];
        paginaEntrada.BeginAnimation(OpacityProperty, null);
        paginaEntrada.Opacity = 1;
        paginaEntrada.Visibility = Visibility.Visible;
        var transformacionEntrada = (TranslateTransform)paginaEntrada.RenderTransform;
        transformacionEntrada.BeginAnimation(TranslateTransform.XProperty, null);
        transformacionEntrada.X = haciaAdelante ? 40 : -40;
        transformacionEntrada.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0,
            TimeSpan.FromMilliseconds(380))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        ReproducirEntrada(destino);
        ActualizarPie();
        ActualizarMovimiento();
    }

    private void ReproducirEntrada(int indice)
    {
        // Sin modo controlable: estos storyboards se dejan correr hasta el final.
        (Resources[_clavesEntrada[indice]] as Storyboard)?.Begin(this);
    }

    private void ActualizarPie()
    {
        for (var i = 0; i < _puntos.Length; i++)
        {
            var activo = i == _indice;
            _puntos[i].BeginAnimation(WidthProperty, new DoubleAnimation(activo ? 22 : 8,
                TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            _puntos[i].Background = activo ? BrochaPuntoActivo : BrochaPuntoInactivo;
        }
        BotonAnterior.Visibility = _indice == 0 ? Visibility.Hidden : Visibility.Visible;
        BotonContinuar.Content = _indice == _paginas.Length - 1 ? "ENTRAR A MI PANEL" : "Continuar";
    }

    // ============================ CIERRE ============================

    private async void Completar()
    {
        if (_completado || _cerrada) return;
        _completado = true;
        _versionNavegacion++;
        foreach (var clave in new[] { "AnimacionAurora", "AnimacionBrilloTarjeta", "AnimacionVida", "FlujoSincronia" })
            (Resources[clave] as Storyboard)?.Remove(this);
        (Resources["AnimacionEntrada"] as Storyboard)?.Remove(this);
        Lienzo.Animando = false;

        var salida = TimeSpan.FromMilliseconds(220);
        TarjetaOnboarding.BeginAnimation(OpacityProperty, new DoubleAnimation(0, salida)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        TarjetaT.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-18, salida)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        await Task.Delay(salida);
        if (_cerrada) return;
        // El panel abre antes de cerrar esta ventana para que nunca quede la app sin ventana.
        Finalizado?.Invoke();
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_completado)
        {
            // El usuario cerró la ventana directamente: cuenta como salto y abre el panel.
            _completado = true;
            Finalizado?.Invoke();
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _cerrada = true;
        DetenerSeguimiento();
        Lienzo.Animando = false;
        foreach (var clave in new[]
                 {
                     "AnimacionAurora", "AnimacionBrilloTarjeta", "AnimacionEntrada", "AnimacionVida",
                     "FlujoSincronia", "EntradaPagina1", "EntradaPagina2", "EntradaPagina3", "EntradaPagina4"
                 })
        {
            (Resources[clave] as Storyboard)?.Remove(this);
        }
        base.OnClosed(e);
    }

    // ====================== PARALLAX Y AMBIENTE ======================

    private void AlMoverMouse(object sender, MouseEventArgs e)
    {
        if (RaizOnboarding.ActualWidth <= 0 || RaizOnboarding.ActualHeight <= 0) return;
        var posicion = e.GetPosition(RaizOnboarding);
        // El foco de luz persigue al mouse al instante; la tarjeta lo hace con inercia.
        FocoMouseT.X = posicion.X - RaizOnboarding.ActualWidth / 2;
        FocoMouseT.Y = posicion.Y - RaizOnboarding.ActualHeight / 2;
        _objetivoX = -(posicion.X / RaizOnboarding.ActualWidth - 0.5) * 14;
        _objetivoY = -(posicion.Y / RaizOnboarding.ActualHeight - 0.5) * 10;
        IniciarSeguimiento();
    }

    private void AlSalirMouse(object sender, MouseEventArgs e)
    {
        _objetivoX = _objetivoY = 0;
        IniciarSeguimiento();
    }

    private void IniciarSeguimiento()
    {
        if (_renderizando || _cerrada || _completado) return;
        _ultimoFotograma = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += AlRenderizarMovimiento;
        _renderizando = true;
    }

    private void AlRenderizarMovimiento(object? sender, EventArgs e)
    {
        var ahora = Stopwatch.GetTimestamp();
        var segundos = Math.Clamp((ahora - _ultimoFotograma) / (double)Stopwatch.Frequency, 0, 0.05);
        _ultimoFotograma = ahora;
        var suavizado = 1 - Math.Exp(-segundos / 0.14);
        _parallaxX += (_objetivoX - _parallaxX) * suavizado;
        _parallaxY += (_objetivoY - _parallaxY) * suavizado;
        ParallaxT.X = _parallaxX;
        ParallaxT.Y = _parallaxY;
        if (Math.Abs(_objetivoX - _parallaxX) + Math.Abs(_objetivoY - _parallaxY) < 0.02) DetenerSeguimiento();
    }

    private void DetenerSeguimiento()
    {
        CompositionTarget.Rendering -= AlRenderizarMovimiento;
        _renderizando = false;
    }

    /// <summary>Arranca o detiene los bucles ambientales según el estado de la ventana.
    /// No reinicia los bucles que ya corren para evitar saltos en la deriva.</summary>
    private void ActualizarMovimiento()
    {
        var animar = IsActive && WindowState != WindowState.Minimized && !_completado && !_cerrada;
        if (animar == _buclesActivos)
        {
            GestionarFlujoSincronia();
            return;
        }

        _buclesActivos = animar;
        foreach (var clave in new[] { "AnimacionAurora", "AnimacionBrilloTarjeta", "AnimacionVida" })
        {
            var historia = (Storyboard)Resources[clave];
            if (animar) historia.Begin(this, true);
            else historia.Remove(this);
        }
        Lienzo.Animando = animar;
        if (!animar)
        {
            DetenerSeguimiento();
            TarjetaOnboarding.Opacity = 1;
            TarjetaT.Y = 0;
            TarjetaEscala.ScaleX = TarjetaEscala.ScaleY = 1;
        }
        GestionarFlujoSincronia();
    }

    private void GestionarFlujoSincronia()
    {
        var flujo = (Storyboard)Resources["FlujoSincronia"];
        if (_buclesActivos && _indice == 2 && !_cerrada && !_completado) flujo.Begin(this, true);
        else flujo.Remove(this);
    }
}
