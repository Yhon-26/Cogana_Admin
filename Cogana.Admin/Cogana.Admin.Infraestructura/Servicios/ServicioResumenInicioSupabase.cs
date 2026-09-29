using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioResumenInicioSupabase(ClienteSupabaseRest cliente)
    : IServicioResumenInicio
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ResultadoResumenInicio> ObtenerAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default)
    {
        if (!cliente.EstaConfigurado)
        {
            return ResultadoResumenInicio.Fallido(
                "La conexión con Supabase todavía no está configurada.");
        }

        try
        {
            var tiendaTexto = tiendaId.ToString("D");
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var limiteVencimiento = hoy.AddDays(30);

            var productosTask = ContarAsync(
                $"/rest/v1/products?select=id&store_id=eq.{tiendaTexto}&is_active=eq.true",
                cancellationToken);
            var categoriasTask = ContarAsync(
                $"/rest/v1/categories?select=id&store_id=eq.{tiendaTexto}&is_active=eq.true",
                cancellationToken);
            var lotesTask = ContarAsync(
                "/rest/v1/inventory_lots?select=id" +
                $"&store_id=eq.{tiendaTexto}" +
                "&status=eq.available" +
                "&loose_on_hand_quantity=gt.0" +
                $"&expires_on=gte.{hoy:yyyy-MM-dd}" +
                $"&expires_on=lte.{limiteVencimiento:yyyy-MM-dd}",
                cancellationToken);
            var pedidosTask = ContarAsync(
                $"/rest/v1/orders?select=id&store_id=eq.{tiendaTexto}&status=not.in.(completed,cancelled)",
                cancellationToken);
            var proveedoresTask = ContarAsync(
                $"/rest/v1/suppliers?select=id&store_id=eq.{tiendaTexto}&is_active=eq.true",
                cancellationToken);
            var tiendaTask = ObtenerTiendaAsync(tiendaId, cancellationToken);
            var estadoTask = ObtenerEstadoTiendaAsync(tiendaId, cancellationToken);
            var pedidosRecientesTask = ObtenerPedidosRecientesAsync(tiendaId, cancellationToken);

            await Task.WhenAll(
                productosTask,
                categoriasTask,
                lotesTask,
                pedidosTask,
                proveedoresTask,
                tiendaTask,
                estadoTask,
                pedidosRecientesTask);

            var tienda = await tiendaTask;
            var estado = await estadoTask;
            var resumen = new ResumenInicio(
                tienda?.Nombre ?? "Tienda Cogana",
                tienda?.Distrito ?? string.Empty,
                estado?.Estado ?? "unknown",
                estado?.MensajePublico,
                await productosTask,
                await categoriasTask,
                await lotesTask,
                await pedidosTask,
                await proveedoresTask,
                await pedidosRecientesTask,
                DateTimeOffset.Now);

            return ResultadoResumenInicio.Correcto(resumen);
        }
        catch (OperationCanceledException)
        {
            return ResultadoResumenInicio.Fallido("La actualización fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoResumenInicio.Fallido(
                "No fue posible consultar el resumen en Supabase.");
        }
        catch (JsonException)
        {
            return ResultadoResumenInicio.Fallido(
                "Supabase devolvió datos que no se pudieron procesar.");
        }
    }

    private async Task<int> ContarAsync(
        string ruta,
        CancellationToken cancellationToken)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, ruta);
        solicitud.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        solicitud.Headers.TryAddWithoutValidation("Range-Unit", "items");
        solicitud.Headers.TryAddWithoutValidation("Range", "0-0");

        using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
        respuesta.EnsureSuccessStatusCode();

        var total = respuesta.Content.Headers.ContentRange?.Length;
        if (total.HasValue)
        {
            return checked((int)total.Value);
        }

        if (respuesta.Headers.TryGetValues("Content-Range", out var valores) ||
            respuesta.Content.Headers.TryGetValues("Content-Range", out valores))
        {
            var valor = valores.FirstOrDefault();
            var separador = valor?.LastIndexOf('/') ?? -1;
            if (separador >= 0 &&
                long.TryParse(valor![(separador + 1)..], out var totalEncabezado))
            {
                return checked((int)totalEncabezado);
            }
        }

        using var documento = await JsonDocument.ParseAsync(
            await respuesta.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        return documento.RootElement.ValueKind == JsonValueKind.Array
            ? documento.RootElement.GetArrayLength()
            : 0;
    }

    private async Task<RespuestaTienda?> ObtenerTiendaAsync(
        Guid tiendaId,
        CancellationToken cancellationToken)
    {
        var ruta =
            "/rest/v1/stores?select=name,district" +
            $"&id=eq.{tiendaId:D}&limit=1";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var tiendas = await respuesta.Content.ReadFromJsonAsync<List<RespuestaTienda>>(
            OpcionesJson,
            cancellationToken);
        return tiendas?.FirstOrDefault();
    }

    private async Task<RespuestaEstadoTienda?> ObtenerEstadoTiendaAsync(
        Guid tiendaId,
        CancellationToken cancellationToken)
    {
        var ruta =
            "/rest/v1/store_operational_state?select=status,public_message" +
            $"&store_id=eq.{tiendaId:D}&limit=1";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var estados = await respuesta.Content.ReadFromJsonAsync<List<RespuestaEstadoTienda>>(
            OpcionesJson,
            cancellationToken);
        return estados?.FirstOrDefault();
    }

    private async Task<IReadOnlyList<PedidoRecienteResumen>> ObtenerPedidosRecientesAsync(
        Guid tiendaId,
        CancellationToken cancellationToken)
    {
        var ruta =
            "/rest/v1/orders" +
            "?select=id,order_number,customer_name,status,estimated_total_cents,final_total_cents,created_at" +
            $"&store_id=eq.{tiendaId:D}" +
            "&order=created_at.desc&limit=5";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var pedidos = await respuesta.Content.ReadFromJsonAsync<List<RespuestaPedido>>(
            OpcionesJson,
            cancellationToken) ?? [];

        return pedidos
            .Select(p => new PedidoRecienteResumen(
                p.Id,
                p.Numero,
                string.IsNullOrWhiteSpace(p.Cliente) ? "Cliente sin nombre" : p.Cliente,
                TraducirEstadoPedido(p.Estado),
                (p.TotalFinal ?? p.TotalEstimado) / 100m,
                p.Fecha))
            .ToList();
    }

    private static string TraducirEstadoPedido(string estado) => estado switch
    {
        "pending" => "Pendiente",
        "confirmed" => "Confirmado",
        "preparing" => "En preparación",
        "ready" => "Listo",
        "out_for_delivery" => "En reparto",
        "completed" => "Completado",
        "cancelled" => "Cancelado",
        _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(estado.Replace('_', ' '))
    };

    private sealed class RespuestaTienda
    {
        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;

        [JsonPropertyName("district")]
        public string? Distrito { get; init; }
    }

    private sealed class RespuestaEstadoTienda
    {
        [JsonPropertyName("status")]
        public string Estado { get; init; } = string.Empty;

        [JsonPropertyName("public_message")]
        public string? MensajePublico { get; init; }
    }

    private sealed class RespuestaPedido
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("order_number")]
        public string Numero { get; init; } = string.Empty;

        [JsonPropertyName("customer_name")]
        public string? Cliente { get; init; }

        [JsonPropertyName("status")]
        public string Estado { get; init; } = string.Empty;

        [JsonPropertyName("estimated_total_cents")]
        public decimal TotalEstimado { get; init; }

        [JsonPropertyName("final_total_cents")]
        public decimal? TotalFinal { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset Fecha { get; init; }
    }
}
