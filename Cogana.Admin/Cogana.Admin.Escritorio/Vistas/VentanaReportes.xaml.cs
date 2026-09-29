using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Microsoft.Win32;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaReportes : Window
{
    private readonly IServicioReportesAdministrativos _servicio;
    private readonly Guid _tiendaId;
    private readonly CultureInfo _cultura = new("es-PE");
    private ReporteAdministrativo? _reporte;

    public VentanaReportes(IServicioReportesAdministrativos servicio, Guid tiendaId)
    {
        _servicio = servicio;
        _tiendaId = tiendaId;
        InitializeComponent();
        Loaded += AlCargar;
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargar;
        AplicarUltimosDias(30);
        FechaInicio.IsEnabled = false;
        FechaFin.IsEnabled = false;
        await ConsultarAsync();
    }

    private void AlCambiarPeriodo(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || Periodo.SelectedValue is not string periodo) return;
        var hoy = DateTime.Today;
        switch (periodo)
        {
            case "7": AplicarUltimosDias(7); break;
            case "30": AplicarUltimosDias(30); break;
            case "month":
                FechaInicio.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
                FechaFin.SelectedDate = hoy;
                break;
        }

        var personalizado = periodo == "custom";
        FechaInicio.IsEnabled = personalizado;
        FechaFin.IsEnabled = personalizado;
    }

    private void AplicarUltimosDias(int dias)
    {
        FechaFin.SelectedDate = DateTime.Today;
        FechaInicio.SelectedDate = DateTime.Today.AddDays(-(dias - 1));
    }

    private async void AlConsultar(object sender, RoutedEventArgs e) => await ConsultarAsync();

    private async Task ConsultarAsync()
    {
        if (FechaInicio.SelectedDate is null || FechaFin.SelectedDate is null)
        {
            MensajeFormulario.Text = "Selecciona la fecha inicial y final.";
            return;
        }
        if (FechaFin.SelectedDate < FechaInicio.SelectedDate)
        {
            MensajeFormulario.Text = "La fecha final debe ser igual o posterior a la fecha inicial.";
            return;
        }
        if ((FechaFin.SelectedDate.Value - FechaInicio.SelectedDate.Value).TotalDays > 366)
        {
            MensajeFormulario.Text = "El periodo no puede superar 367 días.";
            return;
        }

        BotonConsultar.IsEnabled = false;
        BotonExportar.IsEnabled = false;
        MensajeFormulario.Text = "Generando reporte desde Supabase...";
        try
        {
            _reporte = await _servicio.ObtenerAsync(
                _tiendaId,
                DateOnly.FromDateTime(FechaInicio.SelectedDate.Value),
                DateOnly.FromDateTime(FechaFin.SelectedDate.Value));
            Mostrar(_reporte);
            BotonExportar.IsEnabled = true;
            MensajeFormulario.Text = string.Empty;
        }
        catch (HttpRequestException)
        {
            MensajeFormulario.Text = "No fue posible generar el reporte. Revisa la conexión y tus permisos.";
        }
        catch (JsonException)
        {
            MensajeFormulario.Text = "Supabase devolvió un reporte con un formato inesperado.";
        }
        finally
        {
            BotonConsultar.IsEnabled = true;
        }
    }

    private void Mostrar(ReporteAdministrativo reporte)
    {
        TotalVentas.Text = Moneda(reporte.Resumen.VentasCentimos);
        TotalPedidos.Text = reporte.Resumen.Pedidos.ToString("N0", _cultura);
        TicketPromedio.Text = Moneda(reporte.Resumen.TicketPromedioCentimos);
        TotalStockBajo.Text = reporte.Resumen.ProductosStockBajo.ToString("N0", _cultura);
        TotalVencimientos.Text = (reporte.Resumen.LotesPorVencer + reporte.Resumen.LotesVencidos).ToString("N0", _cultura);
        ResumenPeriodo.Text =
            $"Del {reporte.FechaInicio:dd/MM/yyyy} al {reporte.FechaFin:dd/MM/yyyy} · " +
            $"{reporte.Resumen.Completados:N0} completados · {reporte.Resumen.Activos:N0} activos · " +
            $"{reporte.Resumen.Cancelados:N0} cancelados · descuentos {Moneda(reporte.Resumen.DescuentosCentimos)}";

        TablaVentasDiarias.ItemsSource = reporte.VentasDiarias.Select(item => new
        {
            Fecha = item.Fecha.ToString("dd/MM/yyyy"),
            item.Pedidos,
            Ventas = Moneda(item.VentasCentimos)
        }).ToList();
        TablaEstados.ItemsSource = reporte.EstadosPedido.Select(item => new
        {
            Estado = TraducirEstado(item.Estado),
            item.Pedidos,
            Total = Moneda(item.TotalCentimos)
        }).ToList();
        TablaProductos.ItemsSource = reporte.ProductosVendidos.Select(item => new
        {
            item.Producto,
            Cantidad = item.Cantidad.ToString("N0", _cultura),
            item.Pedidos,
            Ventas = Moneda(item.VentasCentimos)
        }).ToList();
        TablaPagos.ItemsSource = reporte.MetodosPago.Select(item => new
        {
            Metodo = TraducirMetodo(item.Metodo),
            item.Pedidos,
            Total = Moneda(item.TotalCentimos)
        }).ToList();
        TablaStockBajo.ItemsSource = reporte.StockBajo.Select(item => new
        {
            item.Producto,
            Unidad = item.Unidad,
            Actual = item.StockActual.ToString("N0", _cultura),
            Minimo = item.StockMinimo.ToString("N0", _cultura)
        }).ToList();
        TablaVencimientos.ItemsSource = reporte.LotesPorVencer.Select(item => new
        {
            item.Lote,
            item.Producto,
            Fecha = item.FechaVencimiento.ToString("dd/MM/yyyy"),
            Cantidad = item.Cantidad.ToString("N0", _cultura),
            Estado = item.Estado == "expired" ? "Vencido" : "Por vencer"
        }).ToList();
    }

    private void AlExportar(object sender, RoutedEventArgs e)
    {
        if (_reporte is null) return;
        var dialogo = new SaveFileDialog
        {
            Title = "Exportar reporte de Cogana",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = $"reporte-cogana-{_reporte.FechaInicio:yyyyMMdd}-{_reporte.FechaFin:yyyyMMdd}.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };
        if (dialogo.ShowDialog(this) != true) return;

        var lineas = CrearCsv(_reporte);
        File.WriteAllLines(dialogo.FileName, lineas, new UTF8Encoding(true));
        MessageBox.Show("Reporte exportado correctamente.", "Exportación completada",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private List<string> CrearCsv(ReporteAdministrativo reporte)
    {
        var lineas = new List<string>
        {
            Csv("REPORTE COGANA", $"{reporte.FechaInicio:dd/MM/yyyy} - {reporte.FechaFin:dd/MM/yyyy}"),
            Csv("RESUMEN", "Valor"),
            Csv("Ventas", Moneda(reporte.Resumen.VentasCentimos)),
            Csv("Pedidos", reporte.Resumen.Pedidos.ToString()),
            Csv("Completados", reporte.Resumen.Completados.ToString()),
            Csv("Activos", reporte.Resumen.Activos.ToString()),
            Csv("Cancelados", reporte.Resumen.Cancelados.ToString()),
            Csv("Ticket promedio", Moneda(reporte.Resumen.TicketPromedioCentimos)),
            string.Empty,
            Csv("VENTAS DIARIAS", "Pedidos", "Ventas")
        };
        lineas.AddRange(reporte.VentasDiarias.Select(item => Csv(
            item.Fecha.ToString("dd/MM/yyyy"), item.Pedidos.ToString(), Moneda(item.VentasCentimos))));
        lineas.Add(string.Empty);
        lineas.Add(Csv("PRODUCTOS VENDIDOS", "Cantidad", "Pedidos", "Ventas"));
        lineas.AddRange(reporte.ProductosVendidos.Select(item => Csv(
            item.Producto, item.Cantidad.ToString(), item.Pedidos.ToString(), Moneda(item.VentasCentimos))));
        lineas.Add(string.Empty);
        lineas.Add(Csv("STOCK BAJO", "Unidad", "Stock actual", "Stock mínimo"));
        lineas.AddRange(reporte.StockBajo.Select(item => Csv(
            item.Producto, item.Unidad, item.StockActual.ToString(), item.StockMinimo.ToString())));
        lineas.Add(string.Empty);
        lineas.Add(Csv("LOTES POR VENCER", "Producto", "Vencimiento", "Cantidad", "Estado"));
        lineas.AddRange(reporte.LotesPorVencer.Select(item => Csv(
            item.Lote, item.Producto, item.FechaVencimiento.ToString("dd/MM/yyyy"),
            item.Cantidad.ToString(), item.Estado == "expired" ? "Vencido" : "Por vencer")));
        return lineas;
    }

    private string Moneda(long centimos) => (centimos / 100m).ToString("C2", _cultura);
    private static string Csv(params string[] valores) => string.Join(';', valores.Select(valor => $"\"{valor.Replace("\"", "\"\"")}\""));
    private static string TraducirMetodo(string metodo) => metodo switch
    {
        "cash" => "Efectivo",
        "yape" => "Yape",
        "plin" => "Plin",
        "card" => "Tarjeta",
        "not_specified" => "Sin especificar",
        _ => metodo
    };
    private static string TraducirEstado(string estado) => estado switch
    {
        "draft" => "Borrador",
        "placed" => "Recibido",
        "confirmed" => "Confirmado",
        "preparing" => "En preparación",
        "ready" => "Listo",
        "out_for_delivery" => "En reparto",
        "completed" => "Completado",
        "cancelled" => "Cancelado",
        _ => estado
    };

    private void AlCerrar(object sender, RoutedEventArgs e) => Close();
}
