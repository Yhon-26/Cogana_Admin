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
using Cogana.Admin.Escritorio.Servicios;

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

        var aplicacion = new Cogana.Admin.Escritorio.App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // Carga únicamente los recursos; nunca ejecuta App.OnStartup ni crea clientes reales de Supabase.
        XNamespace espacio = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var documento = XDocument.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "RecursosAplicacion.xaml"));
        var diccionario = new XElement(espacio + "ResourceDictionary",
            documento.Root!.Attributes().Where(a => a.IsNamespaceDeclaration && a.Name.LocalName != "xmlns")
                .Select(a => a.Name.LocalName == "controles" ? new XAttribute(a.Name, a.Value + ";assembly=Cogana.Admin.Escritorio") : a),
            documento.Root!.Element(espacio + "Application.Resources")!.Elements());
        aplicacion.Resources = (ResourceDictionary)XamlReader.Parse(diccionario.ToString());
        if (args.Contains("--marco"))
        {
            PruebasMarco.Probar(Verificar,args.Contains("--visual"));
            Console.WriteLine($"{cantidad} comprobaciones correctas.");
            return;
        }
        if (args.Contains("--skeleton"))
        {
            PruebasSkeleton.Probar(Verificar);
            Console.WriteLine($"{cantidad} comprobaciones de Skeleton correctas.");
            return;
        }
        if (args.Contains("--onboarding"))
        {
            PruebasOnboarding.Probar(Verificar, args.Contains("--visual"));
            Console.WriteLine($"{cantidad} comprobaciones de Onboarding correctas.");
            return;
        }
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
        var estilos = new VentanaInicioSesion();
        var enlacePrueba = new Button { Content = "¿Olvidaste tu contraseña?", FontSize = 12,
            Style = (Style)estilos.Resources["EnlaceRecuperacion"], HorizontalAlignment = HorizontalAlignment.Center };
        contenido.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(8), Margin = new Thickness(24, 8, 24, 24), Child = enlacePrueba });
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
            ProbarAlmacenWindows(Verificar);
            ProbarEstadosAcceso(Verificar);
            ProbarRecuperacion(Verificar);
            PruebasPanel.Probar(Verificar, args.Contains("--panel-visual"));
            PruebasSkeleton.Probar(Verificar);
            PruebasMarco.Probar(Verificar);
            PruebasImagenProducto.Probar(Verificar);
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
        var cuenta = new CuentaSimulada();
        var acceso = new VentanaInicioSesion(cuenta) { DataContext = vm };
        var confirmaciones = 0;
        acceso.AccesoConfirmado += _ => confirmaciones++;
        acceso.Show();
        Esperar(750);
        var campo = (PasswordBox)acceso.FindName("CampoContrasena");
        var carga = (Grid)acceso.FindName("PantallaCarga");
        var paso = (TextBlock)acceso.FindName("TextoPaso");
        var indicador = (ProgressBar)acceso.FindName("IndicadorAcceso");
        var recordar = (CheckBox)acceso.FindName("RecordarCuenta");
        var iniciar = typeof(VentanaInicioSesion).GetMethod("AlIniciarSesion", BindingFlags.Instance | BindingFlags.NonPublic)!;
        verificar(acceso.FindName("ReducirMovimiento") is null, "El login no muestra la opción de reducir movimiento");
        verificar(!recordar.IsChecked.GetValueOrDefault(), "Recordar cuenta comienza desmarcado");
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
        verificar(paso.Text.Contains("Verificando"), "La espera muestra un estado textual real");
        recordar.IsChecked = true;
        acceso.Activate();
        Esperar(100);
        Esperar(300);
        Capturar(acceso, "login-carga.png");
        verificar(indicador.IsIndeterminate == acceso.IsActive, "El movimiento se activa sin un control adicional");
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
        verificar(cuenta.Cuenta is null, "Un acceso rechazado nunca guarda la contraseña");
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
        verificar(cuenta.Cuenta?.Correo == vm.Correo && cuenta.Cuenta.Contrasena == "ClaveFicticiaDePrueba", "Solo el acceso válido y elegido guarda la cuenta");
        acceso.Close();

        var vmRestaurado = new InicioSesionViewModel(new AutenticacionSimulada());
        var siguiente = new VentanaInicioSesion(cuenta) { DataContext = vmRestaurado };
        var aperturas = 0;
        siguiente.AccesoConfirmado += _ => aperturas++;
        siguiente.Show();
        Esperar(750);
        var campoRestaurado = (PasswordBox)siguiente.FindName("CampoContrasena");
        var opcionRestaurada = (CheckBox)siguiente.FindName("RecordarCuenta");
        verificar(vmRestaurado.Correo == "admin@example.test" && campoRestaurado.Password == "ClaveFicticiaDePrueba" && opcionRestaurada.IsChecked == true, "El siguiente ingreso rellena correo y contraseña protegida");
        verificar(aperturas == 0, "Recordar cuenta no inicia sesión ni omite comprobar permisos");
        vmRestaurado.Correo = "otra@example.test";
        Esperar(50);
        verificar(campoRestaurado.Password.Length == 0, "Cambiar de correo elimina la contraseña de la cuenta anterior del formulario");
        opcionRestaurada.IsChecked = false;
        verificar(cuenta.Cuenta is null, "Desmarcar recordar elimina la credencial guardada");
        siguiente.Close();

        var vmSinRecuerdo = new InicioSesionViewModel(servicio) { Correo = "admin@example.test" };
        var sinRecuerdo = new VentanaInicioSesion(cuenta) { DataContext = vmSinRecuerdo };
        sinRecuerdo.Show();
        Esperar(750);
        ((PasswordBox)sinRecuerdo.FindName("CampoContrasena")).Password = "ClaveFicticiaDePrueba";
        servicio.Respuesta = new();
        iniciar.Invoke(sinRecuerdo, new object[] { sinRecuerdo, new RoutedEventArgs() });
        servicio.Respuesta.SetResult(ResultadoInicioSesion.Correcto(new SesionUsuario(Guid.NewGuid(), vmSinRecuerdo.Correo,
            "token-ficticio", "renovacion-ficticia", Guid.NewGuid(), "admin")));
        Esperar(500);
        verificar(cuenta.Cuenta is null, "Ingresar sin marcar recordar no persiste la contraseña");
        sinRecuerdo.Close();

        cuenta.Guardar("admin@example.test", "ClaveFicticiaDePrueba");
        cuenta.RechazarEliminacion = true;
        var errorAlOlvidar = new VentanaInicioSesion(cuenta) { DataContext = new InicioSesionViewModel(servicio) };
        errorAlOlvidar.Show();
        Esperar(750);
        var opcionError = (CheckBox)errorAlOlvidar.FindName("RecordarCuenta");
        opcionError.IsChecked = false;
        verificar(opcionError.IsChecked == true && ((TextBlock)errorAlOlvidar.FindName("AvisoCuenta")).Text.Contains("eliminar"), "Si Windows rechaza eliminar, conserva la opción y muestra el problema");
        errorAlOlvidar.Close();

        cuenta.RechazarEliminacion = false;
        cuenta.Olvidar();
        cuenta.RechazarGuardado = true;
        var vmErrorGuardado = new InicioSesionViewModel(servicio) { Correo = "admin@example.test" };
        var errorAlGuardar = new VentanaInicioSesion(cuenta) { DataContext = vmErrorGuardado };
        var accesosConErrorGuardado = 0;
        errorAlGuardar.AccesoConfirmado += _ => accesosConErrorGuardado++;
        errorAlGuardar.Show();
        Esperar(750);
        ((CheckBox)errorAlGuardar.FindName("RecordarCuenta")).IsChecked = true;
        ((PasswordBox)errorAlGuardar.FindName("CampoContrasena")).Password = "ClaveFicticiaDePrueba";
        servicio.Respuesta = new();
        iniciar.Invoke(errorAlGuardar, new object[] { errorAlGuardar, new RoutedEventArgs() });
        servicio.Respuesta.SetResult(ResultadoInicioSesion.Correcto(new SesionUsuario(Guid.NewGuid(), vmErrorGuardado.Correo,
            "token-ficticio", "renovacion-ficticia", Guid.NewGuid(), "admin")));
        Esperar(1700);
        verificar(accesosConErrorGuardado == 1 && cuenta.Cuenta is null, "Una falla del almacén informa el problema y permite el acceso válido");
        errorAlGuardar.Close();
    }

    private static void ProbarAlmacenWindows(Action<bool, string> verificar)
    {
        // Destino aislado: jamás consulta ni modifica la cuenta real del usuario.
        var destino = "CoganaAdmin:Prueba:" + Guid.NewGuid();
        var almacen = new CuentaRecordadaWindows(destino);
        try
        {
            verificar(almacen.Leer() is null, "Una cuenta no guardada devuelve vacío");
            almacen.Guardar("prueba@example.test", "ClaveFicticia-á漢🙂");
            var leida = new CuentaRecordadaWindows(destino).Leer();
            verificar(leida?.Correo == "prueba@example.test" && leida.Contrasena == "ClaveFicticia-á漢🙂", "Windows conserva la credencial Unicode entre instancias");
            verificar(!leida!.ToString().Contains("ClaveFicticia"), "La representación de la cuenta no expone su contraseña");
            almacen.Guardar("otra@example.test", "OtraClaveFicticia");
            verificar(almacen.Leer()?.Correo == "otra@example.test", "Guardar otra cuenta reemplaza la anterior");
            almacen.Olvidar();
            verificar(almacen.Leer() is null, "Olvidar borra la credencial de Windows");
            almacen.Olvidar();
            verificar(almacen.Leer() is null, "Olvidar una cuenta ya eliminada es seguro");
        }
        finally { almacen.Olvidar(); }
    }

    private sealed class CuentaSimulada : ICuentaRecordada
    {
        public CuentaRecordada? Cuenta { get; private set; }
        public bool RechazarEliminacion { get; set; }
        public bool RechazarGuardado { get; set; }
        public CuentaRecordada? Leer() => Cuenta;
        public void Guardar(string correo, string contrasena)
        {
            if (RechazarGuardado) throw new System.ComponentModel.Win32Exception(5);
            Cuenta = new(correo, contrasena);
        }
        public void Olvidar()
        {
            if (RechazarEliminacion) throw new System.ComponentModel.Win32Exception(5);
            Cuenta = null;
        }
    }

    private static void ProbarRecuperacion(Action<bool, string> verificar)
    {
        var servicio = new RecuperacionSimulada();
        var ventana = new VentanaRecuperacionContrasena(servicio, "admin@example.test");
        void Pulsar(string metodo) => typeof(VentanaRecuperacionContrasena).GetMethod(metodo, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ventana, new object[] { ventana, new RoutedEventArgs() });
        T Campo<T>(string nombre) where T : class => (T)ventana.FindName(nombre);
        ventana.Show();
        Esperar(200);
        verificar(Campo<StackPanel>("PasoCorreo").Visibility == Visibility.Visible && Campo<StackPanel>("PasoRecuperacion").Visibility == Visibility.Collapsed, "Recuperación comienza con una sola acción: solicitar el correo");
        Capturar(ventana, "recuperacion-correo.png");
        ventana.Height = ventana.MinHeight;
        ventana.Width = ventana.MinWidth;
        ventana.UpdateLayout();
        Capturar(ventana, "recuperacion-minimo.png");
        verificar(Campo<Button>("Volver").IsVisible, "El botón para volver permanece visible en el tamaño mínimo");
        ventana.Width = 600;
        ventana.Height = 840;
        Campo<TextBox>("Correo").Text = "";
        Pulsar("AlSolicitar");
        verificar(servicio.Envios == 0 && Campo<TextBlock>("Mensaje").Text.Contains("válido"), "El correo vacío se valida sin fallar ni enviar solicitudes");
        Campo<TextBox>("Correo").Text = "admin@example.test";
        Pulsar("AlSolicitar");
        verificar(!Campo<Button>("Solicitar").IsEnabled && Campo<ProgressBar>("Progreso").Visibility == Visibility.Visible, "Enviar muestra espera y bloquea solicitudes duplicadas");
        Pulsar("AlSolicitar");
        verificar(servicio.Envios == 1, "La recuperación no envía dos correos durante la misma espera");
        servicio.Envio.SetResult(new(true, "Si existe una cuenta, recibirás el correo."));
        Esperar(220);
        verificar(Campo<StackPanel>("PasoRecuperacion").Visibility == Visibility.Visible && !Campo<Button>("Reenviar").IsEnabled, "El envío confirmado muestra verificación y cuenta regresiva de reenvío");
        Capturar(ventana, "recuperacion-enlace.png");
        Pulsar("AlVerificar");
        verificar(servicio.Verificaciones == 0, "Un enlace vacío no llega al servicio");
        Campo<PasswordBox>("EnlaceOCodigo").Password = "enlace-ficticio";
        Pulsar("AlVerificar");
        servicio.Verificacion.SetResult(new(false, "El enlace venció. Solicita uno nuevo."));
        Esperar(100);
        verificar(Campo<StackPanel>("PasoRecuperacion").Visibility == Visibility.Visible && Campo<PasswordBox>("EnlaceOCodigo").Password.Length == 0, "Un enlace vencido conserva el paso y limpia el secreto");
        servicio.Verificacion = new();
        Campo<PasswordBox>("EnlaceOCodigo").Password = "otro-enlace-ficticio";
        Pulsar("AlVerificar");
        servicio.Verificacion.SetResult(new(true, "Acceso verificado."));
        Esperar(220);
        verificar(Campo<StackPanel>("PasoContrasena").Visibility == Visibility.Visible && Campo<StackPanel>("PasoRecuperacion").Visibility == Visibility.Collapsed, "Solo un enlace verificado permite crear la contraseña");
        Capturar(ventana, "recuperacion-contrasena.png");
        Campo<PasswordBox>("NuevaContrasena").Password = "corta";
        Campo<PasswordBox>("Confirmacion").Password = "corta";
        Pulsar("AlGuardar");
        verificar(servicio.Cambios == 0 && Campo<TextBlock>("Mensaje").Text.Contains("10"), "Una contraseña corta se valida antes de guardar");
        Campo<PasswordBox>("NuevaContrasena").Password = "ClaveFicticiaNueva";
        Pulsar("AlGuardar");
        verificar(servicio.Cambios == 0 && Campo<TextBlock>("Mensaje").Text.Contains("coinciden"), "Una confirmación diferente no cambia la contraseña");
        Campo<PasswordBox>("Confirmacion").Password = "ClaveFicticiaNueva";
        verificar(Campo<TextBlock>("ReglaConfirmacion").Text.Contains("✓"), "La coincidencia se muestra mientras se escribe");
        Pulsar("AlGuardar");
        servicio.Cambio.SetResult(new(false, "No se pudo guardar la contraseña."));
        Esperar(100);
        verificar(Campo<StackPanel>("PasoCompletado").Visibility == Visibility.Collapsed && Campo<StackPanel>("PasoContrasena").Visibility == Visibility.Visible, "Un guardado rechazado nunca muestra éxito");
        servicio.Cambio = new();
        Campo<PasswordBox>("NuevaContrasena").Password = "ClaveFicticiaNueva";
        Campo<PasswordBox>("Confirmacion").Password = "ClaveFicticiaNueva";
        Pulsar("AlGuardar");
        servicio.Cambio.SetResult(new(true, "Contraseña guardada."));
        Esperar(220);
        verificar(Campo<StackPanel>("PasoCompletado").Visibility == Visibility.Visible && Campo<PasswordBox>("NuevaContrasena").Password.Length == 0 && Campo<PasswordBox>("Confirmacion").Password.Length == 0, "El guardado confirmado muestra el cierre y elimina las contraseñas de los campos");
        Capturar(ventana, "recuperacion-completada.png");
        ventana.Close();
        verificar(servicio.Cancelaciones == 1, "Cerrar limpia la sesión de recuperación");

        var accesoDirecto = new VentanaRecuperacionContrasena(servicio, "admin@example.test");
        accesoDirecto.Show();
        typeof(VentanaRecuperacionContrasena).GetMethod("AlTengoEnlace", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(accesoDirecto, new object[] { accesoDirecto, new RoutedEventArgs() });
        verificar(((StackPanel)accesoDirecto.FindName("PasoRecuperacion")).Visibility == Visibility.Visible && servicio.Envios == 1, "Un correo ya recibido permite verificar sin solicitar otro");
        accesoDirecto.Close();
        var inicial = new VentanaRecuperacionContrasena(servicio, "admin@example.test", true);
        inicial.Show();
        Esperar(220);
        verificar(((StackPanel)inicial.FindName("PasoContrasena")).Visibility == Visibility.Visible && ((Grid)inicial.FindName("IndicadorPasos")).Visibility == Visibility.Collapsed, "La contraseña temporal abre directamente el cambio obligatorio");
        Capturar(inicial, "recuperacion-inicial.png");
        inicial.Close();
    }

    private sealed class RecuperacionSimulada : IServicioAutenticacion
    {
        public TaskCompletionSource<ResultadoOperacionAcceso> Envio { get; set; } = new();
        public TaskCompletionSource<ResultadoOperacionAcceso> Verificacion { get; set; } = new();
        public TaskCompletionSource<ResultadoOperacionAcceso> Cambio { get; set; } = new();
        public int Envios, Verificaciones, Cambios, Cancelaciones;
        public Task<ResultadoInicioSesion> IniciarSesionAsync(string correo, string contrasena, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CerrarSesionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void CancelarCambioContrasena() => Cancelaciones++;
        public Task<ResultadoOperacionAcceso> SolicitarRecuperacionAsync(string correo, CancellationToken cancellationToken = default) { Envios++; return Envio.Task; }
        public Task<ResultadoOperacionAcceso> VerificarRecuperacionAsync(string correo, string enlaceOCodigo, CancellationToken cancellationToken = default) { Verificaciones++; return Verificacion.Task; }
        public Task<ResultadoOperacionAcceso> CambiarContrasenaAsync(string contrasena, CancellationToken cancellationToken = default) { Cambios++; return Cambio.Task; }
    }

    private static void Capturar(Window ventana, string nombre)
    {
        var contenido = (FrameworkElement)ventana.Content;
        var imagen = new RenderTargetBitmap((int)contenido.ActualWidth, (int)contenido.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        imagen.Render(contenido);
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
