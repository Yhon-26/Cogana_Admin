using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaEditarPromocion : Window
{
    private readonly IServicioPromocionesAdministrativas _servicio;
    private readonly Guid _tiendaId;
    private readonly Guid _promocionId;
    private Guid _operacionId = Guid.NewGuid();
    private List<ProductoPromocionSeleccion> _productos = [];

    public VentanaEditarPromocion(
        IServicioPromocionesAdministrativas servicio,
        Guid tiendaId,
        Guid promocionId)
    {
        _servicio = servicio;
        _tiendaId = tiendaId;
        _promocionId = promocionId;
        InitializeComponent();
        Loaded += AlCargar;
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargar;
        MensajeFormulario.Text = "Cargando promoción y productos desde Supabase...";

        try
        {
            var promocionTask = _servicio.ObtenerPromocionAsync(_tiendaId, _promocionId);
            var productosTask = _servicio.ObtenerProductosAsync(_tiendaId);
            await Task.WhenAll(promocionTask, productosTask);
            var promocion = await promocionTask;
            if (promocion is null)
            {
                MensajeFormulario.Text = "La promoción ya no está disponible o no pertenece a esta tienda.";
                return;
            }

            _productos = (await productosTask).ToList();
            foreach (var producto in _productos)
            {
                producto.EstaSeleccionado = promocion.ProductosSeleccionados.Contains(producto.Id);
            }
            ListaProductos.ItemsSource = _productos;
            Mostrar(promocion);
            ActualizarResumenProductos();
            BotonGuardar.IsEnabled = true;
            MensajeFormulario.Text = string.Empty;
        }
        catch (HttpRequestException)
        {
            MensajeFormulario.Text = "No fue posible cargar la promoción. Revisa la conexión con Supabase.";
        }
    }

    private void Mostrar(PromocionDetalle promocion)
    {
        TituloPromocion.Text = promocion.Nombre;
        TextoEstado.Text = promocion.EstaActiva ? "Activa" : "Inactiva";
        if (!promocion.EstaActiva)
        {
            EtiquetaEstado.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 222, 218));
            TextoEstado.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(154, 68, 63));
        }

        PromocionNombre.Text = promocion.Nombre;
        PromocionDescripcion.Text = promocion.Descripcion ?? string.Empty;
        PromocionTipo.SelectedValue = promocion.Tipo;
        PromocionValor.Text = EsTipoDinero(promocion.Tipo)
            ? (promocion.Valor / 100m).ToString("0.00", new CultureInfo("es-PE"))
            : promocion.Valor.ToString();
        PromocionInicio.SelectedDate = promocion.Inicio.ToLocalTime().Date;
        PromocionFin.SelectedDate = promocion.Fin.ToLocalTime().Date;
        PromocionPedidoMinimo.Text = (promocion.PedidoMinimoCentimos / 100m).ToString("0.00", new CultureInfo("es-PE"));
        PromocionCantidadMinima.Text = promocion.CantidadMinima?.ToString() ?? string.Empty;
        PromocionCupon.Text = promocion.CodigoCupon ?? string.Empty;
        PromocionPrioridad.Text = promocion.Prioridad.ToString();
        PromocionLimiteUsos.Text = promocion.LimiteUsos?.ToString() ?? string.Empty;
        PromocionLimiteCliente.Text = promocion.LimitePorCliente?.ToString() ?? string.Empty;
        PromocionAcumulable.IsChecked = promocion.Acumulable;
        ActualizarCamposTipo();
    }

    private void AlCambiarTipo(object sender, SelectionChangedEventArgs e) => ActualizarCamposTipo();

    private void ActualizarCamposTipo()
    {
        if (EtiquetaValor is null || PromocionValor is null || ContenedorCantidadMinima is null) return;
        var tipo = PromocionTipo.SelectedValue?.ToString();
        EtiquetaValor.Text = tipo switch
        {
            "percentage" => "Porcentaje *",
            "free_delivery" => "Sin valor monetario",
            _ => "Importe en soles *"
        };
        PromocionValor.IsEnabled = tipo != "free_delivery";
        if (tipo == "free_delivery") PromocionValor.Text = "0";
        ContenedorCantidadMinima.Visibility = tipo == "quantity_price" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AlCambiarProducto(object sender, RoutedEventArgs e) => ActualizarResumenProductos();

    private void AlSeleccionarTodos(object sender, RoutedEventArgs e)
    {
        foreach (var producto in _productos) producto.EstaSeleccionado = true;
        ListaProductos.Items.Refresh();
        ActualizarResumenProductos();
    }

    private void AlQuitarSeleccion(object sender, RoutedEventArgs e)
    {
        foreach (var producto in _productos) producto.EstaSeleccionado = false;
        ListaProductos.Items.Refresh();
        ActualizarResumenProductos();
    }

    private void ActualizarResumenProductos()
    {
        var seleccionados = _productos.Count(producto => producto.EstaSeleccionado);
        ResumenProductos.Text = seleccionados == 0
            ? "Aplicará a todo el catálogo."
            : $"{seleccionados:N0} de {_productos.Count:N0} productos seleccionados.";
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        MensajeFormulario.Text = Validar() ?? string.Empty;
        if (!string.IsNullOrEmpty(MensajeFormulario.Text)) return;

        BotonGuardar.IsEnabled = false;
        MensajeFormulario.Text = "Guardando reglas y productos en Supabase...";
        var tipo = PromocionTipo.SelectedValue!.ToString()!;
        var resultado = await _servicio.GuardarAsync(
            _tiendaId,
            _promocionId,
            new EdicionPromocion(
                _operacionId,
                PromocionNombre.Text,
                PromocionDescripcion.Text,
                tipo,
                LeerValor(tipo),
                tipo == "quantity_price" ? long.Parse(PromocionCantidadMinima.Text) : null,
                ACentimos(PromocionPedidoMinimo.Text),
                PromocionCupon.Text,
                CrearFecha(PromocionInicio.SelectedDate!.Value),
                CrearFecha(PromocionFin.SelectedDate!.Value).AddDays(1).AddTicks(-1),
                LeerEnteroOpcional(PromocionLimiteUsos.Text),
                LeerEnteroOpcional(PromocionLimiteCliente.Text),
                int.Parse(PromocionPrioridad.Text),
                PromocionAcumulable.IsChecked == true,
                _productos.Where(producto => producto.EstaSeleccionado).Select(producto => producto.Id).ToList()));

        MensajeFormulario.Text = resultado.Mensaje;
        BotonGuardar.IsEnabled = true;
        if (resultado.EsExitoso)
        {
            DialogResult = true;
        }
    }

    private string? Validar()
    {
        if (string.IsNullOrWhiteSpace(PromocionNombre.Text)) return "Ingresa el nombre de la promoción.";
        var tipo = PromocionTipo.SelectedValue?.ToString();
        if (string.IsNullOrWhiteSpace(tipo)) return "Selecciona el tipo de promoción.";
        if (PromocionInicio.SelectedDate is null || PromocionFin.SelectedDate is null)
            return "Selecciona las fechas de inicio y fin.";
        if (PromocionFin.SelectedDate < PromocionInicio.SelectedDate)
            return "La fecha de fin debe ser igual o posterior a la fecha de inicio.";

        if (tipo == "percentage")
        {
            if (!long.TryParse(PromocionValor.Text, out var porcentaje) || porcentaje is < 1 or > 100)
                return "El porcentaje debe estar entre 1 y 100.";
        }
        else if (EsTipoDinero(tipo) && (!TryLeerImporte(PromocionValor.Text, out var valor) || valor <= 0))
        {
            return "Ingresa un importe mayor que cero.";
        }

        if (!TryLeerImporte(PromocionPedidoMinimo.Text, out var pedidoMinimo) || pedidoMinimo < 0)
            return "El pedido mínimo debe ser un importe igual o mayor que cero.";
        if (tipo == "quantity_price" && (!long.TryParse(PromocionCantidadMinima.Text, out var cantidad) || cantidad <= 0))
            return "Ingresa una cantidad mínima mayor que cero.";
        if (!int.TryParse(PromocionPrioridad.Text, out var prioridad) || prioridad < 0)
            return "La prioridad debe ser un número igual o mayor que cero.";
        if (!EsEnteroOpcionalValido(PromocionLimiteUsos.Text)) return "El límite total debe quedar vacío o ser mayor que cero.";
        if (!EsEnteroOpcionalValido(PromocionLimiteCliente.Text)) return "El límite por cliente debe quedar vacío o ser mayor que cero.";
        return null;
    }

    private long LeerValor(string tipo)
    {
        if (tipo == "free_delivery") return 0;
        if (tipo == "percentage") return long.Parse(PromocionValor.Text);
        return ACentimos(PromocionValor.Text);
    }

    private static bool EsTipoDinero(string? tipo) => tipo is "fixed_amount" or "fixed_price" or "quantity_price";
    private static long ACentimos(string texto) => checked((long)Math.Round(LeerImporte(texto) * 100m));
    private static decimal LeerImporte(string texto) { TryLeerImporte(texto, out var valor); return valor; }
    private static bool TryLeerImporte(string texto, out decimal valor) =>
        decimal.TryParse(texto, NumberStyles.Number, new CultureInfo("es-PE"), out valor) ||
        decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    private static bool EsEnteroOpcionalValido(string texto) =>
        string.IsNullOrWhiteSpace(texto) || int.TryParse(texto, out var valor) && valor > 0;
    private static int? LeerEnteroOpcional(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : int.Parse(texto);
    private static DateTimeOffset CrearFecha(DateTime fecha) =>
        new(fecha, TimeZoneInfo.Local.GetUtcOffset(fecha));

    private void AlCerrar(object sender, RoutedEventArgs e) => DialogResult = false;
}
