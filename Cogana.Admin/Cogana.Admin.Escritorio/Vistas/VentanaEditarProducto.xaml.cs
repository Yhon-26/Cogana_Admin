using System.Net.Http;
using System.Windows;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaEditarProducto : Window
{
    private readonly IServicioCatalogoAdministrativo _servicio;
    private readonly Guid _tiendaId;
    private readonly Guid _productoId;

    public VentanaEditarProducto(
        IServicioCatalogoAdministrativo servicio,
        Guid tiendaId,
        Guid productoId)
    {
        _servicio = servicio;
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
        BotonGuardar.IsEnabled = true;
        if (resultado.EsExitoso)
        {
            DialogResult = true;
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
