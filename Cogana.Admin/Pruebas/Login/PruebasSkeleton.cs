using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio;
using Cogana.Admin.Escritorio.Controles;
using Cogana.Admin.Escritorio.ViewModels;
using Cogana.Admin.Escritorio.Vistas;
using Cogana.Admin.Infraestructura.Servicios;

internal static class PruebasSkeleton
{
    public static void Probar(Action<bool,string> verificar)
    {
        var resumen=new ResumenPendiente(); var modulos=new ModulosPendientes(); var reportes=new ReportesPendientes();
        var vm=new VentanaPrincipalViewModel(new Backend(),resumen,modulos,reportes,new ServicioActualizacionesVelopack(),new Panel(),
            new(Guid.NewGuid(),"cuenta@example.test","","",Guid.NewGuid(),"owner"));
        var ventana=new MainWindow{DataContext=vm}; ventana.Show(); Esperar(); ventana.UpdateLayout();
        var skeleton=(SkeletonCarga)ventana.FindName("SkeletonResumen");
        verificar(vm.EstaCargandoInicio&&skeleton.IsVisible,"Inicio muestra skeleton durante la consulta real pendiente");
        verificar(!((FrameworkElement)ventana.FindName("ResumenReal")).IsVisible,"No presenta ceros como resumen cargado durante la espera");
        verificar(((SkeletonCarga)ventana.FindName("SkeletonPanelVacio")).IsVisible,"Un panel vacío conserva su estructura con marcadores de carga");
        Capturar(ventana,"skeleton-inicio.png");
        var carpeta=System.IO.Path.Combine(AppContext.BaseDirectory,"skeleton-animado");System.IO.Directory.CreateDirectory(carpeta);
        for(var i=0;i<32;i++){Esperar(60);Capturar(ventana,$"skeleton-animado/cuadro-{i:D3}.png");}
        var reloj=skeleton.Tiempo; Esperar();
        verificar(!SystemParameters.ClientAreaAnimation||skeleton.Tiempo>reloj,"El barrido avanza mientras la consulta sigue pendiente");
        ventana.WindowState=WindowState.Minimized; Esperar();
        verificar(!skeleton.HasAnimatedProperties,"Minimizar detiene el barrido y libera el reloj");
        ventana.WindowState=WindowState.Normal; Esperar();
        verificar(!SystemParameters.ClientAreaAnimation||skeleton.HasAnimatedProperties,"Restaurar reanuda solo la carga activa");
        resumen.Fuente.SetResult(ResultadoResumenInicio.Correcto(new("Tienda prueba","Distrito","closed",null,7,2,0,0,1,[],DateTimeOffset.Now)));
        Esperar(); verificar(!skeleton.IsVisible&&!vm.EstaCargandoInicio&&vm.ProductosActivos==7,"La respuesta muestra datos reales y termina el skeleton sin espera artificial");
        vm.AgregarWidgetPorNombre("Productos"); Esperar();
        verificar(vm.WidgetsInicio[0].EstaCargando,"Agregar una tarjeta consulta sus datos antes de mostrar contenido");
        modulos.Solicitudes.Last().Fuente.SetResult(Datos("PRODUCTO")); Esperar();
        verificar(!vm.WidgetsInicio[0].EstaCargando&&vm.WidgetsInicio[0].Filas.Count==1,"La tarjeta finaliza la carga al recibir sus filas");
        vm.ModuloSeleccionado=vm.Modulos.Single(m=>m.Nombre=="Productos"); Esperar();
        var anterior=modulos.Solicitudes.Last();
        var tabla=(SkeletonCarga)ventana.FindName("SkeletonModulo");
        verificar(tabla.IsVisible&&!((FrameworkElement)ventana.FindName("ContenidoModulo")).IsVisible,"Un módulo pendiente oculta filas y estado vacío con skeleton de tabla");
        Capturar(ventana,"skeleton-productos.png");
        vm.ModuloSeleccionado=vm.Modulos.Single(m=>m.Nombre=="Categorías"); Esperar();
        var actual=modulos.Solicitudes.Last();
        anterior.Fuente.SetResult(Datos("ANTERIOR")); Esperar();
        verificar(vm.EstaCargandoModulo&&vm.FilasModulo.Count==0&&tabla.IsVisible,"Una respuesta tardía no reemplaza el módulo ni termina su carga");
        actual.Fuente.SetResult(Datos("CATEGORIA")); Esperar();
        verificar(!tabla.IsVisible&&vm.FilasModulo.Single().Columna1=="CATEGORIA","Solo la respuesta del módulo actual muestra contenido");
        var consulta=vm.CargarModuloActualAsync(); Esperar();
        modulos.Solicitudes.Last().Fuente.SetException(new System.Net.Http.HttpRequestException()); Esperar();
        verificar(consulta.IsCompleted&&!tabla.IsVisible&&vm.MensajeModulo.Contains("Revisa la conexión"),"Un error termina el skeleton y ofrece recuperación");
        ventana.Close(); verificar(!skeleton.HasAnimatedProperties&&!tabla.HasAnimatedProperties,"Cerrar la ventana libera los relojes de skeleton");
        var fallo=new VentanaPrincipalViewModel(new Backend(false),new ResumenPendiente(),new ModulosPendientes(),reportes,
            new ServicioActualizacionesVelopack(),new Panel("Productos"),new(Guid.NewGuid(),"cuenta@example.test","","",Guid.NewGuid(),"owner"));
        var tarea=fallo.CargarInicioAsync(); Esperar();
        verificar(tarea.IsCompleted&&!fallo.EstaCargandoInicio&&!fallo.WidgetsInicio[0].EstaCargando,"Una conexión rechazada no deja tarjetas cargando indefinidamente");
        var vistaReporte=new VentanaReportes(reportes,Guid.NewGuid());vistaReporte.Show();Esperar();
        var cargaReporte=(SkeletonCarga)vistaReporte.FindName("SkeletonReporte");
        verificar(cargaReporte.IsVisible,"Reportes muestra estructura de indicadores y gráficos mientras consulta");
        Capturar(vistaReporte,"skeleton-reportes.png");
        reportes.Fuente.SetException(new System.Net.Http.HttpRequestException());Esperar();
        verificar(!cargaReporte.IsVisible&&((System.Windows.Controls.TextBlock)vistaReporte.FindName("MensajeFormulario")).Text.Contains("Revisa la conexión"),"Un error de Reportes retira el skeleton y conserva su mensaje");
        vistaReporte.Close();
    }
    private static ResultadoModuloDatos Datos(string nombre)=>ResultadoModuloDatos.Correcto(new("Código","Nombre","Detalle","Precio","Fecha","Agregar",
        [new(Guid.NewGuid(),nombre,"Prueba","","","","Activo")]));
    private static void Esperar(int ms=100){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};
        t.Tick+=(_,_)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
    private static void Capturar(Window ventana,string nombre)
    {
        ventana.UpdateLayout();var bitmap=new RenderTargetBitmap((int)ventana.ActualWidth,(int)ventana.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(ventana);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        using var archivo=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,nombre));png.Save(archivo);
    }
    private sealed class Backend(bool exito=true):IServicioEstadoBackend
    {public Task<ResultadoConexion> ProbarConexionAsync(CancellationToken token=default)=>Task.FromResult(exito?ResultadoConexion.Correcta("Conectado"):ResultadoConexion.Fallida("Sin conexión"));}
    private sealed class ResumenPendiente:IServicioResumenInicio
    {public TaskCompletionSource<ResultadoResumenInicio> Fuente {get;}=new();public Task<ResultadoResumenInicio> ObtenerAsync(Guid id,CancellationToken token=default)=>Fuente.Task;}
    private sealed class ModulosPendientes:IServicioModulosAdministrativos
    {
        public List<(string Nombre,TaskCompletionSource<ResultadoModuloDatos> Fuente)> Solicitudes {get;}=[];
        public Task<ResultadoModuloDatos> ObtenerAsync(string nombre,Guid id,CancellationToken token=default)
        {var fuente=new TaskCompletionSource<ResultadoModuloDatos>();Solicitudes.Add((nombre,fuente));return fuente.Task;}
    }
    private sealed class ReportesPendientes:IServicioReportesAdministrativos
    {public TaskCompletionSource<ReporteAdministrativo> Fuente {get;}=new();public Task<ReporteAdministrativo> ObtenerAsync(Guid id,DateOnly inicio,DateOnly fin,CancellationToken token=default)=>Fuente.Task;}
    private sealed class Panel(params string[] nombres):IServicioPanelInicio
    {public bool TieneConfiguracionGuardada=>true;public IReadOnlyList<string> Cargar()=>nombres;public void Guardar(IReadOnlyList<string> valores){} }
}
