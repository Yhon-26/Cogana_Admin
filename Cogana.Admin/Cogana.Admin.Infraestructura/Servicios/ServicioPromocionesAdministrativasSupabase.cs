using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioPromocionesAdministrativasSupabase(ClienteSupabaseRest cliente)
    : IServicioPromocionesAdministrativas
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PromocionDetalle?> ObtenerPromocionAsync(
        Guid tiendaId,
        Guid promocionId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/promotions" +
            "?select=id,name,description,promotion_type,value,minimum_quantity,minimum_order_cents,coupon_code," +
            "starts_at,ends_at,usage_limit,per_customer_limit,priority,is_stackable,is_active,promotion_products(product_id)" +
            $"&store_id=eq.{tiendaId:D}&id=eq.{promocionId:D}&limit=1";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var promociones = await respuesta.Content.ReadFromJsonAsync<List<RespuestaPromocion>>(
            OpcionesJson,
            cancellationToken) ?? [];
        var promocion = promociones.FirstOrDefault();
        return promocion is null
            ? null
            : new PromocionDetalle(
                promocion.Id,
                promocion.Nombre,
                promocion.Descripcion,
                promocion.Tipo,
                promocion.Valor,
                promocion.CantidadMinima,
                promocion.PedidoMinimoCentimos,
                promocion.CodigoCupon,
                promocion.Inicio,
                promocion.Fin,
                promocion.LimiteUsos,
                promocion.LimitePorCliente,
                promocion.Prioridad,
                promocion.Acumulable,
                promocion.EstaActiva,
                promocion.Productos.Select(item => item.ProductoId).ToHashSet());
    }

    public async Task<IReadOnlyList<ProductoPromocionSeleccion>> ObtenerProductosAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/products?select=id,name,is_active" +
            $"&store_id=eq.{tiendaId:D}&order=name.asc&limit=500";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var productos = await respuesta.Content.ReadFromJsonAsync<List<RespuestaProducto>>(
            OpcionesJson,
            cancellationToken) ?? [];
        return productos.Select(producto => new ProductoPromocionSeleccion(
            producto.Id,
            producto.Nombre,
            producto.EstaActivo)).ToList();
    }

    public async Task<ResultadoOperacion> GuardarAsync(
        Guid tiendaId,
        Guid promocionId,
        EdicionPromocion edicion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/rpc/save_promotion_admin")
            {
                Content = JsonContent.Create(new
                {
                    p_store_id = tiendaId,
                    p_promotion_id = promocionId,
                    p_name = edicion.Nombre.Trim(),
                    p_description = Limpiar(edicion.Descripcion),
                    p_promotion_type = edicion.Tipo,
                    p_value = edicion.Valor,
                    p_minimum_quantity = edicion.CantidadMinima,
                    p_minimum_order_cents = edicion.PedidoMinimoCentimos,
                    p_coupon_code = Limpiar(edicion.CodigoCupon),
                    p_starts_at = edicion.Inicio,
                    p_ends_at = edicion.Fin,
                    p_usage_limit = edicion.LimiteUsos,
                    p_per_customer_limit = edicion.LimitePorCliente,
                    p_priority = edicion.Prioridad,
                    p_is_stackable = edicion.Acumulable,
                    p_product_ids = edicion.ProductoIds,
                    p_operation_id = edicion.OperacionId
                })
            };
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            return respuesta.IsSuccessStatusCode
                ? ResultadoOperacion.Correcta("Promoción y productos actualizados correctamente.")
                : ResultadoOperacion.Fallida(await LeerErrorAsync(respuesta, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida("No fue posible actualizar la promoción en Supabase.");
        }
    }

    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static async Task<string> LeerErrorAsync(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(OpcionesJson, cancellationToken);
            if (error?.Codigo == "23505") return "El código de cupón ya está registrado.";
            if (!string.IsNullOrWhiteSpace(error?.Mensaje)) return error.Mensaje;
        }
        catch (JsonException)
        {
        }

        return $"Supabase rechazó la operación ({(int)respuesta.StatusCode}).";
    }

    private sealed class RespuestaPromocion
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("name")] public string Nombre { get; init; } = string.Empty;
        [JsonPropertyName("description")] public string? Descripcion { get; init; }
        [JsonPropertyName("promotion_type")] public string Tipo { get; init; } = string.Empty;
        [JsonPropertyName("value")] public long Valor { get; init; }
        [JsonPropertyName("minimum_quantity")] public long? CantidadMinima { get; init; }
        [JsonPropertyName("minimum_order_cents")] public long PedidoMinimoCentimos { get; init; }
        [JsonPropertyName("coupon_code")] public string? CodigoCupon { get; init; }
        [JsonPropertyName("starts_at")] public DateTimeOffset Inicio { get; init; }
        [JsonPropertyName("ends_at")] public DateTimeOffset Fin { get; init; }
        [JsonPropertyName("usage_limit")] public int? LimiteUsos { get; init; }
        [JsonPropertyName("per_customer_limit")] public int? LimitePorCliente { get; init; }
        [JsonPropertyName("priority")] public int Prioridad { get; init; }
        [JsonPropertyName("is_stackable")] public bool Acumulable { get; init; }
        [JsonPropertyName("is_active")] public bool EstaActiva { get; init; }
        [JsonPropertyName("promotion_products")] public List<RespuestaProductoPromocion> Productos { get; init; } = [];
    }

    private sealed class RespuestaProductoPromocion
    {
        [JsonPropertyName("product_id")] public Guid ProductoId { get; init; }
    }

    private sealed class RespuestaProducto
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("name")] public string Nombre { get; init; } = string.Empty;
        [JsonPropertyName("is_active")] public bool EstaActivo { get; init; }
    }

    private sealed class RespuestaError
    {
        [JsonPropertyName("code")] public string? Codigo { get; init; }
        [JsonPropertyName("message")] public string? Mensaje { get; init; }
    }
}
