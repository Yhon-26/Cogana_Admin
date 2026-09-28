using System.Windows;
using System.Windows.Controls;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaCambioEstadoPedido : Window
{
    private readonly IServicioPedidosAdministrativos _servicio;
    private readonly Guid _tiendaId;
    private readonly FilaModuloDatos _pedido;

    public VentanaCambioEstadoPedido(
        IServicioPedidosAdministrativos servicio,
        Guid tiendaId,
        FilaModuloDatos pedido)
    {
        _servicio = servicio;
        _tiendaId = tiendaId;
        _pedido = pedido;
        InitializeComponent();
        TituloPedido.Text = $"Pedido {pedido.Columna1}";
        EstadoActual.Text = $"Estado actual: {pedido.Columna4}";
        NuevoEstado.ItemsSource = ObtenerTransiciones(pedido.CodigoEstado, pedido.Columna3);
        NuevoEstado.SelectedIndex = NuevoEstado.Items.Count > 0 ? 0 : -1;
        BotonGuardar.IsEnabled = NuevoEstado.Items.Count > 0;
        if (NuevoEstado.Items.Count == 0)
        {
            Mensaje.Text = "Este pedido no tiene transiciones disponibles.";
        }
    }

    private static IReadOnlyList<OpcionEstadoPedido> ObtenerTransiciones(string estado, string entrega)
    {
        var siguientes = estado switch
        {
            "pending_payment" => new List<OpcionEstadoPedido> { new("queued", "Enviar a cola") },
            "queued" => [new("confirmed", "Confirmar pedido")],
            "confirmed" => [new("preparing", "Iniciar preparación")],
            "preparing" => entrega == "Recojo"
                ? [new("ready_for_pickup", "Listo para recojo"), new("awaiting_adjustment", "Esperar ajuste" )]
                : [new("ready_for_delivery", "Listo para delivery"), new("awaiting_adjustment", "Esperar ajuste")],
            "awaiting_adjustment" => [new("preparing", "Continuar preparación")],
            "ready_for_delivery" => [new("out_for_delivery", "Enviar a reparto")],
            "ready_for_pickup" or "out_for_delivery" => [new("completed", "Completar pedido")],
            _ => []
        };

        if (estado is not ("completed" or "cancelled"))
        {
            siguientes.Add(new OpcionEstadoPedido("cancelled", "Cancelar pedido"));
        }

        return siguientes;
    }

    private void AlCambiarEstadoSeleccionado(object sender, SelectionChangedEventArgs e)
    {
        SeccionCancelacion.Visibility = NuevoEstado.SelectedValue?.ToString() == "cancelled"
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (SeccionCancelacion.Visibility == Visibility.Visible && MotivoCancelacion.SelectedIndex < 0)
        {
            MotivoCancelacion.SelectedIndex = 0;
        }
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        var codigo = NuevoEstado.SelectedValue?.ToString();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            Mensaje.Text = "Selecciona el siguiente estado.";
            return;
        }

        var motivo = codigo == "cancelled" ? MotivoCancelacion.SelectedValue?.ToString() : null;
        if (codigo == "cancelled" && string.IsNullOrWhiteSpace(motivo))
        {
            Mensaje.Text = "Selecciona un motivo de cancelación.";
            return;
        }

        BotonGuardar.IsEnabled = false;
        Mensaje.Text = "Actualizando pedido...";
        try
        {
            var resultado = await _servicio.CambiarEstadoAsync(
                _tiendaId,
                _pedido.Id,
                codigo,
                motivo,
                Nota.Text);
            Mensaje.Text = resultado.Mensaje;
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

    private void AlCancelar(object sender, RoutedEventArgs e) => DialogResult = false;
}
