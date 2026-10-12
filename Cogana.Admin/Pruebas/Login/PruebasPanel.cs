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
        vm.ModuloSeleccionado=vm.Modulos.Single(m=>m.Nombre=="Productos"); Esperar();
        verificar(vm.EsModuloProductos && !vm.PuedeVerImagen,"Ver imagen requiere seleccionar un producto");
        vm.FilaModuloSeleccionada=new(Guid.NewGuid(),"SKU","Producto","","","","Activo");
        verificar(vm.PuedeVerImagen,"Seleccionar un producto habilita Ver imagen");
        vm.ModuloSeleccionado=vm.Modulos.Single(m=>m.Nombre=="Categorías");
        verificar(!vm.EsModuloProductos && !vm.PuedeVerImagen,"Otros módulos no ofrecen la foto de producto");
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
        var anchoExpandido=menu.ActualWidth;
        ventana.EsMenuCompacto=true;
        verificar(!SystemParameters.ClientAreaAnimation || ventana.HasAnimatedProperties,
            "Contraer utiliza una transición de ancho cuando Windows permite animaciones");
        ventana.UpdateLayout(); Esperar(70);
        var anchoIntermedio=ventana.AnchoMenu;
        ventana.EsMenuCompacto=false;
        verificar(Math.Abs(ventana.AnchoMenu-anchoIntermedio)<2,
            "Invertir una transición parte del ancho actual sin saltos");
        ventana.EsMenuCompacto=true; Esperar(350); ventana.UpdateLayout();
        verificar(Math.Abs(ventana.AnchoMenu-104)<1 && !ventana.HasAnimatedProperties,
            "Alternar rápidamente termina compacto y libera el reloj");
        verificar(menu.ActualWidth<anchoExpandido-100 && menu.IsVisible, "El menú compacto recupera espacio y conserva la navegación");
        verificar(((Button)ventana.FindName("BotonCerrarSesion")).IsVisible, "Cerrar sesión permanece disponible en el menú compacto");
        Capturar(ventana,"panel-compacto.png");
        ventana.EsMenuCompacto=false; ventana.Width=1180; Esperar(350); ventana.UpdateLayout();
        verificar(Math.Abs(ventana.AnchoMenu-252)<1,"Expandir recupera el ancho completo");
        ProbarEfecto(ventana,menu,verificar);
        verificar(menu.ActualWidth>100, "El menú puede volver a mostrar los nombres");
        var barra=(FrameworkElement)ventana.FindName("BarraEstado");
        verificar(barra.TransformToAncestor(ventana).Transform(new Point()).Y>ventana.ActualHeight-100,
            "La barra de estado permanece en el borde inferior");
        foreach(var tarjeta in vm.WidgetsInicio.ToArray()) vm.QuitarWidget(tarjeta);
        ventana.UpdateLayout(); Esperar();
        var restaurar=(Button)ventana.FindName("BotonRestaurarPanel");
        verificar(restaurar.IsVisible, "El panel vacío ofrece restaurar las tarjetas iniciales");
        Capturar(ventana,"panel-vacio.png");
        var vmVacio=new VentanaPrincipalViewModel(new ServicioEstadoBackend(cliente),new ServicioResumenInicioSupabase(cliente),
            new ServicioModulosAdministrativosSupabase(cliente),new ServicioReportesAdministrativosSupabase(cliente),
            new ServicioActualizacionesVelopack(),almacen,new(Guid.NewGuid(),"cuenta@example.test","","",Guid.NewGuid(),"admin"));
        verificar(vmVacio.WidgetsInicio.Count==0, "Reabrir respeta un panel guardado vacío");
        restaurar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); ventana.UpdateLayout(); Esperar();
        verificar(vm.WidgetsInicio.Select(w=>w.Nombre).SequenceEqual(new[]{"Pedidos","Productos","Inventario y lotes"}) && almacen.Nombres.Count==3,
            "Restaurar recupera y guarda las tres tarjetas iniciales");
        vm.RestaurarPanelInicial(); verificar(vm.WidgetsInicio.Count==3,"Restaurar no duplica tarjetas");
        ProbarDistribucion(verificar);
        Capturar(ventana,"panel-minimo.png");
        ventana.Width=1920; ventana.Height=1000; ventana.UpdateLayout(); Esperar();
        Capturar(ventana,"panel-amplio.png");
        ventana.Width=1440; ventana.Height=850; ventana.UpdateLayout(); Esperar();
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
    private static void ProbarDistribucion(Action<bool,string> verificar)
    {
        var tablero=new Cogana.Admin.Escritorio.Controles.TableroAdaptable();
        for(int i=0;i<4;i++) tablero.Children.Add(new Border{Height=80});
        tablero.Measure(new Size(1400,double.PositiveInfinity)); tablero.Arrange(new Rect(0,0,1400,80));
        verificar(tablero.Children.Cast<FrameworkElement>().All(c=>Math.Abs(c.ActualWidth-350)<1), "Cuatro tarjetas aprovechan el ancho de una fila amplia");
        tablero.Measure(new Size(900,double.PositiveInfinity)); tablero.Arrange(new Rect(0,0,900,160));
        verificar(tablero.Children.Cast<FrameworkElement>().All(c=>Math.Abs(c.ActualWidth-450)<1) && tablero.DesiredSize.Height==160,
            "Al reducir el ancho las tarjetas pasan a dos columnas sin recortarse");
        tablero.Measure(new Size(300,double.PositiveInfinity)); tablero.Arrange(new Rect(0,0,300,320));
        verificar(tablero.DesiredSize.Height==320, "Una columna mantiene el contenido en espacios estrechos");
    }
    private static void Capturar(Window ventana,string nombre)
    {
        var bitmap=new RenderTargetBitmap((int)ventana.ActualWidth,(int)ventana.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(ventana); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var archivo=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,nombre)); png.Save(archivo);
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
    private static void ProbarEfecto(MainWindow ventana,ListBox menu,Action<bool,string> verificar)
    {
        var opcion=(ListBoxItem)menu.ItemContainerGenerator.ContainerFromIndex(0);
        opcion.ApplyTemplate();
        var efecto=(Cogana.Admin.Escritorio.Controles.EfectoSeleccionMenu)opcion.Template.FindName("EfectoSeleccion",opcion);
        efecto.Iniciar(new Point(18,20),true); Esperar(100);
        verificar(efecto.Tiempo>0 && !efecto.IsHitTestVisible,"El efecto avanza sin interceptar clics o arrastres");
        efecto.Iniciar(new Point(60,20),true);
        verificar(efecto.Origen==new Point(60,20) && efecto.Tiempo<.1,"Un clic repetido reinicia la onda desde el nuevo punto");
        efecto.Detener(); efecto.Tiempo=.2; Capturar(ventana,"menu-onda.png"); efecto.Detener();
        efecto.Iniciar(new Point(18,20),false);
        verificar(efecto.Tiempo<0 && !efecto.HasAnimatedProperties,"Sin animaciones de Windows la selección permanece inmediata");
        efecto.Iniciar(new Point(18,20),true);
        menu.SelectedIndex=1;
        verificar(!efecto.HasAnimatedProperties,"Cambiar de módulo cancela el efecto de la opción anterior");
        menu.SelectedIndex=0;
        efecto.Iniciar(new Point(18,20),true); Esperar(1300);
        verificar(efecto.Tiempo<0 && !efecto.HasAnimatedProperties,"El efecto concluye y libera su reloj");
        efecto.Iniciar(new Point(18,20),true); efecto.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        verificar(!efecto.HasAnimatedProperties && efecto.Tiempo<0,"Descargar la opción cancela cualquier efecto pendiente");
    }
    private static void Esperar(int milisegundos=100)
    {
        var marco=new DispatcherFrame(); var reloj=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(milisegundos)};
        reloj.Tick+=(_,_)=>{reloj.Stop();marco.Continue=false;}; reloj.Start(); Dispatcher.PushFrame(marco);
    }
    private static IEnumerable<DependencyObject> Descendientes(DependencyObject raiz)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(raiz);i++)
        { var hijo=VisualTreeHelper.GetChild(raiz,i); yield return hijo; foreach(var descendiente in Descendientes(hijo)) yield return descendiente; }
    }
    private sealed class AlmacenPanel : IServicioPanelInicio
    {
        public bool TieneConfiguracionGuardada => true;
        public IReadOnlyList<string> Nombres {get;private set;} = ["Pedidos","Productos","Inventario y lotes"];
        public IReadOnlyList<string> Cargar()=>Nombres;
        public void Guardar(IReadOnlyList<string> nombres)=>Nombres=nombres.ToArray();
    }
}

