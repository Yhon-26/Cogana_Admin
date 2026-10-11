using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Reflection;
using System.Xml.Linq;
using System.Windows.Markup;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio.ViewModels;
using Cogana.Admin.Escritorio.Vistas;
using Cogana.Admin.Escritorio.Controles;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var cantidad = 0;
        void Verificar(bool condicion, string nombre)
        {
            if (!condicion) throw new InvalidOperationException(nombre);
            cantidad++;
            Console.WriteLine($"Correcto: {nombre}");
        }
        var puntos = new[] { new Point(0, 0), new Point(1, 0), new Point(0, 1), new Point(1, 1), new Point(0.5, 0.5), new Point(0.3, 0.7) };
        var identidad = TransformacionPerspectiva.Crear(472, 580, 0, 0);
        Verificar(puntos.All(p => (identidad.Transform(p) - p).Length < 1e-9), "Plano neutro sin deformación");
        foreach (var giro in new[] { (-5d, -7d), (5d, 7d), (0d, 7d), (5d, 0d), (-5d, 7d) })
        {
            var perspectiva = TransformacionPerspectiva.Crear(472, 580, giro.Item1, giro.Item2);
            Verificar(puntos.All(p => (perspectiva.Inverse.Transform(perspectiva.Transform(p)) - p).Length < 1e-9), "La inversa recupera las coordenadas de entrada");
            Verificar((perspectiva.Transform(new Point(0.5, 0.5)) - new Point(0.5, 0.5)).Length < 1e-9, "El centro de giro permanece estable");
            Verificar(perspectiva.TransformBounds(new Rect(0, 0, 1, 1)).Contains(perspectiva.Transform(new Point(0, 1))), "Límites incluyen las esquinas proyectadas");
            Verificar((perspectiva.Clone().Transform(new Point(0.3, 0.7)) - perspectiva.Transform(new Point(0.3, 0.7))).Length < 1e-9, "Clonar conserva la proyección");
        }
        var lateral = TransformacionPerspectiva.Crear(472, 580, 0, 7);
        var izquierda = (lateral.Transform(new Point(0, 1)) - lateral.Transform(new Point(0, 0))).Length;
        var derecha = (lateral.Transform(new Point(1, 1)) - lateral.Transform(new Point(1, 0))).Length;
        Verificar(Math.Abs(izquierda - derecha) > 0.03, "Perspectiva real: los lados cercanos y lejanos tienen distinta longitud");

        var aplicacion = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // Carga únicamente los recursos; nunca ejecuta App.OnStartup ni crea clientes reales de Supabase.
        XNamespace espacio = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var documento = XDocument.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "RecursosAplicacion.xaml"));
        var diccionario = new XElement(espacio + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            documento.Root!.Element(espacio + "Application.Resources")!.Elements());
        aplicacion.Resources = (ResourceDictionary)XamlReader.Parse(diccionario.ToString());
        var superficie = new Grid { Width = 472, Height = 540, Background = Brushes.Transparent };
        var texto = new TextBox { Text = "Texto de prueba", Name = "CampoPrueba", Margin = new Thickness(24), FontSize = 18, Height = 46 };
        var boton = new Button { Content = "Acción de prueba", Margin = new Thickness(24), Height = 46 };
        var mensaje = new TextBlock { Text = "Puedes seleccionar texto y pulsar el botón.", FontSize = 14, Margin = new Thickness(24) };
        boton.Click += (_, _) => mensaje.Text = "Interacción correcta";
        var contenido = new StackPanel();
        contenido.Children.Add(new TextBlock { Text = "COGANA\nPerspectiva e interacción", FontSize = 28, Foreground = Brushes.White, Margin = new Thickness(24, 32, 24, 18) });
        contenido.Children.Add(texto);
        contenido.Children.Add(boton);
        contenido.Children.Add(mensaje);
        superficie.Children.Add(new Border { Margin = new Thickness(24), Background = new SolidColorBrush(Color.FromRgb(20, 40, 34)),
            CornerRadius = new CornerRadius(26), BorderBrush = Brushes.MediumSeaGreen, BorderThickness = new Thickness(1), Child = contenido });
        var efecto = new EfectoPerspectivaTarjeta { Proyeccion = TransformacionPerspectiva.Crear(472, 540, 5, 7) };
        superficie.Effect = efecto;
        var ventana = new Window { Title = "Verificación visual · Perspectiva Cogana", Width = 700, Height = 680,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = new SolidColorBrush(Color.FromRgb(6, 9, 12)), Content = superficie };
        ventana.Show();
        ventana.UpdateLayout();
        var ubicacionCampo = texto.TransformToAncestor(superficie).Transform(new Point(texto.ActualWidth / 2, texto.ActualHeight / 2));
        var normalizado = new Point(ubicacionCampo.X / superficie.ActualWidth, ubicacionCampo.Y / superficie.ActualHeight);
        var proyectado = efecto.Proyeccion!.Transform(normalizado);
        var impacto = superficie.InputHitTest(new Point(proyectado.X * superficie.ActualWidth, proyectado.Y * superficie.ActualHeight));
        Verificar(impacto is not null, "La superficie proyectada admite hit testing");
        // WPF aplica el mapeo del efecto al convertir coordenadas al ancestro.
        var centroEnVentana = texto.TransformToAncestor(ventana).Transform(new Point(texto.ActualWidth / 2, texto.ActualHeight / 2));
        var elemento = ventana.InputHitTest(centroEnVentana) as DependencyObject;
        while (elemento is not null && elemento != texto) elemento = VisualTreeHelper.GetParent(elemento);
        Verificar(elemento == texto, "El clic visual llega al TextBox nativo a través de EffectMapping");

        var imagen = new RenderTargetBitmap(700, 680, 96, 96, PixelFormats.Pbgra32);
        imagen.Render(ventana);
        var pixeles = new byte[700 * 680 * 4];
        imagen.CopyPixels(pixeles, 700 * 4, 0);
        Verificar(Enumerable.Range(0, 700 * 680).Count(i => pixeles[i * 4 + 1] > 80) > 3000, "El shader dibuja contenido visible");
        var codificador = new PngBitmapEncoder();
        codificador.Frames.Add(BitmapFrame.Create(imagen));
        using (var archivo = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "perspectiva-verificada.png"))) codificador.Save(archivo);
        Console.WriteLine($"{cantidad} verificaciones correctas. Tier gráfico: {RenderCapability.Tier >> 16}.");
        if (args.Contains("--visual"))
        {
            ventana.Closed += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            Dispatcher.Run();
        }
        else
        {
            ventana.Close();
            ProbarEstadosAcceso(Verificar);
            Console.WriteLine($"{cantidad} verificaciones totales correctas, sin conexión a Supabase.");
        }
    }

    private static void Esperar(int milisegundos)
    {
        var marco = new DispatcherFrame();
        var temporizador = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milisegundos) };
        temporizador.Tick += (_, _) => { temporizador.Stop(); marco.Continue = false; };
        temporizador.Start();
        Dispatcher.PushFrame(marco);
    }

    private static void ProbarEstadosAcceso(Action<bool, string> verificar)
    {
        var servicio = new AutenticacionSimulada();
        var vm = new InicioSesionViewModel(servicio) { Correo = "admin@example.test" };
        var acceso = new VentanaInicioSesion { DataContext = vm };
        var confirmaciones = 0;
        acceso.AccesoConfirmado += _ => confirmaciones++;
        acceso.Show();
        Esperar(750);
        var campo = (PasswordBox)acceso.FindName("CampoContrasena");
        var carga = (Grid)acceso.FindName("PantallaCarga");
        var paso = (TextBlock)acceso.FindName("TextoPaso");
        var indicador = (ProgressBar)acceso.FindName("IndicadorAcceso");
        var movimiento = (CheckBox)acceso.FindName("ReducirMovimiento");
        var iniciar = typeof(VentanaInicioSesion).GetMethod("AlIniciarSesion", BindingFlags.Instance | BindingFlags.NonPublic)!;
        movimiento.IsChecked = true;
        acceso.Width = acceso.MinWidth;
        acceso.Height = acceso.MinHeight;
        acceso.UpdateLayout();
        Capturar(acceso, "login-minimo.png");
        var tarjeta = (Grid)acceso.FindName("SuperficiePerspectiva");
        tarjeta.Effect = new EfectoPerspectivaTarjeta { Proyeccion = TransformacionPerspectiva.Crear(tarjeta.ActualWidth, tarjeta.ActualHeight, 5, 7) };
        Capturar(acceso, "login-perspectiva.png");
        tarjeta.Effect = null;
        campo.Password = "ClaveFicticiaDePrueba";
        iniciar.Invoke(acceso, new object[] { acceso, new RoutedEventArgs() });
        Esperar(50);
        verificar(carga.Visibility == Visibility.Visible && confirmaciones == 0, "Una solicitud pendiente no abre el panel");
        verificar(!indicador.IsIndeterminate && paso.Text.Contains("Verificando"), "Movimiento reducido mantiene un estado textual legible");
        Capturar(acceso, "login-carga.png");
        movimiento.IsChecked = false;
        acceso.Activate();
        Esperar(100);
        verificar(indicador.IsIndeterminate == acceso.IsActive, "La elección explícita de movimiento permite progreso indeterminado sin porcentaje ficticio");
        acceso.WindowState = WindowState.Minimized;
        Esperar(80);
        verificar(!indicador.IsIndeterminate && !((LienzoParticulas)acceso.FindName("Lienzo")).Animando, "Minimizar pausa el indicador y las partículas");
        acceso.WindowState = WindowState.Normal;
        acceso.Activate();
        Esperar(80);
        verificar(indicador.IsIndeterminate == acceso.IsActive, "Restaurar conserva el estado pendiente y reanuda el movimiento activo");
        iniciar.Invoke(acceso, new object[] { acceso, new RoutedEventArgs() });
        verificar(servicio.Solicitudes == 1, "No envía dos solicitudes durante la carga");
        servicio.Respuesta.SetResult(ResultadoInicioSesion.Fallido("Credenciales de prueba rechazadas."));
        Esperar(500);
        verificar(carga.Visibility == Visibility.Collapsed && confirmaciones == 0, "Un error vuelve al formulario sin confirmar acceso");
        verificar(campo.Password.Length == 0 && vm.Mensaje.Contains("rechazadas"), "Borra la contraseña y conserva el mensaje de error");
        verificar(paso.Text != "Bienvenido al sistema", "El error nunca muestra bienvenida");
        vm.Correo = "invalido";
        campo.Password = "ClaveFicticiaDePrueba";
        iniciar.Invoke(acceso, new object[] { acceso, new RoutedEventArgs() });
        verificar(servicio.Solicitudes == 1 && carga.Visibility == Visibility.Collapsed, "La validación local no inicia una carga falsa");
        vm.Correo = "admin@example.test";
        servicio.Respuesta = new();
        iniciar.Invoke(acceso, new object[] { acceso, new RoutedEventArgs() });
        servicio.Respuesta.SetResult(ResultadoInicioSesion.Correcto(new SesionUsuario(Guid.NewGuid(), vm.Correo,
            "token-ficticio", "renovacion-ficticia", Guid.NewGuid(), "admin")));
        Esperar(500);
        verificar(confirmaciones == 1 && paso.Text == "Bienvenido al sistema", "Solo una sesión válida completa la transición");
        verificar(!indicador.IsIndeterminate && indicador.Value == 100, "La confirmación termina el indicador sin medir tiempos inventados");
        acceso.Close();
    }

    private static void Capturar(Window ventana, string nombre)
    {
        var imagen = new RenderTargetBitmap((int)ventana.ActualWidth, (int)ventana.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        imagen.Render(ventana);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(imagen));
        using var archivo = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, nombre));
        png.Save(archivo);
    }

    private sealed class AutenticacionSimulada : IServicioAutenticacion
    {
        public TaskCompletionSource<ResultadoInicioSesion> Respuesta { get; set; } = new();
        public int Solicitudes { get; private set; }
        public Task<ResultadoInicioSesion> IniciarSesionAsync(string correo, string contrasena, CancellationToken cancellationToken = default)
        { Solicitudes++; return Respuesta.Task; }
        public Task CerrarSesionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void CancelarCambioContrasena() { }
        public Task<ResultadoOperacionAcceso> SolicitarRecuperacionAsync(string correo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ResultadoOperacionAcceso> VerificarRecuperacionAsync(string correo, string enlaceOCodigo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ResultadoOperacionAcceso> CambiarContrasenaAsync(string contrasena, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
