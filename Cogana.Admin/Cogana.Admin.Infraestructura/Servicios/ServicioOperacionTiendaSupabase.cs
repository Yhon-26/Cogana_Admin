using System.Net.Http.Json;
using System.Text.Json;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioOperacionTiendaSupabase(ClienteSupabaseRest cliente):IServicioOperacionTienda
{
    public async Task<OperacionTienda> ObtenerAsync(Guid tienda,CancellationToken token=default)
    {
        var pagos=LeerAsync($"/rest/v1/store_settings?store_id=eq.{tienda:D}&select=yape_recipient_name,yape_recipient_phone,plin_recipient_name,plin_recipient_phone,manual_payment_window_minutes",token);
        var estado=LeerAsync($"/rest/v1/store_operational_state?store_id=eq.{tienda:D}&select=status,public_message",token);
        var reglas=LeerAsync($"/rest/v1/store_fulfillment_rules?store_id=eq.{tienda:D}&select=fulfillment_type,slot_minutes,max_orders_per_slot,minimum_notice_minutes,delivery_staff_count",token);
        await Task.WhenAll(pagos,estado,reglas);
        var p=(await pagos)[0];var e=(await estado)[0];
        return new(new(Texto(p,"yape_recipient_name"),Texto(p,"yape_recipient_phone"),Texto(p,"plin_recipient_name"),Texto(p,"plin_recipient_phone"),p.GetProperty("manual_payment_window_minutes").GetInt32()),
            Texto(e,"status")??"closed",Texto(e,"public_message"),(await reglas).EnumerateArray().Select(r=>new ReglaAtencion(Texto(r,"fulfillment_type")!,
            r.GetProperty("slot_minutes").GetInt32(),r.GetProperty("max_orders_per_slot").GetInt32(),r.GetProperty("minimum_notice_minutes").GetInt32(),
            r.GetProperty("delivery_staff_count").ValueKind==JsonValueKind.Null?null:r.GetProperty("delivery_staff_count").GetInt32())).ToList());
    }
    public Task<ResultadoOperacion> GuardarPagosAsync(Guid tienda,PagosTienda pagos,CancellationToken token=default)=>
        GuardarAsync(HttpMethod.Patch,$"/rest/v1/store_settings?store_id=eq.{tienda:D}",new {yape_recipient_name=Limpiar(pagos.YapeNombre),yape_recipient_phone=Limpiar(pagos.YapeTelefono),
            plin_recipient_name=Limpiar(pagos.PlinNombre),plin_recipient_phone=Limpiar(pagos.PlinTelefono),manual_payment_window_minutes=pagos.MinutosPago},true,token);
    public Task<ResultadoOperacion> GuardarEstadoAsync(Guid tienda,string estado,string mensaje,CancellationToken token=default)=>
        GuardarAsync(HttpMethod.Patch,$"/rest/v1/store_operational_state?store_id=eq.{tienda:D}",new {status=estado,public_message=Limpiar(mensaje)},true,token);
    public Task<ResultadoOperacion> GuardarReglaAsync(Guid tienda,ReglaAtencion regla,CancellationToken token=default)=>
        GuardarAsync(HttpMethod.Post,"/rest/v1/rpc/set_fulfillment_rules",new{p_store_id=tienda,p_mode=regla.Modalidad,p_slot_minutes=regla.MinutosFranja,
            p_max_orders=regla.Cupo,p_notice_minutes=regla.Anticipacion,p_delivery_staff_count=regla.Repartidores},false,token);
    private async Task<JsonElement> LeerAsync(string ruta,CancellationToken token)
    {
        using var respuesta=await cliente.ObtenerAsync(ruta,token);respuesta.EnsureSuccessStatusCode();
        var valor=await respuesta.Content.ReadFromJsonAsync<JsonElement>(token);
        if(valor.ValueKind!=JsonValueKind.Array)throw new JsonException("Respuesta de configuración inválida.");
        return valor;
    }
    private async Task<ResultadoOperacion> GuardarAsync(HttpMethod metodo,string ruta,object contenido,bool unaFila,CancellationToken token)
    {
        try
        {
            using var solicitud=new HttpRequestMessage(metodo,ruta){Content=JsonContent.Create(contenido)};
            solicitud.Headers.TryAddWithoutValidation("Prefer","return=representation");
            using var respuesta=await cliente.EnviarAsync(solicitud,token);
            var valor=await respuesta.Content.ReadFromJsonAsync<JsonElement>(token);
            if(!respuesta.IsSuccessStatusCode)return ResultadoOperacion.Fallida(valor.ValueKind==JsonValueKind.Object && valor.TryGetProperty("message",out var error)?error.GetString()??"Operación rechazada.":"Operación rechazada.");
            if(unaFila && (valor.ValueKind!=JsonValueKind.Array || valor.GetArrayLength()!=1))return ResultadoOperacion.Fallida("No se confirmó el registro actualizado. Vuelve a consultar la tienda.");
            return ResultadoOperacion.Correcta("Configuración guardada correctamente.");
        }
        catch(Exception error)when(error is HttpRequestException or JsonException or OperationCanceledException)
        {return ResultadoOperacion.Fallida("No se pudo confirmar el guardado. Vuelve a consultar antes de reintentar.");}
    }
    private static string? Limpiar(string? texto)=>string.IsNullOrWhiteSpace(texto)?null:texto.Trim();
    private static string? Texto(JsonElement elemento,string campo)=>elemento.GetProperty(campo).ValueKind==JsonValueKind.Null?null:elemento.GetProperty(campo).GetString();
}
