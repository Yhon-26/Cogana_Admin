using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cogana.Admin.Escritorio.Servicios;
using Cogana.Admin.Escritorio.Vistas;

internal static class PruebasOnboarding
{
    public static void Probar(Action<bool, string> verificar, bool visual = false)
    {
        // -------- EstadoPrimerIngreso con archivo aislado --------
        var rutaBandera = Path.Combine(Path.GetTempPath(), $"prueba-onboarding-{Guid.NewGuid():N}.json");
        try
        {
            var estado = new EstadoPrimerIngreso(rutaBandera);
            estado.Cargar();
            verificar(!estado.Visto, "Sin archivo previo la introducción está pendiente");
            estado.MarcarVisto("0.0.0-prueba");
            verificar(estado.Visto, "MarcarVisto activa la marca en memoria");
            var recargado = new EstadoPrimerIngreso(rutaBandera);
            recargado.Cargar();
            verificar(recargado.Visto, "La marca sobrevive a una nueva carga del archivo");
        }
        finally
        {
            if (File.Exists(rutaBandera)) File.Delete(rutaBandera);
        }

        // -------- Ventana: estado inicial --------
        var finalizados = 0;
        var ventana = new VentanaOnboarding { Width = 1080, Height = 720 };
        ventana.Finalizado += () => finalizados++;
        ventana.Show();
        ventana.Activate();
        Esperar(); Esperar();
        ventana.UpdateLayout();

        var pagina1 = (Grid)ventana.FindName("Pagina1")!;
        var pagina2 = (Grid)ventana.FindName("Pagina2")!;
        var pagina3 = (Grid)ventana.FindName("Pagina3")!;
        var pagina4 = (Grid)ventana.FindName("Pagina4")!;
        var continuar = (Button)ventana.FindName("BotonContinuar")!;
        var anterior = (Button)ventana.FindName("BotonAnterior")!;

        verificar(pagina1.Visibility == Visibility.Visible && pagina2.Visibility == Visibility.Collapsed,
            "El acto 1 abre visible y los demás permanecen ocultos");
        verificar((string)continuar.Content! == "Continuar" && anterior.Visibility == Visibility.Hidden,
            "El pie arranca sin botón Anterior y con Continuar");
        var punto1 = (Border)ventana.FindName("Punto1")!;
        var punto2 = (Border)ventana.FindName("Punto2")!;
        verificar(punto1.ActualWidth > punto2.ActualWidth, "El punto activo es la píldora más ancha");
        EsperarEscalonado();
        var tituloDiag = (TextBlock)ventana.FindName("TituloBienvenida")!;
        verificar(tituloDiag.Opacity > 0.9, "La entrada escalonada del acto 1 deja el título visible");
        Capturar(ventana, "onboarding-1-bienvenida.png");

        // -------- Navegación hacia adelante --------
        Invocar(continuar); Esperar(); Esperar();
        verificar(pagina2.Visibility == Visibility.Visible && pagina1.Visibility == Visibility.Collapsed,
            "Continuar pasa al acto 2 y oculta el acto 1");
        verificar(anterior.Visibility == Visibility.Visible, "Desde el acto 2 aparece Anterior");
        EsperarEscalonado();
        Capturar(ventana, "onboarding-2-modulos.png");

        Invocar(continuar); Esperar(); Esperar();
        verificar(pagina3.Visibility == Visibility.Visible, "Continuar pasa al acto de sincronía");
        Invocar(continuar); Esperar(); Esperar();
        verificar(pagina4.Visibility == Visibility.Visible && pagina3.Visibility == Visibility.Collapsed,
            "Continuar pasa al acto final");
        verificar((string)continuar.Content! == "ENTRAR A MI PANEL",
            "En el acto final Continuar se convierte en la entrada al panel");
        EsperarEscalonado();
        Capturar(ventana, "onboarding-4-empezar.png");

        // -------- Navegación hacia atrás --------
        Invocar(anterior); Esperar(); Esperar();
        verificar(pagina3.Visibility == Visibility.Visible && pagina4.Visibility == Visibility.Collapsed,
            "Anterior regresa al acto de sincronía");
        EsperarEscalonado();
        Capturar(ventana, "onboarding-3-sincronia.png");

        // -------- Teclado --------
        Teclar(ventana, Key.Right); Esperar(); Esperar();
        verificar(pagina4.Visibility == Visibility.Visible, "La flecha derecha avanza al acto final");
        Teclar(ventana, Key.Left); Esperar(); Esperar();
        verificar(pagina3.Visibility == Visibility.Visible, "La flecha izquierda retrocede");
        Teclar(ventana, Key.Right); Esperar(); Esperar();

        // -------- Fin: Continuar en el último acto dispara Finalizado y cierra --------
        Invocar(continuar);
        Esperar(); Esperar();
        verificar(finalizados == 1, "Completar dispara Finalizado exactamente una vez");
        verificar(!ventana.IsVisible, "La ventana del onboarding se cierra al completar");

        // -------- Salto con Escape --------
        var salto = new VentanaOnboarding();
        var saltados = 0;
        salto.Finalizado += () => saltados++;
        salto.Show(); Esperar();
        Teclar(salto, Key.Escape); Esperar(); Esperar();
        verificar(saltados == 1 && !salto.IsVisible, "Escape salta la introducción y avisa al anfitrión");

        // -------- Cierre con X cuenta como salto --------
        var porCierre = new VentanaOnboarding();
        var cierres = 0;
        porCierre.Finalizado += () => cierres++;
        porCierre.Show(); Esperar();
        porCierre.Close(); Esperar();
        verificar(cierres == 1, "Cerrar la ventana con X también avisa que se saltó la introducción");

        if (visual)
        {
            var reinicio = new VentanaOnboarding();
            reinicio.Closed += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            reinicio.Show();
            Dispatcher.Run();
        }
    }

    private static void Invocar(Button boton) =>
        ((IInvokeProvider)new ButtonAutomationPeer(boton).GetPattern(PatternInterface.Invoke)).Invoke();

    private static void Teclar(Window ventana, Key tecla)
    {
        var origen = PresentationSource.FromVisual(ventana);
        if (origen is null) throw new InvalidOperationException("La ventana no tiene PresentationSource activo");
        ventana.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, origen, 0, tecla)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });
    }

    private static void Esperar()
    {
        var marco = new DispatcherFrame();
        var reloj = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        reloj.Tick += (_, _) => { reloj.Stop(); marco.Continue = false; };
        reloj.Start();
        Dispatcher.PushFrame(marco);
    }

    /// <summary>Espera lo que tarda el escalonado más lento de entrada (≈1.1 s).</summary>
    private static void EsperarEscalonado()
    {
        for (var i = 0; i < 7; i++) Esperar();
    }

    private static void Capturar(Window ventana, string nombre)
    {
        var mapa = new RenderTargetBitmap((int)ventana.ActualWidth, (int)ventana.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        mapa.Render(ventana);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(mapa));
        using var archivo = File.Create(Path.Combine(AppContext.BaseDirectory, nombre));
        png.Save(archivo);
    }
}
