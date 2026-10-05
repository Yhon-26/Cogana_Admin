using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Microsoft.Win32;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaEditarProducto : Window
{
    private readonly IServicioCatalogoAdministrativo _servicio;
    private readonly IServicioImagenesProducto? _imagenes;
    private readonly Guid _tiendaId;
    private readonly Guid _productoId;
    private string? _rutaImagen;
    private bool _imagenModificada;

    public VentanaEditarProducto(
        IServicioCatalogoAdministrativo servicio,
        Guid tiendaId,
        Guid productoId,
        IServicioImagenesProducto? servicioImagenes = null)
    {
        _servicio = servicio;
        _imagenes = servicioImagenes;
        _tiendaId = tiendaId;
        _productoId = productoId;
        InitializeComponent();
        Loaded += AlCargar;
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargar;
        MensajeFormulario.Text = "Cargando información desde Supabase...";

        try
        {
            var categoriasTask = _servicio.ObtenerCategoriasAsync(_tiendaId);
            var productoTask = _servicio.ObtenerProductoAsync(_tiendaId, _productoId);
            await Task.WhenAll(categoriasTask, productoTask);

            var producto = await productoTask;
            if (producto is null)
            {
                MensajeFormulario.Text = "El producto ya no está disponible o no pertenece a esta tienda.";
                return;
            }

            ProductoCategoria.ItemsSource = await categoriasTask;
            Mostrar(producto);
            BotonGuardar.IsEnabled = true;
            MensajeFormulario.Text = string.Empty;

            if (_imagenes is not null)
            {
                var rutaImagen = await _imagenes.ObtenerRutaAsync(_tiendaId, _productoId);
                if (!string.IsNullOrWhiteSpace(rutaImagen))
                {
                    MostrarVistaPrevia(_imagenes.UrlPublica(rutaImagen)!);
                }
            }
        }
        catch (HttpRequestException)
        {
            MensajeFormulario.Text = "No fue posible cargar el producto. Revisa la conexión con Supabase.";
        }
    }

    private void Mostrar(ProductoDetalle producto)
    {
        TituloProducto.Text = producto.Nombre;
        TextoEstado.Text = producto.EstaActivo ? "Activo" : "Inactivo";
        EtiquetaEstado.Background = producto.EstaActivo
            ? FindResource("BrochaAcentoSuave") as System.Windows.Media.Brush
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 222, 218));
        TextoEstado.Foreground = producto.EstaActivo
            ? FindResource("BrochaAcento") as System.Windows.Media.Brush
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(154, 68, 63));

        TotalPresentaciones.Text = producto.PresentacionesActivas.ToString("N0");
        TotalLotes.Text = producto.LotesDisponibles.ToString("N0");
        TotalStock.Text = $"{producto.StockTotal:N0} {AbreviarUnidad(producto.UnidadBase)}";

        ProductoNombre.Text = producto.Nombre;
        ProductoSku.Text = producto.Sku ?? string.Empty;
        ProductoMarca.Text = producto.Marca ?? string.Empty;
        ProductoDescripcion.Text = producto.Descripcion ?? string.Empty;
        ProductoCategoria.SelectedValue = producto.CategoriaId;
        ProductoUnidad.SelectedValue = producto.UnidadBase;
        ProductoStockMinimo.Text = producto.StockMinimo.ToString();
        ProductoVencimiento.IsChecked = producto.ControlaVencimiento;
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        MensajeFormulario.Text = Validar() ?? string.Empty;
        if (!string.IsNullOrEmpty(MensajeFormulario.Text))
        {
            return;
        }

        BotonGuardar.IsEnabled = false;
        MensajeFormulario.Text = "Guardando cambios en Supabase...";

        var categoriaId = ProductoCategoria.SelectedValue is Guid valorCategoria
            ? valorCategoria
            : (Guid?)null;
        var resultado = await _servicio.ActualizarProductoAsync(
            _tiendaId,
            _productoId,
            new NuevoProducto(
                ProductoNombre.Text,
                ProductoSku.Text,
                ProductoMarca.Text,
                ProductoDescripcion.Text,
                categoriaId,
                ProductoUnidad.SelectedValue?.ToString() ?? "unit",
                ProductoVencimiento.IsChecked == true,
                long.Parse(ProductoStockMinimo.Text)));

        MensajeFormulario.Text = resultado.Mensaje;

        if (resultado.EsExitoso && _imagenModificada && _imagenes is not null && !string.IsNullOrWhiteSpace(_rutaImagen))
        {
            MensajeFormulario.Text = "Subiendo imagen del producto...";
            var subida = await _imagenes.SubirAsync(_tiendaId, _productoId, _rutaImagen);
            if (!subida.EsExitoso)
            {
                MensajeFormulario.Text = "El producto se guardó, pero la imagen no pudo subirse: " + subida.Mensaje;
                BotonGuardar.IsEnabled = true;
                return;
            }

            MensajeFormulario.Text = "Producto e imagen actualizados correctamente.";
            _imagenModificada = false;
        }

        BotonGuardar.IsEnabled = true;
        if (resultado.EsExitoso)
        {
            DialogResult = true;
        }
    }

    private void AlElegirImagen(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Elegir imagen del producto",
            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.webp",
            CheckFileExists = true
        };

        if (dialogo.ShowDialog(this) != true)
        {
            return;
        }

        MostrarVistaPrevia(dialogo.FileName);
        _rutaImagen = dialogo.FileName;
        _imagenModificada = true;
        BotonQuitarImagen.Visibility = Visibility.Visible;
        MensajeFormulario.Text = string.Empty;
    }

    private void AlQuitarImagen(object sender, RoutedEventArgs e)
    {
        _rutaImagen = null;
        _imagenModificada = false;
        VistaPreviaImagen.Source = null;
        VistaPreviaImagen.Visibility = Visibility.Collapsed;
        TextoImagenProducto.Visibility = Visibility.Visible;
        BotonQuitarImagen.Visibility = Visibility.Collapsed;
    }

    private void MostrarVistaPrevia(string fuente)
    {
        try
        {
            var imagen = new BitmapImage();
            imagen.BeginInit();
            imagen.CacheOption = BitmapCacheOption.OnLoad;
            imagen.UriSource = new Uri(fuente, UriKind.Absolute);
            imagen.EndInit();
            VistaPreviaImagen.Source = imagen;
            VistaPreviaImagen.Visibility = Visibility.Visible;
            TextoImagenProducto.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            MensajeFormulario.Text = "La imagen no pudo mostrarse.";
        }
    }

    private string? Validar()
    {
        if (string.IsNullOrWhiteSpace(ProductoNombre.Text))
        {
            return "Ingresa el nombre del producto.";
        }

        if (ProductoUnidad.SelectedValue is null)
        {
            return "Selecciona la unidad base.";
        }

        if (!long.TryParse(ProductoStockMinimo.Text, out var stockMinimo) || stockMinimo < 0)
        {
            return "El stock mínimo debe ser un número igual o mayor que cero.";
        }

        return null;
    }

    private static string AbreviarUnidad(string unidad) => unidad switch
    {
        "gram" => "g",
        "milliliter" => "ml",
        _ => "unid."
    };

    private void AlCancelar(object sender, RoutedEventArgs e) => DialogResult = false;
}
