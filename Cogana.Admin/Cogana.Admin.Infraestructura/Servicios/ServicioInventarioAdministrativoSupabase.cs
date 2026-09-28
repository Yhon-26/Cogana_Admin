using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioInventarioAdministrativoSupabase(ClienteSupabaseRest cliente)
    : IServicioInventarioAdministrativo
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<LoteInventarioDetalle?> ObtenerLoteAsync(
        Guid tiendaId,
        Guid loteId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/inventory_lots" +
            "?select=id,product_id,lot_code,received_at,expires_on,loose_on_hand_quantity,status,notes," +
            "products(name,base_unit),suppliers(name)" +
            $"&store_id=eq.{tiendaId:D}&id=eq.{loteId:D}&limit=1";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var lotes = await respuesta.Content.ReadFromJsonAsync<List<RespuestaLote>>(
            OpcionesJson,
            cancellationToken) ?? [];
        var lote = lotes.FirstOrDefault();
        return lote is null
            ? null
            : new LoteInventarioDetalle(
                lote.Id,
                lote.ProductoId,
                lote.Producto?.Nombre ?? "Producto no disponible",
                lote.Producto?.UnidadBase ?? "unit",
                lote.Codigo,
                lote.Proveedor?.Nombre,
                lote.RecibidoEn,
                lote.VenceEl,
                lote.CantidadDisponible,
                lote.Estado,
                lote.Notas);
    }

    public async Task<IReadOnlyList<MovimientoInventarioResumen>> ObtenerMovimientosAsync(
        Guid tiendaId,
        Guid loteId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/inventory_movements" +
            "?select=id,movement_type,base_quantity_delta,loose_quantity_delta,sealed_units_delta,reason,created_at" +
            $"&store_id=eq.{tiendaId:D}&lot_id=eq.{loteId:D}&order=created_at.desc&limit=100";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var movimientos = await respuesta.Content.ReadFromJsonAsync<List<RespuestaMovimiento>>(
            OpcionesJson,
            cancellationToken) ?? [];
        return movimientos.Select(movimiento => new MovimientoInventarioResumen(
            movimiento.Id,
            movimiento.Tipo,
            movimiento.CambioCantidadBase,
            movimiento.CambioCantidadSuelta,
            movimiento.CambioUnidadesSelladas,
            movimiento.Motivo,
            movimiento.CreadoEn)).ToList();
    }

    public async Task<ResultadoOperacion> AjustarExistenciaAsync(
        Guid tiendaId,
        Guid loteId,
        AjusteInventario ajuste,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post,
                "/rest/v1/rpc/adjust_inventory_lot")
            {
                Content = JsonContent.Create(new
                {
                    p_store_id = tiendaId,
                    p_lot_id = loteId,
                    p_counted_quantity = ajuste.CantidadContada,
                    p_operation_id = ajuste.OperacionId,
                    p_reason = ajuste.Motivo.Trim()
                })
            };
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            return respuesta.IsSuccessStatusCode
                ? ResultadoOperacion.Correcta("Existencia ajustada y movimiento registrado correctamente.")
                : ResultadoOperacion.Fallida(await LeerErrorAsync(respuesta, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida("No fue posible ajustar el inventario en Supabase.");
        }
    }

    private static async Task<string> LeerErrorAsync(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(
                OpcionesJson,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Mensaje))
            {
                return error.Mensaje;
            }
        }
        catch (JsonException)
        {
        }

        return $"Supabase rechazó la operación ({(int)respuesta.StatusCode}).";
    }

    private sealed class RespuestaLote
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("product_id")]
        public Guid ProductoId { get; init; }

        [JsonPropertyName("lot_code")]
        public string Codigo { get; init; } = string.Empty;

        [JsonPropertyName("received_at")]
        public DateTimeOffset RecibidoEn { get; init; }

        [JsonPropertyName("expires_on")]
        public DateOnly? VenceEl { get; init; }

        [JsonPropertyName("loose_on_hand_quantity")]
        public long CantidadDisponible { get; init; }

        [JsonPropertyName("status")]
        public string Estado { get; init; } = string.Empty;

        [JsonPropertyName("notes")]
        public string? Notas { get; init; }

        [JsonPropertyName("products")]
        public RespuestaProducto? Producto { get; init; }

        [JsonPropertyName("suppliers")]
        public RespuestaProveedor? Proveedor { get; init; }
    }

    private sealed class RespuestaProducto
    {
        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;

        [JsonPropertyName("base_unit")]
        public string UnidadBase { get; init; } = string.Empty;
    }

    private sealed class RespuestaProveedor
    {
        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;
    }

    private sealed class RespuestaMovimiento
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("movement_type")]
        public string Tipo { get; init; } = string.Empty;

        [JsonPropertyName("base_quantity_delta")]
        public long CambioCantidadBase { get; init; }

        [JsonPropertyName("loose_quantity_delta")]
        public long CambioCantidadSuelta { get; init; }

        [JsonPropertyName("sealed_units_delta")]
        public long CambioUnidadesSelladas { get; init; }

        [JsonPropertyName("reason")]
        public string? Motivo { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreadoEn { get; init; }
    }

    private sealed class RespuestaError
    {
        [JsonPropertyName("message")]
        public string? Mensaje { get; init; }
    }
}
