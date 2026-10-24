using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio.Servicios;
using Cogana.Admin.Infraestructura.Configuracion;
using Cogana.Admin.Infraestructura.Servicios;

var total=0;
void Comprobar(bool condicion,string mensaje) { if(!condicion) throw new InvalidOperationException(mensaje); total++; Console.WriteLine("Correcto: "+mensaje); }
var configuracion=new ConfiguracionSupabase(new Uri("https://pruebas.invalid"),"publica-ficticia");
var tienda=Guid.NewGuid(); var pedido=Guid.NewGuid();
HttpResponseMessage Json(object contenido,HttpStatusCode codigo=HttpStatusCode.OK)=>new(codigo){Content=JsonContent.Create(contenido)};
var bytes=new byte[]{0x89,0x50,0x4e,0x47,0xff,0x00,0x81,0x42};
var solicitudes=new List<(string? Token,byte[] Bytes,string? Tipo,string? Operacion)>();
using(var cliente=new ClienteSupabaseRest(configuracion,new Servidor(async req=>{
    if(req.RequestUri!.AbsolutePath=="/auth/v1/token") return Json(new{access_token="nuevo",refresh_token="renovado"});
    solicitudes.Add((req.Headers.Authorization?.Parameter,await req.Content!.ReadAsByteArrayAsync(),req.Content.Headers.ContentType?.MediaType,
        req.Headers.TryGetValues("x-upsert",out var h)?h.Single():null));
    return Json(new{},solicitudes.Count==1?HttpStatusCode.Unauthorized:HttpStatusCode.OK);
}))) {
    cliente.EstablecerSesion("viejo","renovacion");
    using var req=new HttpRequestMessage(HttpMethod.Post,"/storage/prueba"){Content=new ByteArrayContent(bytes)};
    req.Content.Headers.ContentType=new MediaTypeHeaderValue("image/png"); req.Headers.Add("x-upsert","true");
    using var respuesta=await cliente.EnviarAsync(req);
    Comprobar(respuesta.IsSuccessStatusCode && solicitudes[1].Token=="nuevo","El reintento usa el token renovado");
    Comprobar(solicitudes[1].Bytes.SequenceEqual(bytes),"Una imagen conserva exactamente sus bytes al reintentar");
    Comprobar(solicitudes[1].Tipo=="image/png" && solicitudes[1].Operacion=="true","Conserva los encabezados del cuerpo y operación");
}
var renovaciones=0; var iniciales=0; var barrera=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
using(var cliente=new ClienteSupabaseRest(configuracion,new Servidor(async req=>{
    if(req.RequestUri!.AbsolutePath=="/auth/v1/token") { Interlocked.Increment(ref renovaciones); return Json(new{access_token="nuevo",refresh_token="renovado"}); }
    if(req.Headers.Authorization?.Parameter=="viejo") { if(Interlocked.Increment(ref iniciales)==8)barrera.SetResult(); await barrera.Task; return Json(new{},HttpStatusCode.Unauthorized); }
    return Json(new{});
}))) {
    cliente.EstablecerSesion("viejo","renovacion");
    var respuestas=await Task.WhenAll(Enumerable.Range(0,8).Select(i=>cliente.ObtenerAsync("/datos?i="+i)));
    Comprobar(renovaciones==1 && respuestas.All(x=>x.IsSuccessStatusCode),"Ocho consultas concurrentes comparten una sola renovación");
    foreach(var respuesta in respuestas)respuesta.Dispose();
}
var fotoAnterior=Guid.NewGuid(); var producto=Guid.NewGuid(); var metodos=new List<string>(); var rutasBorradas=new List<string>();
var archivo=Path.Combine(Path.GetTempPath(),"cogana-regresion-"+Guid.NewGuid()+".png");
await File.WriteAllBytesAsync(archivo,bytes);
try {
    using var cliente=new ClienteSupabaseRest(configuracion,new Servidor(async req=>{
        var url=req.RequestUri!; metodos.Add(req.Method.Method+" "+url.AbsolutePath);
        if(req.Method==HttpMethod.Delete){rutasBorradas.Add(await req.Content!.ReadAsStringAsync());return Json(new{});}
        if(req.Method==HttpMethod.Get) return url.Query.Contains("storage_path=eq.") ? Json(Array.Empty<object>()) :
            Json(new[]{new{id=fotoAnterior,storage_path=tienda+"/anterior.png",sort_order=0}});
        if(req.Method==HttpMethod.Patch) return Json(new{message="fallo de metadatos"},HttpStatusCode.InternalServerError);
        return Json(new{});
    }));
    var resultado=await new ServicioImagenesProducto(cliente,configuracion).SubirAsync(tienda,producto,archivo);
    Comprobar(!resultado.EsExitoso,"No informa éxito si falla la escritura de metadatos de imagen");
    Comprobar(!metodos.Any(x=>x.StartsWith("DELETE /rest/")),"El reemplazo nunca borra la referencia anterior antes de guardar");
    Comprobar(rutasBorradas.All(x=>!x.Contains("anterior.png")),"Un guardado rechazado conserva el objeto anterior");
} finally { File.Delete(archivo); }

using(var cliente=new ClienteSupabaseRest(configuracion,new Servidor(req=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent))))) {
    var resultado=await new ServicioCatalogoAdministrativoSupabase(cliente).CambiarEstadoAsync(tienda,"Productos",producto,false);
    Comprobar(!resultado.EsExitoso,"Un HTTP 204 sin fila confirmada no se presenta como guardado efectivo");
}
var confirmada=false;var borradoTrasConfirmar=false;
var imagenValida=Path.Combine(Path.GetTempPath(),"cogana-reemplazo-"+Guid.NewGuid()+".png");await File.WriteAllBytesAsync(imagenValida,bytes);
try
{
    using var cliente=new ClienteSupabaseRest(configuracion,new Servidor(async req=>
    {
        if(req.Method==HttpMethod.Get)return req.RequestUri!.Query.Contains("storage_path=eq.")?Json(Array.Empty<object>()):Json(new[]{new{id=fotoAnterior,storage_path=tienda+"/anterior.png",sort_order=0}});
        if(req.Method==HttpMethod.Patch){confirmada=true;var valor=await req.Content!.ReadFromJsonAsync<JsonElement>();return Json(new[]{valor});}
        if(req.Method==HttpMethod.Delete){borradoTrasConfirmar=confirmada;return Json(new{},HttpStatusCode.InternalServerError);}
        return Json(new{});
    }));
    var resultado=await new ServicioImagenesProducto(cliente,configuracion).SubirAsync(tienda,producto,imagenValida);
    Comprobar(resultado.EsExitoso && confirmada && borradoTrasConfirmar,"La imagen se confirma antes de limpiar el objeto anterior; una limpieza pendiente no revierte el guardado");
}
finally{File.Delete(imagenValida);}
var idAlta=Guid.NewGuid(); var altas=0;
using(var cliente=new ClienteSupabaseRest(configuracion,new Servidor(async req=>{
    if(req.Method==HttpMethod.Get) return Json(new[]{new{id=idAlta}});
    var cuerpo=await req.Content!.ReadFromJsonAsync<JsonElement>();
    Comprobar(cuerpo.GetProperty("id").GetGuid()==idAlta,"El alta usa el identificador estable del formulario");
    altas++; return altas==1?Json(new[]{new{id=idAlta}}):Json(Array.Empty<object>());
}))) {
    var servicio=new ServicioCatalogoAdministrativoSupabase(cliente);
    var datos=new NuevoProducto("Prueba",null,null,null,null,"gram",false,0,idAlta);
    var uno=await servicio.CrearProductoAsync(tienda,datos); var dos=await servicio.CrearProductoAsync(tienda,datos);
    Comprobar(uno.EsExitoso && dos.EsExitoso && dos.ProductoId==idAlta,"Un alta repetida recupera el mismo producto sin duplicarlo");
}
using(var cliente=new ClienteSupabaseRest(configuracion,new Servidor(req=>{
    Comprobar(req.RequestUri!.Query.Contains("offset=250") && req.RequestUri.Query.Contains("limit=50"),"La página consulta solo el tramo solicitado");
    Comprobar(req.RequestUri.Query.Contains("or="),"La búsqueda se envía al servidor");
    var respuesta=Json(new[]{new{id=producto,name="Coincidencia",is_active=true}});
    respuesta.Content.Headers.TryAddWithoutValidation("Content-Range","250-250/301");
    return Task.FromResult(respuesta);
}))) {
    var pagina=await new ServicioModulosAdministrativosSupabase(cliente).ObtenerPaginaAsync("Productos",tienda,250,50,"producto lejano");
    Comprobar(pagina.EsExitoso && pagina.Datos?.TotalRegistros==301 && pagina.Datos.Filas.Count==1,"La búsqueda puede devolver registros posteriores a los primeros 250 y conserva el total real");
}

var rpc=new List<(string Nombre,JsonElement Cuerpo)>();
using(var cliente=new ClienteSupabaseRest(configuracion,new Servidor(async req=>{
    rpc.Add((req.RequestUri!.Segments.Last(),(await req.Content!.ReadFromJsonAsync<JsonElement>()).Clone()));
    return Json(new{status="confirmado",idempotent_replay=false,order_id=pedido,assignment_id=pedido,transaction_id=pedido,substitution_id=pedido});
}))) {
    var servicio=new ServicioOperacionPedidosSupabase(cliente); var op=Guid.NewGuid(); var pago=Guid.NewGuid(); var articulo=Guid.NewGuid(); var persona=Guid.NewGuid();
    await servicio.ConfirmarPagoAsync(tienda,pedido,pago,"R-1",op);
    await servicio.RechazarPagoAsync(tienda,pago,"No recibido",op);
    await servicio.LiquidarDiferenciaAsync(tienda,pago,"D-1",op);
    await servicio.FinalizarPreparacionAsync(tienda,pedido,new[]{new CantidadPreparada(articulo,1250)},op);
    await servicio.AsignarRepartidorAsync(tienda,pedido,persona,op);
    await servicio.CompletarRecojoAsync(tienda,pedido,"CODIGO-FICTICIO",op);
    await servicio.CompletarEntregaAsync(tienda,persona,"CODIGO-FICTICIO","Receptor de prueba",op);
    var nombres=new[]{"confirm_manual_payment","reject_manual_payment","settle_manual_difference","finalize_weighted_order","assign_delivery","complete_pickup","complete_delivery"};
    Comprobar(rpc.Select(x=>x.Nombre).SequenceEqual(nombres),"Las siete operaciones usan los RPC vigentes del backend");
    Comprobar(rpc.All(x=>x.Cuerpo.GetProperty("p_store_id").GetGuid()==tienda && x.Cuerpo.GetProperty("p_operation_id").GetGuid()==op),"Cada operación conserva tienda e identificador de idempotencia");
    Comprobar(rpc[3].Cuerpo.GetProperty("p_items")[0].GetProperty("prepared_quantity").GetInt64()==1250,"La preparación envía cantidades base sin calcular dinero en el cliente");
    Comprobar(rpc[5].Cuerpo.GetProperty("p_confirmation_code").GetString()=="CODIGO-FICTICIO","El recojo exige el contrato de código, no una transición completed");
    Comprobar(rpc[6].Cuerpo.GetProperty("p_assignment_id").GetGuid()==persona,"La entrega se completa sobre la asignación real");
    await servicio.ProponerSustitucionAsync(tienda,pedido,articulo,persona,100,DateTimeOffset.UtcNow.AddMinutes(30),op);
    await servicio.DecidirSustitucionAsync(tienda,persona,true,op);
    await new ServicioInventarioAdministrativoSupabase(cliente).AbrirPaquetesAsync(tienda,pedido,persona,1,"Fraccionar",op);
    await new ServicioOperacionTiendaSupabase(cliente).GuardarReglaAsync(tienda,new("delivery",60,4,90,2));
    using var contratos=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"contratos-backend.json")));
    foreach(var llamada in rpc)
    {
        var firma=contratos.RootElement.GetProperty("functions").EnumerateArray().Single(x=>x.GetProperty("name").GetString()==llamada.Nombre);
        var esperados=firma.GetProperty("parameters").EnumerateArray().Select(x=>x.GetString()).OrderBy(x=>x).ToArray();
        var enviados=llamada.Cuerpo.EnumerateObject().Select(x=>x.Name).OrderBy(x=>x).ToArray();
        Comprobar(esperados.SequenceEqual(enviados),"Contrato vigente: "+llamada.Nombre);
    }
}
using(var cliente=new ClienteSupabaseRest(configuracion,new Servidor(req=>Task.FromResult(Json(new{})))))
{
    var resultado=await new ServicioOperacionPedidosSupabase(cliente).CompletarRecojoAsync(tienda,pedido,"FICTICIO",Guid.NewGuid());
    Comprobar(!resultado.EsExitoso,"Un HTTP 200 sin identidad del registro no confirma una operación del pedido");
}
var carpeta=Path.Combine(Path.GetTempPath(),"cogana-bandera-"+Guid.NewGuid()); Directory.CreateDirectory(carpeta);
try { var estado=new EstadoPrimerIngreso(carpeta); estado.MarcarVisto("prueba"); Comprobar(estado.Visto,"El onboarding termina en memoria aun si el archivo no se puede escribir"); }
finally { Directory.Delete(carpeta); }
Console.WriteLine($"{total} comprobaciones de regresión correctas. Sin producción, correos ni credenciales reales.");

sealed class Servidor(Func<HttpRequestMessage,Task<HttpResponseMessage>> ejecutar):HttpMessageHandler
{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud,CancellationToken token)=>ejecutar(solicitud); }
