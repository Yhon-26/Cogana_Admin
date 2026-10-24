using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio;
using Cogana.Admin.Escritorio.Controles;
using Cogana.Admin.Escritorio.ViewModels;
using Cogana.Admin.Escritorio.Vistas;
using Cogana.Admin.Infraestructura.Configuracion;
using Cogana.Admin.Infraestructura.Servicios;

internal static class PruebasConsolidacion
{
    public static void Probar(Action<bool,string> verificar)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var tienda=Guid.NewGuid();var pedido=Guid.NewGuid();var servidor=new Backend();
        using var cliente=new ClienteSupabaseRest(new(new Uri("https://prueba.invalid"),"publica-ficticia"),servidor);
        var catalogo=new ServicioCatalogoAdministrativoSupabase(cliente);
        var foto=new ServicioImagenesProducto(cliente,new(new Uri("https://prueba.invalid"),"publica-ficticia"));
        var formulario=new VentanaNuevoRegistro(catalogo,tienda,"Productos",foto);
        formulario.Show();Esperar();
        ((TextBox)formulario.FindName("ProductoNombre")).Text="Producto de prueba";
        ((TextBox)formulario.FindName("ProductoStockMinimo")).Text="0";
        var archivo=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"cogana-foto-"+Guid.NewGuid()+".png");
        System.IO.File.WriteAllBytes(archivo,[0x89,0x50,0x4e,0x47]);
        try
        {
            typeof(VentanaNuevoRegistro).GetField("_rutaImagen",flags)!.SetValue(formulario,archivo);
            var guardar=typeof(VentanaNuevoRegistro).GetMethod("AlGuardar",flags)!;
            var boton=(Button)formulario.FindName("BotonGuardar");
            guardar.Invoke(formulario,[boton,new RoutedEventArgs()]);
            Hasta(()=>servidor.Subidas==1 && boton.IsEnabled);
            guardar.Invoke(formulario,[boton,new RoutedEventArgs()]);
            Hasta(()=>servidor.Subidas==2 && boton.IsEnabled);
            verificar(servidor.Altas==1,"Reintentar una foto desde el formulario no repite el alta del producto");
            verificar(boton.Content.ToString()=="Reintentar imagen" && !((TextBox)formulario.FindName("ProductoNombre")).IsEnabled,
                "El guardado parcial conserva los datos confirmados y ofrece reintentar la foto");
            formulario.Close();
        }
        finally{System.IO.File.Delete(archivo);if(formulario.IsVisible)formulario.Close();}

        var consulta=new ServicioPedidosAdministrativosSupabase(cliente);
        var operaciones=new ServicioOperacionPedidosSupabase(cliente);
        var ventana=new VentanaOperacionPedido(consulta,operaciones,tienda,pedido);
        ventana.Show();Hasta(()=>((Button)ventana.FindName("FinalizarPreparacion")).IsEnabled);
        verificar(((DataGrid)ventana.FindName("Cantidades")).Items.Count==1,"Atención de pedidos carga los artículos del contrato real");
        verificar(!((Button)ventana.FindName("CompletarPedido")).IsEnabled,"No ofrece cierre con código mientras el pedido está en preparación");
        var ids=new List<Guid>();
        Func<Guid,Task<ResultadoOperacion>> fallo=id=>{ids.Add(id);return Task.FromResult(ResultadoOperacion.Fallida("Fallo simulado"));};
        var ejecutar=typeof(VentanaOperacionPedido).GetMethod("EjecutarAsync",flags)!;
        var primero=(Task)ejecutar.Invoke(ventana,["operacion-prueba",fallo])!;Hasta(()=>primero.IsCompleted);primero.GetAwaiter().GetResult();
        var segundo=(Task)ejecutar.Invoke(ventana,["operacion-prueba",fallo])!;Hasta(()=>segundo.IsCompleted);segundo.GetAwaiter().GetResult();
        verificar(ids.Count==2 && ids[0]==ids[1],"La atención conserva el identificador de la misma operación cuando falla y se reintenta");
        servidor.Estado="ready_for_pickup";
        ((Button)ventana.FindName("Actualizar")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Hasta(()=>((Button)ventana.FindName("CompletarPedido")).IsEnabled);
        verificar(!((Button)ventana.FindName("FinalizarPreparacion")).IsEnabled,"La preparación finalizada queda bloqueada al consultar el resultado del servidor");
        verificar(((Button)ventana.FindName("CompletarPedido")).FocusVisualStyle is not null,"Las acciones operativas tienen indicador de foco por teclado");
        ((TabControl)ventana.FindName("Pasos")).SelectedIndex=3;Esperar();Capturar(ventana,"consolidacion-cierre-pedido.png");
        var texto=(TextBlock)ventana.FindName("Resumen");var original=texto.Foreground;
        var contraste=typeof(AccesibilidadVentana).GetMethod("AplicarContraste",BindingFlags.Static|BindingFlags.NonPublic)!;
        contraste.Invoke(null,[ventana,true]);
        verificar(Equals(texto.Foreground,SystemColors.WindowTextBrush),"El alto contraste usa la paleta de texto de Windows");
        Capturar(ventana,"consolidacion-alto-contraste.png");
        contraste.Invoke(null,[ventana,false]);
        verificar(Equals(texto.Foreground,original),"Desactivar alto contraste recupera la paleta anterior");
        ventana.Close();

        var configuracion=new VentanaConfiguracionTienda(new ServicioConfiguracionTiendaSupabase(cliente),tienda,new ServicioOperacionTiendaSupabase(cliente))
            {Width=900,Height=650};
        configuracion.Show();Esperar();
        var pestanas=(TabControl)configuracion.FindName("Pestanas");pestanas.SelectedIndex=pestanas.Items.Count-1;Esperar();
        verificar(((TabItem)pestanas.SelectedItem).Content is PanelOperacionTienda {IsEnabled:true},"Configuración integra destinatarios, estado y reglas de atención con contratos vigentes");
        verificar(((Button)configuracion.FindName("BotonGuardar")).Visibility==Visibility.Collapsed,"Operación y pagos usa el guardado de su sección y oculta el guardado general");
        var panelOperacion=(PanelOperacionTienda)((TabItem)pestanas.SelectedItem).Content;
        var guardarPagos=Controles<Button>(panelOperacion).First(x=>x.Content?.ToString()=="Guardar destinatarios de pago");
        guardarPagos.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Hasta(()=>configuracion.HuboCambios);
        verificar(configuracion.HuboCambios,"Guardar parámetros operativos notifica que el panel debe actualizarse al volver");
        Capturar(configuracion,"consolidacion-configuracion.png");configuracion.Close();

        using var sinConexion=new ClienteSupabaseRest(new(null,string.Empty));
        var modulos=new ServicioModulosAdministrativosSupabase(cliente);
        var vm=new VentanaPrincipalViewModel(new ServicioEstadoBackend(sinConexion),new ServicioResumenInicioSupabase(sinConexion),modulos,
            new ServicioReportesAdministrativosSupabase(sinConexion),new ServicioActualizacionesVelopack(),new PanelVacio(),new(Guid.NewGuid(),"prueba@example.test","","",tienda,"admin"));
        vm.ModuloSeleccionado=vm.Modulos.First(x=>x.Nombre=="Productos");
        var carga=vm.CargarModuloActualAsync();Hasta(()=>carga.IsCompleted);carga.GetAwaiter().GetResult();
        verificar(vm.TotalModulo==301 && vm.PuedePaginaSiguiente,"El panel habilita navegación con el total real del servidor");
        vm.PaginaSiguienteCommand.Execute(null);Hasta(()=>!vm.EstaCargandoModulo);
        verificar(servidor.UltimoOffset==50 && vm.PuedePaginaAnterior,"Siguiente consulta otra página en lugar de filtrar la primera");
        vm.TextoBusqueda="Producto 300";Esperar(450);Hasta(()=>!vm.EstaCargandoModulo);
        verificar(servidor.UltimoOffset==0 && servidor.Busqueda,"Buscar reinicia la paginación y consulta el servidor");
        var panel=new MainWindow{DataContext=vm,Width=900,Height=560};panel.Show();Esperar();
        verificar(panel.ActualWidth<=900 && panel.ActualHeight<=560,"El panel admite un espacio lógico de 900 por 560");
        Capturar(panel,"consolidacion-panel-compacto.png");panel.Close();
        var vmActualizaciones=new VentanaPrincipalViewModel(new ServicioEstadoBackend(sinConexion),new ServicioResumenInicioSupabase(sinConexion),modulos,
            new ServicioReportesAdministrativosSupabase(sinConexion),new ActualizadorFallido(),new PanelVacio(),new(Guid.NewGuid(),"prueba@example.test","","",tienda,"admin"));
        var actualizacion=vmActualizaciones.GestionarActualizacionAsync();Hasta(()=>actualizacion.IsCompleted);actualizacion.GetAwaiter().GetResult();
        verificar(vmActualizaciones.MensajeActualizacion.Contains("No se pudo") && vmActualizaciones.TextoBotonActualizacion=="Buscar actualizaciones",
            "Un fallo del actualizador informa el problema y permite reintentar sin cerrar la aplicación");
        var brocha=(SolidColorBrush)Application.Current.FindResource("BrochaTextoSuave");
        verificar(Contraste(brocha.Color,Color.FromRgb(0xE7,0xE8,0xE5))>=4.5,"El texto secundario supera 4,5 a 1 sobre el fondo del panel");
        var cambio=new VentanaCambioEstadoPedido(consulta,tienda,new(pedido,"1","Cliente","Recojo","Listo","S/ 1","Pagado","ready_for_pickup"));
        verificar(!((ComboBox)cambio.FindName("NuevoEstado")).Items.Cast<OpcionEstadoPedido>().Any(x=>x.Codigo=="completed"),
            "Cambiar estado no ofrece el cierre que el backend exige realizar por código");cambio.Close();
    }

    private static double Contraste(Color a,Color b)
    { double L(Color c){double F(byte v){var x=v/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}return .2126*F(c.R)+.7152*F(c.G)+.0722*F(c.B);}return (Math.Max(L(a),L(b))+.05)/(Math.Min(L(a),L(b))+.05); }
    private static IEnumerable<T> Controles<T>(DependencyObject raiz)where T:DependencyObject
    {if(raiz is T actual)yield return actual;for(var i=0;i<VisualTreeHelper.GetChildrenCount(raiz);i++)foreach(var hijo in Controles<T>(VisualTreeHelper.GetChild(raiz,i)))yield return hijo;}
    private static void Esperar(int ms=100){var marco=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();marco.Continue=false;};timer.Start();Dispatcher.PushFrame(marco);}
    private static void Hasta(Func<bool> condicion){var limite=DateTime.UtcNow.AddSeconds(5);while(!condicion()&&DateTime.UtcNow<limite)Esperar(20);if(!condicion())throw new TimeoutException("La ventana no completó el estado esperado.");}
    private static void Capturar(Window ventana,string nombre){ventana.UpdateLayout();var bitmap=new RenderTargetBitmap((int)ventana.ActualWidth,(int)ventana.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(ventana);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var archivo=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,nombre));png.Save(archivo);}
    private sealed class PanelVacio:IServicioPanelInicio{public bool TieneConfiguracionGuardada=>true;public IReadOnlyList<string>Cargar()=>[];public void Guardar(IReadOnlyList<string> modulos){} }
    private sealed class ActualizadorFallido:IServicioActualizaciones
    {
        public bool EstaInstaladoComoPaquete=>true;public string VersionActual=>"0.0.0";
        public Task<InfoActualizacionDisponible?> BuscarNuevaVersionAsync(CancellationToken token=default)=>throw new HttpRequestException("Sin red de prueba");
        public Task DescargarAsync(InfoActualizacionDisponible version,IProgress<int>? progreso=null,CancellationToken token=default)=>throw new HttpRequestException("Sin red de prueba");
        public void InstalarYReiniciar(InfoActualizacionDisponible version)=>throw new InvalidOperationException("La prueba nunca instala.");
    }
    private sealed class Backend:HttpMessageHandler
    {
        public int Altas,Subidas,UltimoOffset;public bool Busqueda;public string Estado="preparing";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken token)
        {
            var ruta=req.RequestUri!.AbsolutePath;object datos=Array.Empty<object>();var codigo=HttpStatusCode.OK;
            if(ruta=="/rest/v1/products" && req.Method==HttpMethod.Post){Altas++;var body=await req.Content!.ReadFromJsonAsync<JsonElement>(token);datos=new[]{new{id=body.GetProperty("id").GetGuid()}};}
            else if(ruta.StartsWith("/storage/")){Subidas++;codigo=HttpStatusCode.Forbidden;datos=new{message="Subida rechazada de prueba"};}
            else if(ruta=="/rest/v1/products")
            {
                var query=System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);UltimoOffset=int.Parse(query["offset"]??"0");Busqueda=query["or"] is not null;
                datos=new[]{new{id=Guid.NewGuid(),name="Producto 300",sku="SKU",base_unit="gram",is_active=true}};
            }
            else if(ruta=="/rest/v1/orders")datos=new[]{new{id=Guid.NewGuid(),order_number="PRUEBA",source="android",fulfillment_type="pickup",status=Estado,payment_status="paid",payment_method="yape",customer_name="Cliente simulado",customer_phone="000",created_at=DateTimeOffset.UtcNow,final_total_cents=Estado=="preparing"?(long?)null:100}};
            else if(ruta=="/rest/v1/order_items")datos=new[]{new{id=Guid.NewGuid(),product_name_snapshot="Producto simulado",presentation_name_snapshot="Granel",base_unit_snapshot="gram",request_mode="quantity",estimated_quantity=1000,estimated_line_cents=100,substitution_policy="ask"}};
            else if(ruta=="/rest/v1/stores")datos=new[]{new{id=Guid.NewGuid(),name="Tienda de prueba",address="Dirección de prueba",district="Distrito de prueba"}};
            else if(ruta=="/rest/v1/store_settings")datos=new[]{new{display_name="Tienda de prueba",yape_recipient_name="Negocio de prueba",yape_recipient_phone="000000000",plin_recipient_name="Negocio de prueba",plin_recipient_phone="000000000",manual_payment_window_minutes=15}};
            else if(ruta=="/rest/v1/store_operational_state")datos=new[]{new{status="accepting_orders",public_message="Mensaje de prueba"}};
            else if(ruta=="/rest/v1/store_fulfillment_rules")datos=new[]{new{fulfillment_type="pickup",slot_minutes=30,max_orders_per_slot=8,minimum_notice_minutes=45,delivery_staff_count=(int?)null},new{fulfillment_type="delivery",slot_minutes=60,max_orders_per_slot=4,minimum_notice_minutes=90,delivery_staff_count=(int?)2}};
            var respuesta=new HttpResponseMessage(codigo){Content=JsonContent.Create(datos)};
            if(ruta=="/rest/v1/products" && req.Method==HttpMethod.Get)respuesta.Content.Headers.TryAddWithoutValidation("Content-Range","0-0/301");
            return respuesta;
        }
    }
}
