using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using Cogana.Admin.Aplicacion.Contratos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaImagenProducto : Window
{
    private readonly IServicioImagenesProducto _imagenes;
    private readonly Guid _tiendaId, _productoId;
    private readonly HttpClient _descarga;
    private readonly CancellationTokenSource _cierre = new();
    private bool _cargando, _cerrada;
    public VentanaImagenProducto(IServicioImagenesProducto imagenes, Guid tiendaId, Guid productoId,
        string nombre, HttpMessageHandler? manejador = null)
    {
        _imagenes=imagenes; _tiendaId=tiendaId; _productoId=productoId;
        _descarga=manejador is null ? new HttpClient() : new HttpClient(manejador);
        _descarga.Timeout=TimeSpan.FromSeconds(15);
        InitializeComponent(); NombreProducto.Text=nombre;
        Loaded+=async (_,_)=>await CargarAsync();
        Closed+=(_,_)=>{_cerrada=true; _cierre.Cancel(); _descarga.Dispose(); FotoProducto.Source=null;};
    }
    private async void AlRecargar(object sender,RoutedEventArgs e)=>await CargarAsync();
    private void AlCerrar(object sender,RoutedEventArgs e)=>Close();
    private async Task CargarAsync()
    {
        if(_cargando || _cerrada) return;
        _cargando=true; BotonRecargar.IsEnabled=false;
        FotoProducto.Source=null; FotoProducto.Visibility=Visibility.Collapsed;
        EstadoImagen.Visibility=Visibility.Visible; EstadoImagen.Text="Cargando imagen…";
        try
        {
            var ruta=await _imagenes.ObtenerRutaAsync(_tiendaId,_productoId,_cierre.Token);
            if(_cerrada) return;
            if(string.IsNullOrWhiteSpace(ruta)) {EstadoImagen.Text="Este producto todavía no tiene una imagen. Puedes agregarla desde Editar producto.";return;}
            if(!Uri.TryCreate(_imagenes.UrlPublica(ruta),UriKind.Absolute,out var url) || url.Scheme!=Uri.UriSchemeHttps)
            {EstadoImagen.Text="La imagen no tiene una dirección válida.";return;}
            using var respuesta=await _descarga.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,_cierre.Token);
            respuesta.EnsureSuccessStatusCode();
            using var datos=new MemoryStream();
            using var origen=await respuesta.Content.ReadAsStreamAsync(_cierre.Token);
            var bloque=new byte[81920]; int leidos;
            while((leidos=await origen.ReadAsync(bloque,_cierre.Token))>0)
            {
                if(datos.Length+leidos>10*1024*1024) throw new InvalidDataException("Imagen demasiado grande");
                datos.Write(bloque,0,leidos);
            }
            _cierre.Token.ThrowIfCancellationRequested(); datos.Position=0;
            var foto=new BitmapImage(); foto.BeginInit(); foto.CacheOption=BitmapCacheOption.OnLoad;
            foto.DecodePixelWidth=1600; foto.StreamSource=datos; foto.EndInit(); foto.Freeze();
            if(_cerrada) return;
            FotoProducto.Source=foto; FotoProducto.Visibility=Visibility.Visible; EstadoImagen.Visibility=Visibility.Collapsed;
        }
        catch(OperationCanceledException) {if(!_cerrada) EstadoImagen.Text="La carga tardó demasiado. Intenta volver a cargar.";}
        catch(ObjectDisposedException) when(_cerrada) { }
        catch(HttpRequestException) {if(!_cerrada) EstadoImagen.Text="No se pudo descargar la imagen. Revisa la conexión y vuelve a cargar.";}
        catch(Exception error) when(error is System.Text.Json.JsonException or IOException or NotSupportedException or System.Runtime.InteropServices.COMException or ArgumentException)
        {if(!_cerrada) EstadoImagen.Text="No se pudo mostrar este archivo. Comprueba la imagen o utiliza JPG o PNG.";}
        finally {_cargando=false; if(!_cerrada) BotonRecargar.IsEnabled=true;}
    }
}
