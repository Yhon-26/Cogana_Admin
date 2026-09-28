using System.Net.Http;
using System.Windows;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaDetallePedido : Window
{
    private readonly IServicioPedidosAdministrativos _servicio;
    private readonly Guid _tiendaId;
    private readonly Guid _pedidoId;
    private PedidoDetalle? _pedido;
    private bool _huboCambios;

    public VentanaDetallePedido(
        IServicioPedidosAdministrativos servicio,
        Guid tiendaId,
        Guid pedidoId)
    {
        _servicio = servicio;
        _tiendaId = tiendaId;
        _pedidoId = pedidoId;
        InitializeComponent();
        Loaded += AlCargar;
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargar;
        await CargarDatosAsync();
    }

    private async Task CargarDatosAsync()
    {
        MensajeVentana.Text = "Cargando información completa desde Supabase...";
        BotonGestionar.IsEnabled = false;

        try
        {
            var pedidoTask = _servicio.ObtenerPedidoAsync(_tiendaId, _pedidoId);
            var itemsTask = _servicio.ObtenerItemsAsync(_tiendaId, _pedidoId);
            var historialTask = _servicio.ObtenerHistorialAsync(_tiendaId, _pedidoId);
            var pagosTask = _servicio.ObtenerPagosAsync(_tiendaId, _pedidoId);
            var entregaTask = _servicio.ObtenerEntregaAsync(_tiendaId, _pedidoId);
            await Task.WhenAll(pedidoTask, itemsTask, historialTask, pagosTask, entregaTask);

            _pedido = await pedidoTask;
            if (_pedido is null)
            {
                MensajeVentana.Text = "El pedido ya no está disponible o no pertenece a esta tienda.";
                return;
            }

            var items = await itemsTask;
            var historial = await historialTask;
            var pagos = await pagosTask;
            var entrega = await entregaTask;
            MostrarPedido(_pedido);
            ListaItems.ItemsSource = items;
            ListaHistorial.ItemsSource = historial;
            ListaPagos.ItemsSource = pagos;
            MostrarEntrega(entrega);
            ResumenItems.Text = items.Count == 0 ? "No hay productos registrados." : $"{items.Count:N0} productos en el pedido.";
            ResumenPagos.Text = pagos.Count == 0 ? "No hay transacciones registradas." : $"{pagos.Count:N0} transacciones registradas.";
            BotonGestionar.IsEnabled = _pedido.Estado is not ("completed" or "cancelled");
            MensajeVentana.Text = historial.Count == 0 ? "El pedido todavía no tiene historial de estados." : $"{historial.Count:N0} cambios registrados en el historial.";
        }
        catch (HttpRequestException)
        {
            MensajeVentana.Text = "No fue posible cargar el pedido. Revisa la conexión con Supabase.";
        }
    }

    private void MostrarPedido(PedidoDetalle pedido)
    {
        TituloPedido.Text = $"Pedido {pedido.Numero}";
        SubtituloPedido.Text = $"{pedido.NombreCliente} · {pedido.TipoEntrega}";
        TextoEstado.Text = TraducirEstado(pedido.Estado);
        NombreCliente.Text = pedido.NombreCliente;
        ContactoCliente.Text = string.Join(" · ", new[] { pedido.TelefonoCliente, pedido.CorreoCliente }.Where(valor => !string.IsNullOrWhiteSpace(valor)));
        ModalidadEntrega.Text = pedido.TipoEntrega;
        DireccionEntrega.Text = pedido.TipoEntrega == "Delivery" ? pedido.DireccionEntrega ?? "Dirección no disponible" : "Recojo en tienda";
        ReferenciaEntrega.Text = string.IsNullOrWhiteSpace(pedido.ReferenciaEntrega) ? string.Empty : $"Referencia: {pedido.ReferenciaEntrega}";
        HorarioPedido.Text = pedido.ProgramadoPara?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Lo antes posible";
        MetodoPago.Text = $"{pedido.MetodoPago} · {TraducirPago(pedido.EstadoPago)}";
        OrigenPedido.Text = $"Origen: {pedido.Origen}";
        FechaPedido.Text = $"Creado: {pedido.CreadoEn.ToLocalTime():dd/MM/yyyy HH:mm}";
        NotasPedido.Text = string.IsNullOrWhiteSpace(pedido.Notas) ? "Sin notas del cliente" : pedido.Notas;

        var subtotal = pedido.SubtotalFinalCentimos ?? pedido.SubtotalEstimadoCentimos;
        var descuento = pedido.DescuentoFinalCentimos ?? pedido.DescuentoEstimadoCentimos;
        var entrega = pedido.TarifaEntregaCentimos - pedido.DescuentoEntregaCentimos;
        var total = pedido.TotalFinalCentimos ?? pedido.TotalEstimadoCentimos;
        ImporteSubtotal.Text = Moneda(subtotal);
        ImporteDescuentos.Text = Moneda(descuento + pedido.DescuentoEntregaCentimos);
        ImporteEntrega.Text = Moneda(entrega);
        ImporteTotal.Text = Moneda(total);

        if (pedido.Estado == "cancelled")
        {
            EtiquetaEstado.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 222, 218));
            TextoEstado.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(154, 68, 63));
        }
    }

    private void MostrarEntrega(EntregaPedidoDetalle? entrega)
    {
        if (entrega is null)
        {
            EstadoEntrega.Text = _pedido?.TipoEntrega == "Delivery" ? "Sin asignación de reparto" : "Pedido para recojo";
            DetalleEntrega.Text = "No existen eventos de reparto para este pedido.";
            ListaEventosEntrega.ItemsSource = null;
            return;
        }

        EstadoEntrega.Text = entrega.Estado;
        var detalle = new List<string> { $"Asignado: {entrega.AsignadoEn.ToLocalTime():dd/MM/yyyy HH:mm}" };
        if (!string.IsNullOrWhiteSpace(entrega.Destinatario)) detalle.Add($"Destinatario: {entrega.Destinatario}");
        if (!string.IsNullOrWhiteSpace(entrega.Notas)) detalle.Add(entrega.Notas);
        DetalleEntrega.Text = string.Join(" · ", detalle);
        ListaEventosEntrega.ItemsSource = entrega.Eventos;
    }

    private async void AlGestionar(object sender, RoutedEventArgs e)
    {
        if (_pedido is null) return;
        var fila = new FilaModuloDatos(
            _pedido.Id,
            _pedido.Numero,
            _pedido.NombreCliente,
            _pedido.TipoEntrega,
            TraducirEstado(_pedido.Estado),
            Moneda(_pedido.TotalFinalCentimos ?? _pedido.TotalEstimadoCentimos),
            TraducirPago(_pedido.EstadoPago),
            _pedido.Estado);
        var ventana = new VentanaCambioEstadoPedido(_servicio, _tiendaId, fila) { Owner = this };
        if (ventana.ShowDialog() == true)
        {
            _huboCambios = true;
            await CargarDatosAsync();
        }
    }

    private static string Moneda(long centimos) => $"S/ {centimos / 100m:N2}";
    private static string TraducirEstado(string estado) => estado switch
    {
        "pending_payment" => "Pago pendiente",
        "queued" => "En cola",
        "confirmed" => "Confirmado",
        "preparing" => "En preparación",
        "awaiting_adjustment" => "Esperando ajuste",
        "ready_for_pickup" => "Listo para recojo",
        "ready_for_delivery" => "Listo para delivery",
        "out_for_delivery" => "En camino",
        "completed" => "Completado",
        "cancelled" => "Cancelado",
        _ => estado
    };

    private static string TraducirPago(string estado) => estado switch
    {
        "pending" => "Pendiente",
        "requires_action" => "Requiere acción",
        "processing" => "Procesando",
        "paid" => "Pagado",
        "partially_refunded" => "Reembolso parcial",
        "refunded" => "Reembolsado",
        "failed" => "Fallido",
        "expired" => "Vencido",
        "cancelled" => "Cancelado",
        _ => estado
    };

    private void AlCerrar(object sender, RoutedEventArgs e) => DialogResult = _huboCambios;
}
