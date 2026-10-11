using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio;
using Cogana.Admin.Escritorio.ViewModels;
using Cogana.Admin.Infraestructura.Servicios;

internal static class PruebasPanel
{
    public static void Probar(Action<bool,string> verificar, bool visual)
    {
        using var cliente = new ClienteSupabaseRest(new(null, string.Empty));
        var almacen = new AlmacenPanel();
        var vm = new VentanaPrincipalViewModel(new ServicioEstadoBackend(cliente),
            new ServicioResumenInicioSupabase(cliente), new ServicioModulosAdministrativosSupabase(cliente),
            new ServicioReportesAdministrativosSupabase(cliente), new ServicioActualizacionesVelopack(),
            almacen, new(Guid.NewGuid(), "cuenta@example.test", "", "", Guid.NewGuid(), "owner"));
        var ventana = new MainWindow { DataContext = vm, Title = "Prueba aislada · Panel Cogana" };
        ventana.Show(); ventana.UpdateLayout(); Esperar();
        var textos = Descendientes(ventana).OfType<TextBlock>().Select(t=>t.Text).ToList();
        verificar(!textos.Contains("Secciones del panel") && !textos.Any(t=>t.Contains("bandeja")), "El panel elimina la bandeja duplicada");
        var menu = Descendientes(ventana).OfType<ListBox>().Single(l=>l.ItemsSource == vm.Modulos);
        var promocion = (ListBoxItem)menu.ItemContainerGenerator.ContainerFromItem(vm.Modulos.Single(m=>m.Nombre=="Promociones"));
        verificar(promocion.Style.Setters.OfType<EventSetter>().Count()==4, "El menú izquierdo conecta los eventos de clic y arrastre");
        promocion.IsSelected = true; Esperar();
        verificar(vm.ModuloSeleccionado?.Nombre=="Promociones", "La selección del menú abre el módulo completo");
        vm.ModuloSeleccionado=vm.Modulos[0];
        var cantidad=vm.WidgetsInicio.Count;
        vm.AgregarWidgetPorNombre("Promociones");
        verificar(vm.WidgetsInicio.Count==cantidad+1 && almacen.Nombres.Contains("Promociones"), "Agregar desde el menú guarda la tarjeta");
        vm.AgregarWidgetPorNombre("Promociones"); vm.AgregarWidgetPorNombre("Inicio"); vm.AgregarWidgetPorNombre("No existe");
        verificar(vm.WidgetsInicio.Count==cantidad+1, "Descarta duplicados, Inicio y nombres ajenos");
        var widget=vm.WidgetsInicio.Single(w=>w.Nombre=="Promociones");
        vm.MoverWidget(widget,vm.WidgetsInicio[0],false);
        verificar(almacen.Nombres[0]=="Promociones", "Reordenar mantiene la persistencia existente");
        vm.QuitarWidget(widget);
        verificar(!almacen.Nombres.Contains("Promociones"), "Quitar una tarjeta también actualiza la disposición");
        var salida=(Button)ventana.FindName("BotonCerrarSesion");
        verificar(salida.IsVisible && salida.ActualWidth>100, "Cerrar sesión es visible junto a la cuenta");
        var espera = new TaskCompletionSource(); var solicitudes=0;
        ventana.CerrarSesionSolicitado=async()=>{solicitudes++; await espera.Task;};
        salida.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        salida.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        verificar(solicitudes==1 && !ventana.IsEnabled && salida.Content?.ToString()=="Cerrando sesión…", "Cerrar sesión bloquea acciones y pulsaciones repetidas");
        espera.SetResult(); Esperar();
        verificar(ventana.IsEnabled && salida.Content?.ToString()=="Cerrar sesión", "La acción finaliza sin dejar el panel bloqueado");
        ventana.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)ventana.ActualWidth,(int)ventana.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(ventana); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using(var archivo=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"panel-menu-sesion.png"))) png.Save(archivo);
        if(visual) { ventana.Closed+=(_,_)=>Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); Dispatcher.Run(); }
        else
        {
            ventana.Close();
            ProbarRegresoAlLogin(verificar);
        }
    }
    private static void ProbarRegresoAlLogin(Action<bool,string> verificar)
    {
        // Ejecuta el ciclo real de App con configuración vacía y salida HTTP simulada.
        var app=(App)Application.Current;
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(App).GetField("_configuracionSupabase",flags)!.SetValue(app,
            new Cogana.Admin.Infraestructura.Configuracion.ConfiguracionSupabase(null,string.Empty));
        typeof(App).GetMethod("MostrarInicioSesion",flags)!.Invoke(app,null);
        var login=app.MainWindow;
        typeof(App).GetMethod("AbrirPanelPrincipal",flags)!.Invoke(app,
            [new SesionUsuario(Guid.NewGuid(),"cuenta@example.test","","",Guid.NewGuid(),"admin")]);
        var panel=(MainWindow)app.MainWindow;
        verificar(panel.IsVisible && !login.IsVisible, "El ciclo real de App reemplaza el login por el panel");
        using var clienteSalida=new ClienteSupabaseRest(new(new Uri("https://prueba.example.test"),"publica-prueba"),new SalidaSimulada());
        typeof(App).GetField("_servicioAutenticacion",flags)!.SetValue(app,new ServicioAutenticacionSupabase(clienteSalida));
        var cierre=panel.CerrarSesionSolicitado!();
        while(!cierre.IsCompleted) Esperar();
        cierre.GetAwaiter().GetResult();
        verificar(app.MainWindow is Cogana.Admin.Escritorio.Vistas.VentanaInicioSesion && app.MainWindow.IsVisible && !panel.IsVisible,
            "Cerrar sesión vuelve al login real sin cerrar la aplicación");
        var acceso=(InicioSesionViewModel)app.MainWindow.DataContext;
        verificar(string.IsNullOrEmpty(acceso.Correo) && !acceso.EstaProcesando,
            "El nuevo login espera otro ingreso y no reutiliza la identidad anterior");
        app.MainWindow.Close();
    }
    private sealed class SalidaSimulada : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage solicitud,CancellationToken token)
            => Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
    }
    private static void Esperar()
    {
        var marco=new DispatcherFrame(); var reloj=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
        reloj.Tick+=(_,_)=>{reloj.Stop();marco.Continue=false;}; reloj.Start(); Dispatcher.PushFrame(marco);
    }
    private static IEnumerable<DependencyObject> Descendientes(DependencyObject raiz)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(raiz);i++)
        { var hijo=VisualTreeHelper.GetChild(raiz,i); yield return hijo; foreach(var descendiente in Descendientes(hijo)) yield return descendiente; }
    }
    private sealed class AlmacenPanel : IServicioPanelInicio
    {
        public IReadOnlyList<string> Nombres {get;private set;} = ["Pedidos","Productos","Inventario y lotes"];
        public IReadOnlyList<string> Cargar()=>Nombres;
        public void Guardar(IReadOnlyList<string> nombres)=>Nombres=nombres.ToArray();
    }
}

