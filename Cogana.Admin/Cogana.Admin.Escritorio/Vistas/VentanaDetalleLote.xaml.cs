using System.Net.Http;
using System.Windows;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaDetalleLote : Window
{
    private readonly IServicioInventarioAdministrativo _servicio;
    private readonly Guid _tiendaId;
    private readonly Guid _loteId;
    private Guid _operacionId = Guid.NewGuid();
    private LoteInventarioDetalle? _lote;

    public VentanaDetalleLote(
        IServicioInventarioAdministrativo servicio,
        Guid tiendaId,
        Guid loteId)
    {
        _servicio = servicio;
        _tiendaId = tiendaId;
        _loteId = loteId;
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
        MensajeVentana.Text = "Cargando información desde Supabase...";
        BotonAjustar.IsEnabled = false;

        try
        {
            var loteTask = _servicio.ObtenerLoteAsync(_tiendaId, _loteId);
            var movimientosTask = _servicio.ObtenerMovimientosAsync(_tiendaId, _loteId);
            await Task.WhenAll(loteTask, movimientosTask);
            _lote = await loteTask;
            if (_lote is null)
            {
                MensajeVentana.Text = "El lote ya no está disponible o no pertenece a esta tienda.";
                return;
            }

            MostrarLote(_lote);
            var movimientos = await movimientosTask;
            ListaMovimientos.ItemsSource = movimientos;
            ResumenMovimientos.Text = movimientos.Count == 0
                ? "Este lote todavía no tiene movimientos registrados."
                : $"{movimientos.Count:N0} movimientos registrados, del más reciente al más antiguo.";
            BotonAjustar.IsEnabled = true;
            MensajeVentana.Text = string.Empty;
        }
        catch (HttpRequestException)
        {
            MensajeVentana.Text = "No fue posible cargar el lote. Revisa la conexión con Supabase.";
        }
    }

    private void MostrarLote(LoteInventarioDetalle lote)
    {
        TituloLote.Text = $"Lote {lote.Codigo}";
        SubtituloLote.Text = lote.Producto;
        TextoEstado.Text = TraducirEstado(lote.Estado);
        CantidadDisponible.Text = $"{lote.CantidadDisponible:N0} {AbreviarUnidad(lote.UnidadBase)}";
        FechaVencimiento.Text = lote.VenceEl?.ToString("dd/MM/yyyy") ?? "Sin vencimiento";
        NombreProveedor.Text = lote.Proveedor ?? "Sin proveedor";
        FechaRecepcion.Text = lote.RecibidoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        NotasLote.Text = string.IsNullOrWhiteSpace(lote.Notas) ? "Sin notas" : lote.Notas;
        CantidadContada.Text = lote.CantidadDisponible.ToString();

        if (lote.Estado is "blocked" or "expired" or "depleted")
        {
            EtiquetaEstado.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(238, 222, 218));
            TextoEstado.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(154, 68, 63));
        }
    }

    private async void AlAjustar(object sender, RoutedEventArgs e)
    {
        MensajeVentana.Text = ValidarAjuste() ?? string.Empty;
        if (!string.IsNullOrEmpty(MensajeVentana.Text)) return;

        var cantidad = long.Parse(CantidadContada.Text);
        var confirmacion = MessageBox.Show(
            $"La existencia del lote cambiará de {_lote!.CantidadDisponible:N0} a {cantidad:N0}. ¿Deseas continuar?",
            "Confirmar ajuste de inventario",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmacion != MessageBoxResult.Yes) return;

        BotonAjustar.IsEnabled = false;
        MensajeVentana.Text = "Registrando ajuste y movimiento...";
        var resultado = await _servicio.AjustarExistenciaAsync(
            _tiendaId,
            _loteId,
            new AjusteInventario(_operacionId, cantidad, MotivoAjuste.Text));
        MensajeVentana.Text = resultado.Mensaje;

        if (resultado.EsExitoso)
        {
            _operacionId = Guid.NewGuid();
            MotivoAjuste.Clear();
            await CargarDatosAsync();
        }
        else
        {
            BotonAjustar.IsEnabled = true;
        }
    }

    private string? ValidarAjuste()
    {
        if (_lote is null) return "El lote todavía no está disponible.";
        if (!long.TryParse(CantidadContada.Text, out var cantidad) || cantidad < 0)
            return "La cantidad contada debe ser un número igual o mayor que cero.";
        if (cantidad == _lote.CantidadDisponible)
            return "La cantidad contada debe ser diferente de la existencia actual.";
        if (string.IsNullOrWhiteSpace(MotivoAjuste.Text))
            return "Describe el motivo del ajuste.";
        return null;
    }

    private static string TraducirEstado(string estado) => estado switch
    {
        "available" => "Disponible",
        "blocked" => "Bloqueado",
        "expired" => "Vencido",
        "depleted" => "Agotado",
        _ => estado
    };

    private static string AbreviarUnidad(string unidad) => unidad switch
    {
        "gram" => "g",
        "milliliter" => "ml",
        _ => "unid."
    };

    private void AlCerrar(object sender, RoutedEventArgs e) => Close();
}
