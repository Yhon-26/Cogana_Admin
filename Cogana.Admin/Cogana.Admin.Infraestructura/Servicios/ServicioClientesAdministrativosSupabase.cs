using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioClientesAdministrativosSupabase(ClienteSupabaseRest cliente)
    : IServicioClientesAdministrativos
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ClienteDetalle?> ObtenerClienteAsync(
        Guid tiendaId,
        Guid clienteId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/customers" +
            "?select=id,name,phone,phone_verified_at,email,status,cash_pickup_restricted_until,created_at" +
            $"&store_id=eq.{tiendaId:D}&id=eq.{clienteId:D}&limit=1";
        var clientes = await ConsultarAsync<RespuestaCliente>(ruta, cancellationToken);
        var cliente = clientes.FirstOrDefault();
        return cliente is null
            ? null
            : new ClienteDetalle(
                cliente.Id,
                cliente.Nombre,
                cliente.Telefono,
                cliente.TelefonoVerificadoEn,
                cliente.Correo,
                cliente.Estado,
                cliente.EfectivoRestringidoHasta,
                cliente.CreadoEn);
    }

    public async Task<IReadOnlyList<DireccionClienteDetalle>> ObtenerDireccionesAsync(
        Guid tiendaId,
        Guid clienteId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/customer_addresses" +
            "?select=id,label,address_line,district,reference,latitude,longitude,is_default" +
            $"&store_id=eq.{tiendaId:D}&customer_id=eq.{clienteId:D}&order=is_default.desc,created_at.desc";
        var direcciones = await ConsultarAsync<RespuestaDireccion>(ruta, cancellationToken);
        return direcciones.Select(direccion => new DireccionClienteDetalle(
            direccion.Id,
            direccion.Etiqueta,
            direccion.Direccion,
            direccion.Distrito,
            direccion.Referencia,
            direccion.Latitud,
            direccion.Longitud,
            direccion.EsPrincipal)).ToList();
    }

    public async Task<PreferenciasClienteDetalle?> ObtenerPreferenciasAsync(
        Guid tiendaId,
        Guid clienteId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/customer_preferences" +
            "?select=order_notifications,promotion_notifications,marketing_consent,substitution_default" +
            $"&store_id=eq.{tiendaId:D}&customer_id=eq.{clienteId:D}&limit=1";
        var preferencias = await ConsultarAsync<RespuestaPreferencias>(ruta, cancellationToken);
        var preferencia = preferencias.FirstOrDefault();
        return preferencia is null
            ? null
            : new PreferenciasClienteDetalle(
                preferencia.NotificacionesPedido,
                preferencia.NotificacionesPromociones,
                preferencia.ConsentimientoMarketing,
                preferencia.SustitucionPredeterminada);
    }

    public async Task<IReadOnlyList<PedidoClienteResumen>> ObtenerPedidosAsync(
        Guid tiendaId,
        Guid clienteId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/orders" +
            "?select=id,order_number,fulfillment_type,status,payment_status,estimated_total_cents,final_total_cents,created_at" +
            $"&store_id=eq.{tiendaId:D}&customer_id=eq.{clienteId:D}&order=created_at.desc&limit=100";
        var pedidos = await ConsultarAsync<RespuestaPedido>(ruta, cancellationToken);
        return pedidos.Select(pedido => new PedidoClienteResumen(
            pedido.Id,
            pedido.Numero,
            TraducirEntrega(pedido.Entrega),
            TraducirEstadoPedido(pedido.Estado),
            TraducirPago(pedido.EstadoPago),
            pedido.TotalFinalCentimos ?? pedido.TotalEstimadoCentimos,
            pedido.CreadoEn)).ToList();
    }

    private async Task<List<T>> ConsultarAsync<T>(string ruta, CancellationToken cancellationToken)
    {
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<List<T>>(OpcionesJson, cancellationToken) ?? [];
    }

    private static string TraducirEntrega(string valor) => valor switch
    {
        "pickup" => "Recojo",
        "delivery" => "Delivery",
        _ => valor
    };

    private static string TraducirEstadoPedido(string valor) => valor switch
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
        _ => valor
    };

    private static string TraducirPago(string valor) => valor switch
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
        _ => valor
    };

    private sealed class RespuestaCliente
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("name")] public string Nombre { get; init; } = string.Empty;
        [JsonPropertyName("phone")] public string Telefono { get; init; } = string.Empty;
        [JsonPropertyName("phone_verified_at")] public DateTimeOffset? TelefonoVerificadoEn { get; init; }
        [JsonPropertyName("email")] public string? Correo { get; init; }
        [JsonPropertyName("status")] public string Estado { get; init; } = string.Empty;
        [JsonPropertyName("cash_pickup_restricted_until")] public DateTimeOffset? EfectivoRestringidoHasta { get; init; }
        [JsonPropertyName("created_at")] public DateTimeOffset CreadoEn { get; init; }
    }

    private sealed class RespuestaDireccion
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("label")] public string Etiqueta { get; init; } = string.Empty;
        [JsonPropertyName("address_line")] public string Direccion { get; init; } = string.Empty;
        [JsonPropertyName("district")] public string Distrito { get; init; } = string.Empty;
        [JsonPropertyName("reference")] public string? Referencia { get; init; }
        [JsonPropertyName("latitude")] public decimal? Latitud { get; init; }
        [JsonPropertyName("longitude")] public decimal? Longitud { get; init; }
        [JsonPropertyName("is_default")] public bool EsPrincipal { get; init; }
    }

    private sealed class RespuestaPreferencias
    {
        [JsonPropertyName("order_notifications")] public bool NotificacionesPedido { get; init; }
        [JsonPropertyName("promotion_notifications")] public bool NotificacionesPromociones { get; init; }
        [JsonPropertyName("marketing_consent")] public bool ConsentimientoMarketing { get; init; }
        [JsonPropertyName("substitution_default")] public string SustitucionPredeterminada { get; init; } = string.Empty;
    }

    private sealed class RespuestaPedido
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("order_number")] public string Numero { get; init; } = string.Empty;
        [JsonPropertyName("fulfillment_type")] public string Entrega { get; init; } = string.Empty;
        [JsonPropertyName("status")] public string Estado { get; init; } = string.Empty;
        [JsonPropertyName("payment_status")] public string EstadoPago { get; init; } = string.Empty;
        [JsonPropertyName("estimated_total_cents")] public long TotalEstimadoCentimos { get; init; }
        [JsonPropertyName("final_total_cents")] public long? TotalFinalCentimos { get; init; }
        [JsonPropertyName("created_at")] public DateTimeOffset CreadoEn { get; init; }
    }
}
