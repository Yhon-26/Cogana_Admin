using System.Globalization;
using System.Net.Http;
using System.Windows;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaEditarCatalogo : Window
{
    private readonly IServicioCatalogoAdministrativo _servicio;
    private readonly Guid _tiendaId;
    private readonly Guid _registroId;
    private readonly string _modulo;

    public VentanaEditarCatalogo(
        IServicioCatalogoAdministrativo servicio,
        Guid tiendaId,
        Guid registroId,
        string modulo)
    {
        _servicio = servicio;
        _tiendaId = tiendaId;
        _registroId = registroId;
        _modulo = modulo;
        InitializeComponent();
        ConfigurarModulo();
        Loaded += AlCargar;
    }

    private void ConfigurarModulo()
    {
        switch (_modulo)
        {
            case "Categorías":
                Title = "Editar categoría";
                InicialModulo.Text = "C";
                TituloFormulario.Text = "Editar categoría";
                DescripcionFormulario.Text = "Organización y orden del catálogo";
                SeccionCategoria.Visibility = Visibility.Visible;
                break;
            case "Presentaciones":
                Title = "Editar presentación";
                InicialModulo.Text = "PR";
                TituloFormulario.Text = "Editar presentación";
                DescripcionFormulario.Text = "Formato de venta, cantidad y precio";
                SeccionPresentacion.Visibility = Visibility.Visible;
                break;
            case "Proveedores":
                Title = "Editar proveedor";
                InicialModulo.Text = "PV";
                TituloFormulario.Text = "Editar proveedor";
                DescripcionFormulario.Text = "Datos comerciales y de contacto";
                SeccionProveedor.Visibility = Visibility.Visible;
                break;
        }
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargar;
        MensajeFormulario.Text = "Cargando información desde Supabase...";

        try
        {
            var encontrado = _modulo switch
            {
                "Categorías" => await CargarCategoriaAsync(),
                "Presentaciones" => await CargarPresentacionAsync(),
                "Proveedores" => await CargarProveedorAsync(),
                _ => false
            };

            if (!encontrado)
            {
                MensajeFormulario.Text = "El registro ya no está disponible o no pertenece a esta tienda.";
                return;
            }

            BotonGuardar.IsEnabled = true;
            MensajeFormulario.Text = string.Empty;
        }
        catch (HttpRequestException)
        {
            MensajeFormulario.Text = "No fue posible cargar el registro. Revisa la conexión con Supabase.";
        }
    }

    private async Task<bool> CargarCategoriaAsync()
    {
        var categoria = await _servicio.ObtenerCategoriaAsync(_tiendaId, _registroId);
        if (categoria is null) return false;

        CategoriaNombre.Text = categoria.Nombre;
        CategoriaOrden.Text = categoria.Orden.ToString();
        CategoriaIdentificador.Text = categoria.Identificador;
        MostrarEstado(categoria.EstaActiva);
        DetalleSincronizacion.Text = "El identificador se actualizará automáticamente al cambiar el nombre.";
        return true;
    }

    private async Task<bool> CargarPresentacionAsync()
    {
        var productosTask = _servicio.ObtenerProductosAsync(_tiendaId);
        var presentacionTask = _servicio.ObtenerPresentacionAsync(_tiendaId, _registroId);
        await Task.WhenAll(productosTask, presentacionTask);
        var presentacion = await presentacionTask;
        if (presentacion is null) return false;

        PresentacionProducto.ItemsSource = await productosTask;
        PresentacionProducto.SelectedValue = presentacion.ProductoId;
        PresentacionNombre.Text = presentacion.Nombre;
        PresentacionSku.Text = presentacion.Sku ?? string.Empty;
        PresentacionTipo.SelectedValue = presentacion.Tipo;
        PresentacionCantidad.Text = presentacion.CantidadBase.ToString();
        PresentacionPrecio.Text = (presentacion.PrecioCentimos / 100m).ToString("0.00", new CultureInfo("es-PE"));
        PresentacionSellada.IsChecked = presentacion.Sellada;
        MostrarEstado(presentacion.EstaActiva);
        return true;
    }

    private async Task<bool> CargarProveedorAsync()
    {
        var proveedor = await _servicio.ObtenerProveedorAsync(_tiendaId, _registroId);
        if (proveedor is null) return false;

        ProveedorNombre.Text = proveedor.Nombre;
        ProveedorRuc.Text = proveedor.Ruc ?? string.Empty;
        ProveedorTelefono.Text = proveedor.Telefono ?? string.Empty;
        MostrarEstado(proveedor.EstaActivo);
        return true;
    }

    private void MostrarEstado(bool activo)
    {
        TextoEstado.Text = activo ? "Activo" : "Inactivo";
        if (activo) return;

        EtiquetaEstado.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(238, 222, 218));
        TextoEstado.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(154, 68, 63));
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        MensajeFormulario.Text = Validar() ?? string.Empty;
        if (!string.IsNullOrEmpty(MensajeFormulario.Text)) return;

        BotonGuardar.IsEnabled = false;
        MensajeFormulario.Text = "Guardando cambios en Supabase...";
        var resultado = await GuardarAsync();
        MensajeFormulario.Text = resultado.Mensaje;
        BotonGuardar.IsEnabled = true;

        if (resultado.EsExitoso)
        {
            DialogResult = true;
        }
    }

    private Task<ResultadoOperacion> GuardarAsync() => _modulo switch
    {
        "Categorías" => _servicio.ActualizarCategoriaAsync(
            _tiendaId,
            _registroId,
            new NuevaCategoria(CategoriaNombre.Text, int.Parse(CategoriaOrden.Text))),
        "Presentaciones" => _servicio.ActualizarPresentacionAsync(
            _tiendaId,
            _registroId,
            new NuevaPresentacion(
                (Guid)(PresentacionProducto.SelectedValue ?? Guid.Empty),
                PresentacionNombre.Text,
                PresentacionSku.Text,
                PresentacionTipo.SelectedValue?.ToString() ?? "unit",
                long.Parse(PresentacionCantidad.Text),
                checked((long)Math.Round(LeerImporte(PresentacionPrecio.Text) * 100m)),
                PresentacionSellada.IsChecked == true)),
        "Proveedores" => _servicio.ActualizarProveedorAsync(
            _tiendaId,
            _registroId,
            new NuevoProveedor(ProveedorNombre.Text, ProveedorRuc.Text, ProveedorTelefono.Text)),
        _ => Task.FromResult(ResultadoOperacion.Fallida("Módulo no disponible."))
    };

    private string? Validar()
    {
        if (_modulo == "Categorías")
        {
            if (string.IsNullOrWhiteSpace(CategoriaNombre.Text)) return "Ingresa el nombre de la categoría.";
            if (!int.TryParse(CategoriaOrden.Text, out var orden) || orden < 0)
                return "El orden debe ser un número igual o mayor que cero.";
        }

        if (_modulo == "Presentaciones")
        {
            if (PresentacionProducto.SelectedValue is not Guid) return "Selecciona un producto.";
            if (string.IsNullOrWhiteSpace(PresentacionNombre.Text)) return "Ingresa el nombre de la presentación.";
            if (PresentacionTipo.SelectedValue is null) return "Selecciona el tipo de presentación.";
            if (!long.TryParse(PresentacionCantidad.Text, out var cantidad) || cantidad <= 0)
                return "La cantidad base debe ser mayor que cero.";
            if (!TryLeerImporte(PresentacionPrecio.Text, out var precio) || precio <= 0)
                return "Ingresa un precio válido mayor que cero.";
        }

        if (_modulo == "Proveedores")
        {
            if (string.IsNullOrWhiteSpace(ProveedorNombre.Text)) return "Ingresa el nombre o razón social.";
            var ruc = ProveedorRuc.Text.Trim();
            if (ruc.Length > 0 && (ruc.Length != 11 || !ruc.All(char.IsDigit)))
                return "El RUC debe contener 11 números.";
        }

        return null;
    }

    private static decimal LeerImporte(string texto)
    {
        TryLeerImporte(texto, out var valor);
        return valor;
    }

    private static bool TryLeerImporte(string texto, out decimal valor) =>
        decimal.TryParse(texto, NumberStyles.Number, new CultureInfo("es-PE"), out valor) ||
        decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);

    private void AlCancelar(object sender, RoutedEventArgs e) => DialogResult = false;
}
