using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio.Vistas;

internal static class PruebasImagenProducto
{
    public static void Probar(Action<bool,string> verificar)
    {
        var tienda=Guid.NewGuid(); var producto=Guid.NewGuid();
        var servicio=new ImagenSimulada(); var servidor=new DescargaSimulada();
        var ventana=new VentanaImagenProducto(servicio,tienda,producto,"Producto de prueba",servidor);
        ventana.Show(); Esperar();
        var foto=(Image)ventana.FindName("FotoProducto");
        verificar(foto.Source is BitmapSource && foto.IsVisible,"Ver imagen descarga y muestra la foto asociada");
        verificar(servicio.Tienda==tienda && servicio.Producto==producto && servidor.Solicitudes.All(s=>s==HttpMethod.Get),"Consulta el producto de la tienda sin subir ni modificar imágenes");
        Capturar(ventana,"producto-imagen.png"); ventana.Close();
        servicio=new ImagenSimulada{Ruta=null}; servidor=new DescargaSimulada();
        ventana=new(servicio,tienda,producto,"Sin foto",servidor); ventana.Show(); Esperar();
        verificar(servidor.Solicitudes.Count==0 && Estado(ventana).Contains("todavía no tiene"),"Un producto sin foto informa el estado sin descarga"); ventana.Close();
        servicio=new ImagenSimulada(); servidor=new DescargaSimulada{Error=true};
        ventana=new(servicio,tienda,producto,"Error de descarga",servidor); ventana.Show(); Esperar();
        verificar(Estado(ventana).Contains("No se pudo descargar") && ((Image)ventana.FindName("FotoProducto")).Source is null,"Una descarga rechazada no muestra una foto anterior");
        servidor.Error=false; ((Button)ventana.FindName("BotonRecargar")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Esperar();
        verificar(((Image)ventana.FindName("FotoProducto")).Source is not null,"Volver a cargar recupera la foto tras un error"); ventana.Close();
        servidor=new DescargaSimulada{Corrupta=true}; ventana=new(new ImagenSimulada(),tienda,producto,"Archivo incompatible",servidor); ventana.Show(); Esperar();
        verificar(Estado(ventana).Contains("No se pudo mostrar"),"Una imagen dañada informa el problema sin cerrar el programa"); ventana.Close();
    }
    private static string Estado(Window ventana)=>((TextBlock)ventana.FindName("EstadoImagen")).Text;
    private static void Esperar()
    {var marco=new DispatcherFrame();var reloj=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};reloj.Tick+=(_,_)=>{reloj.Stop();marco.Continue=false;};reloj.Start();Dispatcher.PushFrame(marco);}
    private static void Capturar(Window ventana,string nombre)
    {var bitmap=new RenderTargetBitmap((int)ventana.ActualWidth,(int)ventana.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(ventana);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var archivo=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,nombre));png.Save(archivo);}
    private sealed class ImagenSimulada : IServicioImagenesProducto
    {
        public string? Ruta {get;set;}="tienda/producto.png"; public Guid Tienda,Producto;
        public Task<string?> ObtenerRutaAsync(Guid tiendaId,Guid productoId,CancellationToken token=default){Tienda=tiendaId;Producto=productoId;return Task.FromResult(Ruta);}
        public string? UrlPublica(string? ruta)=>ruta is null?null:"https://prueba.example.test/storage/v1/object/public/product-images/"+ruta;
        public Task<ResultadoOperacion> SubirAsync(Guid t,Guid p,string ruta,CancellationToken token=default)=>throw new Exception("La consulta de imagen no debe subir archivos");
    }
    private sealed class DescargaSimulada : HttpMessageHandler
    {
        public bool Error,Corrupta; public List<HttpMethod> Solicitudes {get;}=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud,CancellationToken token)
        {
            Solicitudes.Add(solicitud.Method);
            byte[] datos;
            if(Corrupta) datos=[1,2,3];
            else {var imagen=BitmapSource.Create(32,32,96,96,PixelFormats.Rgb24,null,Enumerable.Repeat((byte)100,32*32*3).ToArray(),32*3);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(imagen));using var memoria=new System.IO.MemoryStream();png.Save(memoria);datos=memoria.ToArray();}
            return Task.FromResult(new HttpResponseMessage(Error?HttpStatusCode.NotFound:HttpStatusCode.OK){Content=new ByteArrayContent(datos)});
        }
    }
}
