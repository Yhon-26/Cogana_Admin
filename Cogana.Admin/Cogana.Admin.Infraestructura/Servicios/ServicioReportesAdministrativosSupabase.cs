using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioReportesAdministrativosSupabase(ClienteSupabaseRest cliente)
    : IServicioReportesAdministrativos
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ReporteAdministrativo> ObtenerAsync(
        Guid tiendaId,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken cancellationToken = default)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/rpc/get_admin_report")
        {
            Content = JsonContent.Create(new
            {
                p_store_id = tiendaId,
                p_start_date = fechaInicio.ToString("yyyy-MM-dd"),
                p_end_date = fechaFin.ToString("yyyy-MM-dd")
            })
        };
        using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var reporte = await respuesta.Content.ReadFromJsonAsync<RespuestaReporte>(OpcionesJson, cancellationToken)
            ?? throw new JsonException("Supabase no devolvió el reporte.");

        return new ReporteAdministrativo(
            LeerFecha(reporte.FechaInicio),
            LeerFecha(reporte.FechaFin),
            new ResumenReporte(
                reporte.Resumen.Pedidos,
                reporte.Resumen.Completados,
                reporte.Resumen.Cancelados,
                reporte.Resumen.Activos,
                reporte.Resumen.VentasCentimos,
                reporte.Resumen.TicketPromedioCentimos,
                reporte.Resumen.DescuentosCentimos,
                reporte.Resumen.ProductosActivos,
                reporte.Resumen.ProductosStockBajo,
                reporte.Resumen.LotesPorVencer,
                reporte.Resumen.LotesVencidos),
            reporte.VentasDiarias.Select(item => new VentaDiariaReporte(
                LeerFecha(item.Fecha), item.Pedidos, item.VentasCentimos)).ToList(),
            reporte.EstadosPedido.Select(item => new EstadoPedidoReporte(
                item.Estado, item.Pedidos, item.TotalCentimos)).ToList(),
            reporte.ProductosVendidos.Select(item => new ProductoVendidoReporte(
                item.ProductoId, item.Producto, item.Cantidad, item.VentasCentimos, item.Pedidos)).ToList(),
            reporte.MetodosPago.Select(item => new MetodoPagoReporte(
                item.Metodo, item.Pedidos, item.TotalCentimos)).ToList(),
            reporte.StockBajo.Select(item => new StockBajoReporte(
                item.ProductoId, item.Producto, item.Unidad, item.StockActual, item.StockMinimo)).ToList(),
            reporte.LotesPorVencer.Select(item => new LoteVencimientoReporte(
                item.LoteId, item.Lote, item.Producto, LeerFecha(item.FechaVencimiento), item.Cantidad, item.Estado)).ToList());
    }

    private static DateOnly LeerFecha(string valor) =>
        DateOnly.TryParse(valor, out var fecha) ? fecha : throw new JsonException("Fecha no válida en el reporte.");

    private sealed class RespuestaReporte
    {
        [JsonPropertyName("start_date")] public string FechaInicio { get; init; } = string.Empty;
        [JsonPropertyName("end_date")] public string FechaFin { get; init; } = string.Empty;
        [JsonPropertyName("summary")] public RespuestaResumen Resumen { get; init; } = new();
        [JsonPropertyName("sales_by_day")] public List<RespuestaVentaDiaria> VentasDiarias { get; init; } = [];
        [JsonPropertyName("order_statuses")] public List<RespuestaEstadoPedido> EstadosPedido { get; init; } = [];
        [JsonPropertyName("top_products")] public List<RespuestaProductoVendido> ProductosVendidos { get; init; } = [];
        [JsonPropertyName("payment_methods")] public List<RespuestaMetodoPago> MetodosPago { get; init; } = [];
        [JsonPropertyName("low_stock")] public List<RespuestaStockBajo> StockBajo { get; init; } = [];
        [JsonPropertyName("expiring_lots")] public List<RespuestaLote> LotesPorVencer { get; init; } = [];
    }

    private sealed class RespuestaResumen
    {
        [JsonPropertyName("orders_count")] public long Pedidos { get; init; }
        [JsonPropertyName("completed_count")] public long Completados { get; init; }
        [JsonPropertyName("cancelled_count")] public long Cancelados { get; init; }
        [JsonPropertyName("active_count")] public long Activos { get; init; }
        [JsonPropertyName("sales_cents")] public long VentasCentimos { get; init; }
        [JsonPropertyName("average_ticket_cents")] public long TicketPromedioCentimos { get; init; }
        [JsonPropertyName("discount_cents")] public long DescuentosCentimos { get; init; }
        [JsonPropertyName("active_products_count")] public long ProductosActivos { get; init; }
        [JsonPropertyName("low_stock_count")] public long ProductosStockBajo { get; init; }
        [JsonPropertyName("expiring_lots_count")] public long LotesPorVencer { get; init; }
        [JsonPropertyName("expired_lots_count")] public long LotesVencidos { get; init; }
    }

    private sealed class RespuestaVentaDiaria
    {
        [JsonPropertyName("date")] public string Fecha { get; init; } = string.Empty;
        [JsonPropertyName("orders_count")] public int Pedidos { get; init; }
        [JsonPropertyName("sales_cents")] public long VentasCentimos { get; init; }
    }

    private sealed class RespuestaEstadoPedido
    {
        [JsonPropertyName("status")] public string Estado { get; init; } = string.Empty;
        [JsonPropertyName("orders_count")] public int Pedidos { get; init; }
        [JsonPropertyName("total_cents")] public long TotalCentimos { get; init; }
    }

    private sealed class RespuestaProductoVendido
    {
        [JsonPropertyName("product_id")] public Guid ProductoId { get; init; }
        [JsonPropertyName("product_name")] public string Producto { get; init; } = string.Empty;
        [JsonPropertyName("quantity")] public long Cantidad { get; init; }
        [JsonPropertyName("revenue_cents")] public long VentasCentimos { get; init; }
        [JsonPropertyName("orders_count")] public int Pedidos { get; init; }
    }

    private sealed class RespuestaMetodoPago
    {
        [JsonPropertyName("method")] public string Metodo { get; init; } = string.Empty;
        [JsonPropertyName("orders_count")] public int Pedidos { get; init; }
        [JsonPropertyName("total_cents")] public long TotalCentimos { get; init; }
    }

    private sealed class RespuestaStockBajo
    {
        [JsonPropertyName("product_id")] public Guid ProductoId { get; init; }
        [JsonPropertyName("product_name")] public string Producto { get; init; } = string.Empty;
        [JsonPropertyName("base_unit")] public string Unidad { get; init; } = string.Empty;
        [JsonPropertyName("current_quantity")] public long StockActual { get; init; }
        [JsonPropertyName("minimum_quantity")] public long StockMinimo { get; init; }
    }

    private sealed class RespuestaLote
    {
        [JsonPropertyName("lot_id")] public Guid LoteId { get; init; }
        [JsonPropertyName("lot_code")] public string Lote { get; init; } = string.Empty;
        [JsonPropertyName("product_name")] public string Producto { get; init; } = string.Empty;
        [JsonPropertyName("expires_on")] public string FechaVencimiento { get; init; } = string.Empty;
        [JsonPropertyName("quantity")] public long Cantidad { get; init; }
        [JsonPropertyName("status")] public string Estado { get; init; } = string.Empty;
    }
}
