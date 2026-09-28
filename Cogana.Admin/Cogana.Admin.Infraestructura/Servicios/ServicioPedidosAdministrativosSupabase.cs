using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioPedidosAdministrativosSupabase(ClienteSupabaseRest cliente)
    : IServicioPedidosAdministrativos
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PedidoDetalle?> ObtenerPedidoAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/orders" +
            "?select=id,order_number,source,fulfillment_type,status,payment_status,payment_method," +
            "customer_name,customer_phone,customer_email,delivery_address,scheduled_for,estimated_ready_at," +
            "estimated_subtotal_cents,estimated_discount_cents,delivery_fee_cents,delivery_discount_cents," +
            "estimated_total_cents,final_subtotal_cents,final_discount_cents,final_total_cents,notes," +
            "cancellation_reason_code,created_at" +
            $"&store_id=eq.{tiendaId:D}&id=eq.{pedidoId:D}&limit=1";
        var pedidos = await ConsultarAsync<RespuestaPedido>(ruta, cancellationToken);
        var pedido = pedidos.FirstOrDefault();
        return pedido is null
            ? null
            : new PedidoDetalle(
                pedido.Id,
                pedido.Numero,
                TraducirOrigen(pedido.Origen),
                TraducirEntrega(pedido.TipoEntrega),
                pedido.Estado,
                pedido.EstadoPago,
                TraducirMetodoPago(pedido.MetodoPago),
                pedido.NombreCliente,
                pedido.TelefonoCliente,
                pedido.CorreoCliente,
                CrearDireccion(pedido.DireccionEntrega),
                LeerJson(pedido.DireccionEntrega, "reference", "referencia"),
                pedido.ProgramadoPara,
                pedido.EstimadoListoEn,
                pedido.SubtotalEstimadoCentimos,
                pedido.DescuentoEstimadoCentimos,
                pedido.TarifaEntregaCentimos,
                pedido.DescuentoEntregaCentimos,
                pedido.TotalEstimadoCentimos,
                pedido.SubtotalFinalCentimos,
                pedido.DescuentoFinalCentimos,
                pedido.TotalFinalCentimos,
                pedido.Notas,
                pedido.MotivoCancelacion,
                pedido.CreadoEn);
    }

    public async Task<IReadOnlyList<ItemPedidoDetalle>> ObtenerItemsAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/order_items" +
            "?select=id,product_name_snapshot,presentation_name_snapshot,base_unit_snapshot,request_mode," +
            "requested_quantity,requested_money_cents,estimated_quantity,prepared_quantity," +
            "estimated_line_cents,estimated_discount_cents,final_line_cents,final_discount_cents,substitution_policy" +
            $"&store_id=eq.{tiendaId:D}&order_id=eq.{pedidoId:D}&order=created_at.asc";
        var items = await ConsultarAsync<RespuestaItem>(ruta, cancellationToken);
        return items.Select(item => new ItemPedidoDetalle(
            item.Id,
            item.Producto,
            item.Presentacion,
            item.UnidadBase,
            item.ModoSolicitud,
            item.CantidadSolicitada,
            item.ImporteSolicitadoCentimos,
            item.CantidadEstimada,
            item.CantidadPreparada,
            item.ImporteEstimadoCentimos,
            item.DescuentoEstimadoCentimos,
            item.ImporteFinalCentimos,
            item.DescuentoFinalCentimos,
            item.PoliticaSustitucion)).ToList();
    }

    public async Task<IReadOnlyList<HistorialEstadoPedido>> ObtenerHistorialAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/order_status_history?select=id,from_status,to_status,reason_code,note,created_at" +
            $"&store_id=eq.{tiendaId:D}&order_id=eq.{pedidoId:D}&order=created_at.desc";
        var historial = await ConsultarAsync<RespuestaHistorial>(ruta, cancellationToken);
        return historial.Select(item => new HistorialEstadoPedido(
            item.Id,
            item.EstadoAnterior is null ? null : TraducirEstado(item.EstadoAnterior),
            TraducirEstado(item.EstadoNuevo),
            TraducirMotivo(item.Motivo),
            item.Nota,
            item.CreadoEn)).ToList();
    }

    public async Task<IReadOnlyList<PagoPedidoResumen>> ObtenerPagosAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/payment_transactions" +
            "?select=id,transaction_type,method,provider,provider_reference,amount_cents,status,failure_message,created_at" +
            $"&store_id=eq.{tiendaId:D}&order_id=eq.{pedidoId:D}&order=created_at.desc";
        var pagos = await ConsultarAsync<RespuestaPago>(ruta, cancellationToken);
        return pagos.Select(pago => new PagoPedidoResumen(
            pago.Id,
            TraducirTipoTransaccion(pago.Tipo),
            TraducirMetodoPago(pago.Metodo),
            pago.Proveedor,
            pago.Referencia,
            pago.ImporteCentimos,
            TraducirEstadoPago(pago.Estado),
            pago.Error,
            pago.CreadoEn)).ToList();
    }

    public async Task<EntregaPedidoDetalle?> ObtenerEntregaAsync(
        Guid tiendaId,
        Guid pedidoId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/delivery_assignments" +
            "?select=id,status,recipient_name,notes,assigned_at,started_at,delivered_at," +
            "delivery_events(id,event_type,note,created_at)" +
            $"&store_id=eq.{tiendaId:D}&order_id=eq.{pedidoId:D}&limit=1";
        var entregas = await ConsultarAsync<RespuestaEntrega>(ruta, cancellationToken);
        var entrega = entregas.FirstOrDefault();
        return entrega is null
            ? null
            : new EntregaPedidoDetalle(
                entrega.Id,
                TraducirEstadoEntrega(entrega.Estado),
                entrega.Destinatario,
                entrega.Notas,
                entrega.AsignadoEn,
                entrega.IniciadoEn,
                entrega.EntregadoEn,
                entrega.Eventos
                    .OrderByDescending(evento => evento.CreadoEn)
                    .Select(evento => new EventoEntregaResumen(
                        evento.Id,
                        TraducirEventoEntrega(evento.Tipo),
                        evento.Nota,
                        evento.CreadoEn))
                    .ToList());
    }

    public async Task<ResultadoOperacion> CambiarEstadoAsync(
        Guid tiendaId,
        Guid pedidoId,
        string nuevoEstado,
        string? motivo,
        string? nota,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post,
                "/rest/v1/rpc/transition_order")
            {
                Content = JsonContent.Create(new
                {
                    p_store_id = tiendaId,
                    p_order_id = pedidoId,
                    p_to_status = nuevoEstado,
                    p_operation_id = Guid.NewGuid(),
                    p_reason_code = Limpiar(motivo),
                    p_note = Limpiar(nota)
                })
            };
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            if (respuesta.IsSuccessStatusCode)
            {
                return ResultadoOperacion.Correcta("Estado del pedido actualizado correctamente.");
            }

            try
            {
                var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(
                    OpcionesJson,
                    cancellationToken);
                return ResultadoOperacion.Fallida(
                    string.IsNullOrWhiteSpace(error?.Mensaje)
                        ? $"Supabase rechazó el cambio ({(int)respuesta.StatusCode})."
                        : error.Mensaje);
            }
            catch (JsonException)
            {
                return ResultadoOperacion.Fallida(
                    $"Supabase rechazó el cambio ({(int)respuesta.StatusCode}).");
            }
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida(
                "No fue posible cambiar el estado del pedido.");
        }
    }

    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private async Task<List<T>> ConsultarAsync<T>(string ruta, CancellationToken cancellationToken)
    {
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<List<T>>(OpcionesJson, cancellationToken) ?? [];
    }

    private static string? CrearDireccion(JsonElement? valor)
    {
        var direccion = LeerJson(valor, "address", "address_line", "direccion");
        var distrito = LeerJson(valor, "district", "distrito");
        var partes = new[] { direccion, distrito }.Where(parte => !string.IsNullOrWhiteSpace(parte));
        var resultado = string.Join(", ", partes);
        return string.IsNullOrWhiteSpace(resultado) ? null : resultado;
    }

    private static string? LeerJson(JsonElement? valor, params string[] nombres)
    {
        if (valor is not JsonElement elemento || elemento.ValueKind != JsonValueKind.Object) return null;
        foreach (var nombre in nombres)
        {
            if (elemento.TryGetProperty(nombre, out var propiedad) && propiedad.ValueKind == JsonValueKind.String)
            {
                return Limpiar(propiedad.GetString());
            }
        }
        return null;
    }

    private static string TraducirOrigen(string valor) => valor switch
    {
        "web" => "Web",
        "android" => "Aplicación móvil",
        "phone" => "Teléfono",
        "whatsapp" => "WhatsApp",
        _ => valor
    };

    private static string TraducirEntrega(string valor) => valor == "delivery" ? "Delivery" : "Recojo";
    private static string TraducirMetodoPago(string valor) => valor switch
    {
        "cash" => "Efectivo",
        "yape" => "Yape",
        "plin" => "Plin",
        "card" => "Tarjeta",
        _ => valor
    };

    private static string TraducirEstado(string valor) => valor switch
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

    private static string? TraducirMotivo(string? valor) => valor switch
    {
        "customer_request" => "Solicitud del cliente",
        "out_of_stock" => "Producto sin stock",
        "store_closed" => "Tienda cerrada",
        "other" => "Otro motivo",
        null => null,
        _ => valor
    };

    private static string TraducirTipoTransaccion(string valor) => valor switch
    {
        "charge" => "Cobro",
        "refund" => "Reembolso",
        "adjustment" => "Ajuste",
        _ => valor
    };

    private static string TraducirEstadoPago(string valor) => valor switch
    {
        "pending" => "Pendiente",
        "requires_action" => "Requiere acción",
        "processing" => "Procesando",
        "succeeded" => "Completado",
        "failed" => "Fallido",
        "cancelled" => "Cancelado",
        "expired" => "Vencido",
        _ => valor
    };

    private static string TraducirEstadoEntrega(string valor) => valor switch
    {
        "assigned" => "Asignado",
        "accepted" => "Aceptado",
        "picked_up" => "Recogido",
        "delivering" => "En reparto",
        "delivered" => "Entregado",
        "failed" => "Fallido",
        "cancelled" => "Cancelado",
        _ => valor
    };

    private static string TraducirEventoEntrega(string valor) => valor switch
    {
        "assigned" => "Asignado",
        "accepted" => "Aceptado",
        "picked_up" => "Recogido",
        "departed" => "Salió de tienda",
        "arrived" => "Llegó al destino",
        "delivered" => "Entregado",
        "recipient_absent" => "Destinatario ausente",
        "address_problem" => "Problema con dirección",
        "failed" => "Entrega fallida",
        "cancelled" => "Cancelado",
        _ => valor
    };

    private sealed class RespuestaPedido
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("order_number")] public string Numero { get; init; } = string.Empty;
        [JsonPropertyName("source")] public string Origen { get; init; } = string.Empty;
        [JsonPropertyName("fulfillment_type")] public string TipoEntrega { get; init; } = string.Empty;
        [JsonPropertyName("status")] public string Estado { get; init; } = string.Empty;
        [JsonPropertyName("payment_status")] public string EstadoPago { get; init; } = string.Empty;
        [JsonPropertyName("payment_method")] public string MetodoPago { get; init; } = string.Empty;
        [JsonPropertyName("customer_name")] public string NombreCliente { get; init; } = string.Empty;
        [JsonPropertyName("customer_phone")] public string TelefonoCliente { get; init; } = string.Empty;
        [JsonPropertyName("customer_email")] public string? CorreoCliente { get; init; }
        [JsonPropertyName("delivery_address")] public JsonElement? DireccionEntrega { get; init; }
        [JsonPropertyName("scheduled_for")] public DateTimeOffset? ProgramadoPara { get; init; }
        [JsonPropertyName("estimated_ready_at")] public DateTimeOffset? EstimadoListoEn { get; init; }
        [JsonPropertyName("estimated_subtotal_cents")] public long SubtotalEstimadoCentimos { get; init; }
        [JsonPropertyName("estimated_discount_cents")] public long DescuentoEstimadoCentimos { get; init; }
        [JsonPropertyName("delivery_fee_cents")] public long TarifaEntregaCentimos { get; init; }
        [JsonPropertyName("delivery_discount_cents")] public long DescuentoEntregaCentimos { get; init; }
        [JsonPropertyName("estimated_total_cents")] public long TotalEstimadoCentimos { get; init; }
        [JsonPropertyName("final_subtotal_cents")] public long? SubtotalFinalCentimos { get; init; }
        [JsonPropertyName("final_discount_cents")] public long? DescuentoFinalCentimos { get; init; }
        [JsonPropertyName("final_total_cents")] public long? TotalFinalCentimos { get; init; }
        [JsonPropertyName("notes")] public string? Notas { get; init; }
        [JsonPropertyName("cancellation_reason_code")] public string? MotivoCancelacion { get; init; }
        [JsonPropertyName("created_at")] public DateTimeOffset CreadoEn { get; init; }
    }

    private sealed class RespuestaItem
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("product_name_snapshot")] public string Producto { get; init; } = string.Empty;
        [JsonPropertyName("presentation_name_snapshot")] public string Presentacion { get; init; } = string.Empty;
        [JsonPropertyName("base_unit_snapshot")] public string UnidadBase { get; init; } = string.Empty;
        [JsonPropertyName("request_mode")] public string ModoSolicitud { get; init; } = string.Empty;
        [JsonPropertyName("requested_quantity")] public long? CantidadSolicitada { get; init; }
        [JsonPropertyName("requested_money_cents")] public long? ImporteSolicitadoCentimos { get; init; }
        [JsonPropertyName("estimated_quantity")] public long CantidadEstimada { get; init; }
        [JsonPropertyName("prepared_quantity")] public long? CantidadPreparada { get; init; }
        [JsonPropertyName("estimated_line_cents")] public long ImporteEstimadoCentimos { get; init; }
        [JsonPropertyName("estimated_discount_cents")] public long DescuentoEstimadoCentimos { get; init; }
        [JsonPropertyName("final_line_cents")] public long? ImporteFinalCentimos { get; init; }
        [JsonPropertyName("final_discount_cents")] public long? DescuentoFinalCentimos { get; init; }
        [JsonPropertyName("substitution_policy")] public string PoliticaSustitucion { get; init; } = string.Empty;
    }

    private sealed class RespuestaHistorial
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("from_status")] public string? EstadoAnterior { get; init; }
        [JsonPropertyName("to_status")] public string EstadoNuevo { get; init; } = string.Empty;
        [JsonPropertyName("reason_code")] public string? Motivo { get; init; }
        [JsonPropertyName("note")] public string? Nota { get; init; }
        [JsonPropertyName("created_at")] public DateTimeOffset CreadoEn { get; init; }
    }

    private sealed class RespuestaPago
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("transaction_type")] public string Tipo { get; init; } = string.Empty;
        [JsonPropertyName("method")] public string Metodo { get; init; } = string.Empty;
        [JsonPropertyName("provider")] public string Proveedor { get; init; } = string.Empty;
        [JsonPropertyName("provider_reference")] public string? Referencia { get; init; }
        [JsonPropertyName("amount_cents")] public long ImporteCentimos { get; init; }
        [JsonPropertyName("status")] public string Estado { get; init; } = string.Empty;
        [JsonPropertyName("failure_message")] public string? Error { get; init; }
        [JsonPropertyName("created_at")] public DateTimeOffset CreadoEn { get; init; }
    }

    private sealed class RespuestaEntrega
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("status")] public string Estado { get; init; } = string.Empty;
        [JsonPropertyName("recipient_name")] public string? Destinatario { get; init; }
        [JsonPropertyName("notes")] public string? Notas { get; init; }
        [JsonPropertyName("assigned_at")] public DateTimeOffset AsignadoEn { get; init; }
        [JsonPropertyName("started_at")] public DateTimeOffset? IniciadoEn { get; init; }
        [JsonPropertyName("delivered_at")] public DateTimeOffset? EntregadoEn { get; init; }
        [JsonPropertyName("delivery_events")] public List<RespuestaEventoEntrega> Eventos { get; init; } = [];
    }

    private sealed class RespuestaEventoEntrega
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("event_type")] public string Tipo { get; init; } = string.Empty;
        [JsonPropertyName("note")] public string? Nota { get; init; }
        [JsonPropertyName("created_at")] public DateTimeOffset CreadoEn { get; init; }
    }

    private sealed class RespuestaError
    {
        [JsonPropertyName("message")]
        public string? Mensaje { get; init; }
    }
}
