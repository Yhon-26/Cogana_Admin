using System.Net.Http.Json;
using System.Text.Json;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioOperacionPedidosSupabase(ClienteSupabaseRest cliente) : IServicioOperacionPedidos
{
    public Task<ResultadoOperacion> ConfirmarPagoAsync(Guid tienda, Guid pedido, Guid pago, string referencia, Guid operacion, CancellationToken token = default) =>
        EjecutarAsync("confirm_manual_payment", new { p_store_id=tienda, p_order_id=pedido, p_transaction_id=pago, p_operation_id=operacion, p_provider_reference=referencia.Trim(), p_proof_storage_path=(string?)null }, token);
    public Task<ResultadoOperacion> RechazarPagoAsync(Guid tienda, Guid pago, string motivo, Guid operacion, CancellationToken token = default) =>
        EjecutarAsync("reject_manual_payment", new { p_store_id=tienda, p_transaction_id=pago, p_reason=motivo.Trim(), p_operation_id=operacion }, token);
    public Task<ResultadoOperacion> LiquidarDiferenciaAsync(Guid tienda, Guid pago, string referencia, Guid operacion, CancellationToken token = default) =>
        EjecutarAsync("settle_manual_difference", new { p_store_id=tienda, p_transaction_id=pago, p_reference=referencia.Trim(), p_operation_id=operacion }, token);
    public Task<ResultadoOperacion> FinalizarPreparacionAsync(Guid tienda, Guid pedido, IReadOnlyList<CantidadPreparada> cantidades, Guid operacion, CancellationToken token = default) =>
        EjecutarAsync("finalize_weighted_order", new { p_store_id=tienda, p_order_id=pedido, p_items=cantidades.Select(x=>new { order_item_id=x.ArticuloId, prepared_quantity=x.Cantidad }), p_operation_id=operacion }, token);
    public Task<ResultadoOperacion> AsignarRepartidorAsync(Guid tienda, Guid pedido, Guid repartidor, Guid operacion, CancellationToken token = default) =>
        EjecutarAsync("assign_delivery", new { p_store_id=tienda, p_order_id=pedido, p_driver_user_id=repartidor, p_operation_id=operacion }, token);
    public Task<ResultadoOperacion> CompletarRecojoAsync(Guid tienda, Guid pedido, string codigo, Guid operacion, CancellationToken token = default) =>
        EjecutarAsync("complete_pickup", new { p_store_id=tienda, p_order_id=pedido, p_confirmation_code=codigo.Trim(), p_operation_id=operacion }, token);
    public Task<ResultadoOperacion> CompletarEntregaAsync(Guid tienda, Guid asignacion, string codigo, string destinatario, Guid operacion, CancellationToken token = default) =>
        EjecutarAsync("complete_delivery", new { p_store_id=tienda, p_assignment_id=asignacion, p_confirmation_code=codigo.Trim(), p_evidence_storage_path=(string?)null, p_recipient_name=destinatario.Trim(), p_operation_id=operacion }, token);
    public async Task<IReadOnlyList<UsuarioAdministrativoDetalle>> ObtenerRepartidoresAsync(Guid tienda, CancellationToken token = default) =>
        (await new ServicioUsuariosAdministrativosSupabase(cliente).ObtenerUsuariosAsync(tienda, token))
            .Where(x=>x.EstaActivo && x.Rol is "owner" or "admin" or "driver").ToList();

    public async Task<IReadOnlyList<SustitucionPedido>> ObtenerSustitucionesAsync(Guid tienda,Guid pedido,CancellationToken token=default)
    {
        var datos=await LeerListaAsync($"/rest/v1/order_substitutions?store_id=eq.{tienda:D}&order_id=eq.{pedido:D}&select=id,order_item_id,status,price_difference_cents,expires_at&order=created_at.desc",token);
        return datos.EnumerateArray().Select(x=>new SustitucionPedido(x.GetProperty("id").GetGuid(),x.GetProperty("order_item_id").GetGuid(),x.GetProperty("status").GetString()!,
            x.GetProperty("price_difference_cents").GetInt64(),x.GetProperty("expires_at").ValueKind==JsonValueKind.Null?null:x.GetProperty("expires_at").GetDateTimeOffset())).ToList();
    }
    public async Task<IReadOnlyList<PresentacionSustitucion>> BuscarReemplazosAsync(Guid tienda,string busqueda,CancellationToken token=default)
    {
        var termino=busqueda.Trim().Replace("\\","\\\\").Replace("\"","\\\"").Replace("*","").Replace("%","");
        var filtro=Uri.EscapeDataString("\"*"+termino+"*\"");
        var datos=await LeerListaAsync($"/rest/v1/product_presentations?store_id=eq.{tienda:D}&is_active=eq.true&name=ilike.{filtro}&select=id,name,base_quantity,price_cents,products(name,base_unit)&order=name.asc,id.asc&limit=50",token);
        return datos.EnumerateArray().Select(x=>new PresentacionSustitucion(x.GetProperty("id").GetGuid(),x.GetProperty("name").GetString()!,
            x.GetProperty("products").GetProperty("name").GetString()!,x.GetProperty("products").GetProperty("base_unit").GetString()!,
            x.GetProperty("base_quantity").GetInt64(),x.GetProperty("price_cents").GetInt64())).ToList();
    }
    public Task<ResultadoOperacion> ProponerSustitucionAsync(Guid tienda,Guid pedido,Guid articulo,Guid presentacion,long cantidad,DateTimeOffset vence,Guid operacion,CancellationToken token=default)=>
        EjecutarAsync("propose_substitution",new{p_store_id=tienda,p_order_id=pedido,p_order_item_id=articulo,p_replacement_presentation_id=presentacion,p_proposed_quantity=cantidad,p_expires_at=vence,p_operation_id=operacion},token);
    public Task<ResultadoOperacion> DecidirSustitucionAsync(Guid tienda,Guid sustitucion,bool aceptar,Guid operacion,CancellationToken token=default)=>
        EjecutarAsync("decide_substitution",new{p_store_id=tienda,p_substitution_id=sustitucion,p_decision=aceptar?"accepted":"rejected",p_operation_id=operacion},token);
    private async Task<JsonElement> LeerListaAsync(string ruta,CancellationToken token)
    {
        using var respuesta=await cliente.ObtenerAsync(ruta,token);respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>(token);
    }

    private async Task<ResultadoOperacion> EjecutarAsync(string funcion, object parametros, CancellationToken token)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/rpc/"+funcion) { Content=JsonContent.Create(parametros) };
            using var respuesta = await cliente.EnviarAsync(solicitud, token);
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>(token);
            if (!respuesta.IsSuccessStatusCode)
                return ResultadoOperacion.Fallida(cuerpo.ValueKind == JsonValueKind.Object && cuerpo.TryGetProperty("message",out var mensaje)
                    ? mensaje.GetString() ?? "El servidor rechazó la operación." : "El servidor rechazó la operación.");
            if (cuerpo.ValueKind != JsonValueKind.Object)
                return ResultadoOperacion.Fallida("No se pudo confirmar el resultado. Actualiza el pedido antes de reintentar.");
            var identidad=funcion switch
            {
                "confirm_manual_payment" or "reject_manual_payment" or "settle_manual_difference"=>"transaction_id",
                "assign_delivery"=>"assignment_id",
                "propose_substitution" or "decide_substitution"=>"substitution_id",
                _=>"order_id"
            };
            if(!cuerpo.TryGetProperty(identidad,out var identificador) || identificador.ValueKind!=JsonValueKind.String || !identificador.TryGetGuid(out _))
                return ResultadoOperacion.Fallida("El servidor no confirmó el registro afectado. Actualiza el pedido antes de reintentar.");
            return ResultadoOperacion.Correcta("Operación registrada. El pedido se actualizará con el resultado del servidor.");
        }
        catch (OperationCanceledException) { return ResultadoOperacion.Fallida("No se confirmó la operación a tiempo. Actualiza el pedido o reintenta la misma operación."); }
        catch (HttpRequestException) { return ResultadoOperacion.Fallida("Se perdió la conexión. Actualiza el pedido o reintenta la misma operación."); }
        catch (JsonException) { return ResultadoOperacion.Fallida("No se pudo interpretar la respuesta. Actualiza el pedido antes de reintentar."); }
    }
}
