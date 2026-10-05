using System.IO;
using System.Net.Http;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Microsoft.Win32;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaNuevoRegistro : Window
{
    private readonly IServicioCatalogoAdministrativo _servicio;
    private readonly IServicioImagenesProducto? _imagenes;
    private readonly Guid _tiendaId;
    private readonly string _modulo;
    private readonly Guid _operacionId = Guid.NewGuid();
    private string? _rutaImagen;

    public VentanaNuevoRegistro(
        IServicioCatalogoAdministrativo servicio,
        Guid tiendaId,
        string modulo,
        IServicioImagenesProducto? servicioImagenes = null)
    {
        _servicio = servicio;
        _imagenes = servicioImagenes;
        _tiendaId = tiendaId;
        _modulo = modulo;
        InitializeComponent();
        ConfigurarFormulario();
        Loaded += AlCargar;
    }

    private void ConfigurarFormulario()
    {
        switch (_modulo)
        {
            case "Productos":
                InicialModulo.Text = "P";
                TituloFormulario.Text = "Nuevo producto";
                DescripcionFormulario.Text = "Registra la información base del catálogo.";
                SeccionProducto.Visibility = Visibility.Visible;
                ProductoUnidad.SelectedIndex = 0;
                break;
            case "Categorías":
                InicialModulo.Text = "C";
                TituloFormulario.Text = "Nueva categoría";
                DescripcionFormulario.Text = "Crea una sección para organizar los productos.";
                SeccionCategoria.Visibility = Visibility.Visible;
                break;
            case "Proveedores":
                InicialModulo.Text = "PV";
                TituloFormulario.Text = "Nuevo proveedor";
                DescripcionFormulario.Text = "Registra un proveedor para el abastecimiento.";
                SeccionProveedor.Visibility = Visibility.Visible;
                break;
            case "Presentaciones":
                InicialModulo.Text = "PR";
                TituloFormulario.Text = "Nueva presentación";
                DescripcionFormulario.Text = "Define cómo se vende un producto y su precio.";
                SeccionPresentacion.Visibility = Visibility.Visible;
                PresentacionTipo.SelectedIndex = 0;
                break;
            case "Promociones":
                InicialModulo.Text = "%";
                TituloFormulario.Text = "Nueva promoción";
                DescripcionFormulario.Text = "Configura el beneficio y su periodo de vigencia.";
                SeccionPromocion.Visibility = Visibility.Visible;
                PromocionTipo.SelectedIndex = 0;
                PromocionInicio.SelectedDate = DateTime.Today;
                PromocionFin.SelectedDate = DateTime.Today.AddDays(30);
                break;
            case "Inventario y lotes":
                InicialModulo.Text = "L";
                TituloFormulario.Text = "Registrar lote";
                DescripcionFormulario.Text = "Añade existencias disponibles al inventario.";
                SeccionLote.Visibility = Visibility.Visible;
                break;
        }
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargar;
        if (_modulo is not ("Productos" or "Presentaciones" or "Inventario y lotes"))
        {
            return;
        }

        try
        {
            if (_modulo == "Productos")
            {
                ProductoCategoria.ItemsSource = await _servicio.ObtenerCategoriasAsync(_tiendaId);
            }
            else if (_modulo == "Presentaciones")
            {
                PresentacionProducto.ItemsSource = await _servicio.ObtenerProductosAsync(_tiendaId);
            }
            else
            {
                var productosTask = _servicio.ObtenerProductosAsync(_tiendaId);
                var proveedoresTask = _servicio.ObtenerProveedoresAsync(_tiendaId);
                await Task.WhenAll(productosTask, proveedoresTask);
                LoteProducto.ItemsSource = await productosTask;
                LoteProveedor.ItemsSource = await proveedoresTask;
            }
        }
        catch (HttpRequestException)
        {
            MensajeFormulario.Text = "No fue posible cargar las categorías.";
        }
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        MensajeFormulario.Text = string.Empty;
        var validacion = Validar();
        if (!string.IsNullOrWhiteSpace(validacion))
        {
            MensajeFormulario.Text = validacion;
            return;
        }

        BotonGuardar.IsEnabled = false;
        MensajeFormulario.Text = "Guardando en Supabase...";

        try
        {
            var resultado = await GuardarAsync();
            if (resultado.EsExitoso &&
                _ultimoProductoCreadoId is Guid nuevoId &&
                _rutaImagen is not null &&
                _imagenes is not null)
            {
                MensajeFormulario.Text = "Subiendo imagen del producto...";
                var subida = await _imagenes.SubirAsync(_tiendaId, nuevoId, _rutaImagen);
                MensajeFormulario.Text = subida.EsExitoso
                    ? "Producto e imagen registrados correctamente."
                    : $"El producto se creó, pero la imagen no pudo subirse: {subida.Mensaje}";
                if (!subida.EsExitoso)
                {
                    BotonGuardar.IsEnabled = true;
                    return;
                }
            }

            MensajeFormulario.Text = resultado.Mensaje;
            if (resultado.EsExitoso)
            {
                DialogResult = true;
            }
        }
        finally
        {
            BotonGuardar.IsEnabled = true;
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
        BotonQuitarImagen.Visibility = Visibility.Visible;
        MensajeFormulario.Text = string.Empty;
    }

    private void AlQuitarImagen(object sender, RoutedEventArgs e)
    {
        _rutaImagen = null;
        VistaPreviaImagen.Source = null;
        VistaPreviaImagen.Visibility = Visibility.Collapsed;
        TextoImagenProducto.Visibility = Visibility.Visible;
        BotonQuitarImagen.Visibility = Visibility.Collapsed;
    }

    private void MostrarVistaPrevia(string ruta)
    {
        try
        {
            var imagen = new BitmapImage();
            imagen.BeginInit();
            imagen.CacheOption = BitmapCacheOption.OnLoad;
            imagen.UriSource = new Uri(ruta, UriKind.Absolute);
            imagen.EndInit();
            VistaPreviaImagen.Source = imagen;
            VistaPreviaImagen.Visibility = Visibility.Visible;
            TextoImagenProducto.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            MensajeFormulario.Text = "El archivo no es una imagen válida.";
        }
    }

    private Guid? _ultimoProductoCreadoId;

    private async Task<ResultadoOperacion> GuardarAsync()
    {
        switch (_modulo)
        {
            case "Productos":
            {
                var creacion = await _servicio.CrearProductoAsync(
                    _tiendaId,
                    new NuevoProducto(
                        ProductoNombre.Text,
                        ProductoSku.Text,
                        ProductoMarca.Text,
                        ProductoDescripcion.Text,
                        ProductoCategoria.SelectedValue as Guid?,
                        ProductoUnidad.SelectedValue?.ToString() ?? "kg",
                        ProductoVencimiento.IsChecked == true,
                        long.TryParse(ProductoStockMinimo.Text, out var minimo) ? minimo : 0));
                _ultimoProductoCreadoId = creacion.ProductoId;
                return creacion;
            }
            case "Categorías":
                return await _servicio.CrearCategoriaAsync(
                    _tiendaId,
                    new NuevaCategoria(
                        CategoriaNombre.Text,
                        int.TryParse(CategoriaOrden.Text, out var orden) ? orden : 0));
            case "Proveedores":
                return await _servicio.CrearProveedorAsync(
                    _tiendaId,
                    new NuevoProveedor(
                        ProveedorNombre.Text,
                        ProveedorRuc.Text,
                        ProveedorTelefono.Text));
            case "Presentaciones":
                return await _servicio.CrearPresentacionAsync(
                    _tiendaId,
                    new NuevaPresentacion(
                        (Guid)(PresentacionProducto.SelectedValue ?? Guid.Empty),
                        PresentacionNombre.Text,
                        PresentacionSku.Text,
                        PresentacionTipo.SelectedValue?.ToString() ?? "bulk",
                        long.TryParse(PresentacionCantidad.Text, out var cantidad) ? cantidad : 1,
                        ImporteEnCentimos(PresentacionPrecio.Text),
                        PresentacionSellada.IsChecked == true));
            case "Promociones":
                return await _servicio.CrearPromocionAsync(
                    _tiendaId,
                    new NuevaPromocion(
                        PromocionNombre.Text,
                        PromocionDescripcion.Text,
                        PromocionTipo.SelectedValue?.ToString() ?? "percentage",
                        ValorPromocion(),
                        CrearFecha(PromocionInicio.SelectedDate ?? DateTime.Today),
                        CrearFecha(PromocionFin.SelectedDate ?? DateTime.Today.AddDays(30)).AddDays(1).AddTicks(-1)));
            case "Inventario y lotes":
                return await _servicio.RegistrarLoteAsync(
                    _tiendaId,
                    new NuevoLote(
                        _operacionId,
                        (Guid)(LoteProducto.SelectedValue ?? Guid.Empty),
                        LoteProveedor.SelectedValue as Guid?,
                        LoteCodigo.Text,
                        LoteVencimiento.SelectedDate is DateTime vencimiento
                            ? DateOnly.FromDateTime(vencimiento)
                            : null,
                        long.TryParse(LoteCantidad.Text, out var cantidadLote) ? cantidadLote : 0,
                        LoteNotas.Text));
            default:
                return ResultadoOperacion.Fallida("Módulo no disponible.");
        }
    }

    private string? Validar()
    {
        if (_modulo == "Productos")
        {
            if (string.IsNullOrWhiteSpace(ProductoNombre.Text)) return "Ingresa el nombre del producto.";
            if (!long.TryParse(ProductoStockMinimo.Text, out var minimo) || minimo < 0)
                return "El stock mínimo debe ser un número igual o mayor que cero.";
        }

        if (_modulo == "Categorías")
        {
            if (string.IsNullOrWhiteSpace(CategoriaNombre.Text)) return "Ingresa el nombre de la categoría.";
            if (!int.TryParse(CategoriaOrden.Text, out var orden) || orden < 0)
                return "El orden debe ser un número igual o mayor que cero.";
        }

        if (_modulo == "Proveedores" && string.IsNullOrWhiteSpace(ProveedorNombre.Text))
        {
            return "Ingresa el nombre o razón social del proveedor.";
        }

        if (_modulo == "Presentaciones")
        {
            if (PresentacionProducto.SelectedValue is not Guid) return "Selecciona un producto.";
            if (string.IsNullOrWhiteSpace(PresentacionNombre.Text)) return "Ingresa el nombre de la presentación.";
            if (!long.TryParse(PresentacionCantidad.Text, out var cantidad) || cantidad <= 0)
                return "La cantidad base debe ser mayor que cero.";
            if (!TryParseImporte(PresentacionPrecio.Text, out var precio) || precio <= 0)
                return "Ingresa un precio válido mayor que cero.";
        }

        if (_modulo == "Promociones")
        {
            if (string.IsNullOrWhiteSpace(PromocionNombre.Text)) return "Ingresa el nombre de la promoción.";
            if (PromocionInicio.SelectedDate is null || PromocionFin.SelectedDate is null)
                return "Selecciona las fechas de inicio y fin.";
            if (PromocionFin.SelectedDate <= PromocionInicio.SelectedDate)
                return "La fecha de fin debe ser posterior a la fecha de inicio.";
            if (PromocionTipo.SelectedValue?.ToString() != "free_delivery" &&
                (!TryParseImporte(PromocionValor.Text, out var valor) || valor <= 0))
                return "Ingresa un valor válido para la promoción.";
        }

        if (_modulo == "Inventario y lotes")
        {
            if (LoteProducto.SelectedValue is not Guid) return "Selecciona un producto.";
            if (string.IsNullOrWhiteSpace(LoteCodigo.Text)) return "Ingresa el código del lote.";
            if (!long.TryParse(LoteCantidad.Text, out var cantidad) || cantidad <= 0)
                return "La cantidad debe ser un número mayor que cero.";
            if (LoteVencimiento.SelectedDate is DateTime fecha && fecha.Date < DateTime.Today)
                return "La fecha de vencimiento no puede estar en el pasado.";
        }

        return null;
    }

    private long ValorPromocion()
    {
        var tipo = PromocionTipo.SelectedValue?.ToString();
        if (tipo == "free_delivery")
        {
            return 0;
        }

        TryParseImporte(PromocionValor.Text, out var valor);
        return tipo == "fixed_amount"
            ? checked((long)Math.Round(valor * 100m))
            : checked((long)Math.Round(valor));
    }

    private static long ImporteEnCentimos(string texto)
    {
        TryParseImporte(texto, out var valor);
        return checked((long)Math.Round(valor * 100m));
    }

    private static bool TryParseImporte(string texto, out decimal valor) =>
        decimal.TryParse(texto, NumberStyles.Number, new CultureInfo("es-PE"), out valor) ||
        decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);

    private static DateTimeOffset CrearFecha(DateTime fecha) =>
        new(fecha, TimeZoneInfo.Local.GetUtcOffset(fecha));

    private void AlCancelar(object sender, RoutedEventArgs e) => DialogResult = false;
}
