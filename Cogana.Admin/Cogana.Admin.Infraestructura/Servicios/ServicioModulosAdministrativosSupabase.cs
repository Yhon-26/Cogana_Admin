using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioModulosAdministrativosSupabase(ClienteSupabaseRest cliente)
    : IServicioModulosAdministrativos
{
    public async Task<ResultadoModuloDatos> ObtenerAsync(
        string modulo,
        Guid tiendaId,
        CancellationToken cancellationToken = default)
    {
        if (!cliente.EstaConfigurado)
        {
            return ResultadoModuloDatos.Fallido(
                "La conexión con Supabase todavía no está configurada.");
        }

        try
        {
            var datos = modulo switch
            {
                "Productos" => await ObtenerProductosAsync(tiendaId, cancellationToken),
                "Categorías" => await ObtenerCategoriasAsync(tiendaId, cancellationToken),
                "Presentaciones" => await ObtenerPresentacionesAsync(tiendaId, cancellationToken),
                "Inventario y lotes" => await ObtenerInventarioAsync(tiendaId, cancellationToken),
                "Proveedores" => await ObtenerProveedoresAsync(tiendaId, cancellationToken),
                "Pedidos" => await ObtenerPedidosAsync(tiendaId, cancellationToken),
                "Clientes" => await ObtenerClientesAsync(tiendaId, cancellationToken),
                "Promociones" => await ObtenerPromocionesAsync(tiendaId, cancellationToken),
                "Reportes" => await ObtenerReportesAsync(tiendaId, cancellationToken),
                "Usuarios" => await ObtenerUsuariosAsync(tiendaId, cancellationToken),
                "Configuración" => await ObtenerConfiguracionAsync(tiendaId, cancellationToken),
                _ => new ModuloDatos("", "", "", "", "", "", [])
            };

            return ResultadoModuloDatos.Correcto(datos);
        }
        catch (OperationCanceledException)
        {
            return ResultadoModuloDatos.Fallido("La consulta fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoModuloDatos.Fallido(
                "No fue posible consultar este módulo en Supabase.");
        }
        catch (JsonException)
        {
            return ResultadoModuloDatos.Fallido(
                "Supabase devolvió información que no se pudo procesar.");
        }
    }

    private async Task<ModuloDatos> ObtenerProductosAsync(Guid tiendaId, CancellationToken token)
    {
        var filas = await ConsultarAsync(
            "/rest/v1/products?select=id,sku,name,brand,base_unit,is_active,categories(name)" +
            $"&store_id=eq.{tiendaId:D}&order=name.asc&limit=250",
            item => new FilaModuloDatos(
                GuidValor(item, "id"),
                Texto(item, "sku", "Sin SKU"),
                Texto(item, "name"),
                Texto(item, "brand", "Sin marca"),
                TextoAnidado(item, "categories", "name", "Sin categoría"),
                TraducirUnidad(Texto(item, "base_unit")),
                Booleano(item, "is_active") ? "Activo" : "Inactivo"),
            token);
        return new("SKU", "Producto", "Marca", "Categoría", "Unidad", "Nuevo producto", filas);
    }

    private async Task<ModuloDatos> ObtenerCategoriasAsync(Guid tiendaId, CancellationToken token)
    {
        var filas = await ConsultarAsync(
            "/rest/v1/categories?select=id,name,slug,sort_order,is_active,updated_at" +
            $"&store_id=eq.{tiendaId:D}&order=sort_order.asc,name.asc&limit=250",
            item => new FilaModuloDatos(
                GuidValor(item, "id"),
                Texto(item, "name"),
                Texto(item, "slug"),
                $"Orden {Entero(item, "sort_order")}",
                Booleano(item, "is_active") ? "Visible" : "Oculta",
                Fecha(item, "updated_at"),
                Booleano(item, "is_active") ? "Activa" : "Inactiva"),
            token);
        return new("Categoría", "Identificador", "Orden", "Visibilidad", "Actualizada", "Nueva categoría", filas);
    }

    private async Task<ModuloDatos> ObtenerPresentacionesAsync(Guid tiendaId, CancellationToken token)
    {
        var filas = await ConsultarAsync(
            "/rest/v1/product_presentations?select=id,sku,name,kind,base_quantity,price_cents,is_active,products(name)" +
            $"&store_id=eq.{tiendaId:D}&order=name.asc&limit=250",
            item => new FilaModuloDatos(
                GuidValor(item, "id"),
                Texto(item, "sku", "Sin SKU"),
                Texto(item, "name"),
                TextoAnidado(item, "products", "name", "Producto no disponible"),
                TraducirTipoPresentacion(Texto(item, "kind")),
                Moneda(EnteroLargo(item, "price_cents")),
                Booleano(item, "is_active") ? "Activa" : "Inactiva"),
            token);
        return new("SKU", "Presentación", "Producto", "Tipo", "Precio", "Nueva presentación", filas);
    }

    private async Task<ModuloDatos> ObtenerInventarioAsync(Guid tiendaId, CancellationToken token)
    {
        var filas = await ConsultarAsync(
            "/rest/v1/inventory_lots?select=id,lot_code,expires_on,loose_on_hand_quantity,status,updated_at,products(name),suppliers(name)" +
            $"&store_id=eq.{tiendaId:D}&order=expires_on.asc.nullslast&limit=250",
            item => new FilaModuloDatos(
                GuidValor(item, "id"),
                Texto(item, "lot_code"),
                TextoAnidado(item, "products", "name", "Producto no disponible"),
                EnteroLargo(item, "loose_on_hand_quantity").ToString("N0", CulturaPeru),
                FechaSolo(item, "expires_on", "Sin vencimiento"),
                TextoAnidado(item, "suppliers", "name", "Sin proveedor"),
                TraducirEstadoLote(Texto(item, "status"))),
            token);
        return new("Lote", "Producto", "Existencia", "Vencimiento", "Proveedor", "Registrar lote", filas);
    }

    private async Task<ModuloDatos> ObtenerProveedoresAsync(Guid tiendaId, CancellationToken token)
    {
        var filas = await ConsultarAsync(
            "/rest/v1/suppliers?select=id,name,tax_id,phone,is_active,updated_at" +
            $"&store_id=eq.{tiendaId:D}&order=name.asc&limit=250",
            item => new FilaModuloDatos(
                GuidValor(item, "id"),
                Texto(item, "name"),
                Texto(item, "tax_id", "Sin RUC"),
                Texto(item, "phone", "Sin teléfono"),
                Booleano(item, "is_active") ? "Habilitado" : "Deshabilitado",
                Fecha(item, "updated_at"),
                Booleano(item, "is_active") ? "Activo" : "Inactivo"),
            token);
        return new("Proveedor", "RUC", "Teléfono", "Disponibilidad", "Actualizado", "Nuevo proveedor", filas);
    }

    private async Task<ModuloDatos> ObtenerPedidosAsync(Guid tiendaId, CancellationToken token)
    {
        var filas = await ConsultarAsync(
            "/rest/v1/orders?select=id,order_number,customer_name,fulfillment_type,status,payment_status,estimated_total_cents,final_total_cents,created_at" +
            $"&store_id=eq.{tiendaId:D}&order=created_at.desc&limit=250",
            item => new FilaModuloDatos(
                GuidValor(item, "id"),
                Texto(item, "order_number"),
                Texto(item, "customer_name"),
                TraducirEntrega(Texto(item, "fulfillment_type")),
                TraducirEstadoPedido(Texto(item, "status")),
                Moneda(EnteroLargoNullable(item, "final_total_cents") ?? EnteroLargo(item, "estimated_total_cents")),
                TraducirPago(Texto(item, "payment_status")),
                Texto(item, "status")),
            token);
        return new("Pedido", "Cliente", "Entrega", "Estado", "Total", "Actualizar", filas);
    }

    private async Task<ModuloDatos> ObtenerClientesAsync(Guid tiendaId, CancellationToken token)
    {
        var filas = await ConsultarAsync(
            "/rest/v1/customers?select=id,name,phone,email,status,created_at" +
            $"&store_id=eq.{tiendaId:D}&order=created_at.desc&limit=250",
            item => new FilaModuloDatos(
                GuidValor(item, "id"),
                Texto(item, "name"),
                Texto(item, "phone"),
                Texto(item, "email", "Sin correo"),
                Fecha(item, "created_at"),
                Texto(item, "status"),
                TraducirEstadoCliente(Texto(item, "status"))),
            token);
        return new("Cliente", "Teléfono", "Correo", "Registro", "Estado interno", "Actualizar", filas);
    }

    private async Task<ModuloDatos> ObtenerPromocionesAsync(Guid tiendaId, CancellationToken token)
    {
        var filas = await ConsultarAsync(
            "/rest/v1/promotions?select=id,name,promotion_type,value,starts_at,ends_at,is_active" +
            $"&store_id=eq.{tiendaId:D}&order=starts_at.desc&limit=250",
            item => new FilaModuloDatos(
                GuidValor(item, "id"),
                Texto(item, "name"),
                TraducirPromocion(Texto(item, "promotion_type")),
                ValorPromocion(Texto(item, "promotion_type"), EnteroLargo(item, "value")),
                Fecha(item, "starts_at"),
                Fecha(item, "ends_at"),
                Booleano(item, "is_active") ? "Activa" : "Inactiva"),
            token);
        return new("Promoción", "Tipo", "Beneficio", "Inicio", "Fin", "Nueva promoción", filas);
    }

    private async Task<ModuloDatos> ObtenerUsuariosAsync(Guid tiendaId, CancellationToken token)
    {
        var servicioUsuarios = new ServicioUsuariosAdministrativosSupabase(cliente);
        var usuarios = await servicioUsuarios.ObtenerUsuariosAsync(tiendaId, token);
        var filas = usuarios.Select(usuario => new FilaModuloDatos(
            usuario.Id,
            usuario.NombreCompleto,
            usuario.Correo,
            TraducirRol(usuario.Rol),
            usuario.EstaActivo ? "Acceso permitido" : "Acceso suspendido",
            usuario.UltimoAcceso is null
                ? "Sin ingreso registrado"
                : usuario.UltimoAcceso.Value.LocalDateTime.ToString("dd/MM/yyyy HH:mm", CulturaPeru),
            usuario.EstaActivo ? "Activo" : "Inactivo",
            usuario.Rol)).ToList();

        return new("Usuario", "Correo", "Rol", "Acceso", "Último ingreso", "Crear acceso", filas);
    }

    private async Task<ModuloDatos> ObtenerConfiguracionAsync(Guid tiendaId, CancellationToken token)
    {
        var filas = new List<FilaModuloDatos>();
        var ajustes = await ObtenerJsonAsync(
            "/rest/v1/store_settings?select=*" + $"&store_id=eq.{tiendaId:D}&limit=1",
            token);

        if (ajustes.GetArrayLength() > 0)
        {
            var item = ajustes[0];
            filas.Add(new FilaModuloDatos(
                tiendaId,
                "Datos de tienda",
                Texto(item, "display_name"),
                Texto(item, "tax_id", "Sin RUC"),
                Texto(item, "phone", "Sin teléfono"),
                CanalesAtencion(item),
                "Configurado"));
        }

        var horarios = await ObtenerJsonAsync(
            "/rest/v1/store_business_hours?select=id,weekday,opens_at,closes_at,is_closed" +
            $"&store_id=eq.{tiendaId:D}&order=weekday.asc",
            token);
        foreach (var item in horarios.EnumerateArray())
        {
            var cerrado = Booleano(item, "is_closed");
            filas.Add(new FilaModuloDatos(
                GuidValor(item, "id"),
                "Horario",
                NombreDia(Entero(item, "weekday")),
                cerrado ? "Cerrado" : $"{Texto(item, "opens_at")} – {Texto(item, "closes_at")}",
                "Hora local",
                "Atención al público",
                cerrado ? "Cerrado" : "Abierto"));
        }

        var excepciones = await ObtenerJsonAsync(
            "/rest/v1/store_schedule_exceptions?select=id,local_date,opens_at,closes_at,is_closed,public_message" +
            $"&store_id=eq.{tiendaId:D}&order=local_date.asc&limit=100",
            token);
        foreach (var item in excepciones.EnumerateArray())
        {
            var cerrado = Booleano(item, "is_closed");
            filas.Add(new FilaModuloDatos(
                GuidValor(item, "id"),
                "Fecha especial",
                FechaSolo(item, "local_date", "Sin fecha"),
                cerrado ? "Cerrado" : $"{Texto(item, "opens_at")} – {Texto(item, "closes_at")}",
                Texto(item, "public_message", "Sin mensaje"),
                "Excepción del horario",
                cerrado ? "Cerrado" : "Abierto"));
        }

        return new("Sección", "Nombre / Día", "Valor", "Zona", "Detalle", "Editar configuración", filas);
    }

    private async Task<ModuloDatos> ObtenerReportesAsync(Guid tiendaId, CancellationToken token)
    {
        var documentos = await ObtenerJsonAsync(
            "/rest/v1/orders?select=id,status,estimated_total_cents,final_total_cents,created_at" +
            $"&store_id=eq.{tiendaId:D}&order=created_at.desc&limit=1000",
            token);
        var pedidos = documentos.EnumerateArray().ToList();
        var completados = pedidos.Where(p => Texto(p, "status") == "completed").ToList();
        var cancelados = pedidos.Count(p => Texto(p, "status") == "cancelled");
        var pendientes = pedidos.Count - completados.Count - cancelados;
        var ventas = completados.Sum(p =>
            EnteroLargoNullable(p, "final_total_cents") ?? EnteroLargo(p, "estimated_total_cents"));

        var filas = new List<FilaModuloDatos>
        {
            Metrica("Pedidos registrados", pedidos.Count.ToString("N0", CulturaPeru), "Histórico disponible", "Todos los estados"),
            Metrica("Pedidos completados", completados.Count.ToString("N0", CulturaPeru), Moneda(ventas), "Ventas cerradas"),
            Metrica("Pedidos pendientes", pendientes.ToString("N0", CulturaPeru), "Requieren seguimiento", "Operación"),
            Metrica("Pedidos cancelados", cancelados.ToString("N0", CulturaPeru), "Histórico disponible", "Cancelaciones")
        };
        return new("Indicador", "Resultado", "Importe / Nota", "Alcance", "Fuente", "Actualizar", filas);
    }

    private static FilaModuloDatos Metrica(string nombre, string valor, string nota, string alcance) =>
        new(Guid.NewGuid(), nombre, valor, nota, alcance, "Supabase", "Actualizado");

    private async Task<List<FilaModuloDatos>> ConsultarAsync(
        string ruta,
        Func<JsonElement, FilaModuloDatos> convertir,
        CancellationToken token)
    {
        var documento = await ObtenerJsonAsync(ruta, token);
        return documento.EnumerateArray().Select(convertir).ToList();
    }

    private async Task<JsonElement> ObtenerJsonAsync(string ruta, CancellationToken token)
    {
        using var respuesta = await cliente.ObtenerAsync(ruta, token);
        respuesta.EnsureSuccessStatusCode();
        using var documento = await JsonDocument.ParseAsync(
            await respuesta.Content.ReadAsStreamAsync(token),
            cancellationToken: token);
        return documento.RootElement.Clone();
    }

    private static readonly CultureInfo CulturaPeru = new("es-PE");

    private static string Texto(JsonElement item, string nombre, string alternativo = "") =>
        item.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? alternativo
            : alternativo;

    private static string TextoAnidado(
        JsonElement item,
        string objeto,
        string propiedad,
        string alternativo)
    {
        if (!item.TryGetProperty(objeto, out var anidado) ||
            anidado.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return alternativo;
        }

        if (anidado.ValueKind == JsonValueKind.Array)
        {
            anidado = anidado.EnumerateArray().FirstOrDefault();
        }

        return anidado.ValueKind == JsonValueKind.Object
            ? Texto(anidado, propiedad, alternativo)
            : alternativo;
    }

    private static Guid GuidValor(JsonElement item, string nombre) =>
        Guid.TryParse(Texto(item, nombre), out var id) ? id : Guid.Empty;

    private static bool Booleano(JsonElement item, string nombre) =>
        item.TryGetProperty(nombre, out var valor) &&
        valor.ValueKind is JsonValueKind.True;

    private static int Entero(JsonElement item, string nombre) =>
        item.TryGetProperty(nombre, out var valor) && valor.TryGetInt32(out var numero) ? numero : 0;

    private static long EnteroLargo(JsonElement item, string nombre) =>
        item.TryGetProperty(nombre, out var valor) && valor.TryGetInt64(out var numero) ? numero : 0;

    private static long? EnteroLargoNullable(JsonElement item, string nombre) =>
        item.TryGetProperty(nombre, out var valor) && valor.TryGetInt64(out var numero) ? numero : null;

    private static string Fecha(JsonElement item, string nombre) =>
        DateTimeOffset.TryParse(Texto(item, nombre), out var fecha)
            ? fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CulturaPeru)
            : "Sin fecha";

    private static string FechaSolo(JsonElement item, string nombre, string alternativo) =>
        DateOnly.TryParse(Texto(item, nombre), out var fecha)
            ? fecha.ToString("dd/MM/yyyy", CulturaPeru)
            : alternativo;

    private static string Moneda(long centimos) => $"S/ {centimos / 100m:N2}";

    private static string TraducirUnidad(string valor) => valor switch
    {
        "gram" => "Gramos",
        "unit" => "Unidad",
        "milliliter" => "Mililitros",
        _ => valor
    };

    private static string TraducirTipoPresentacion(string valor) => valor switch
    {
        "bulk" => "Granel",
        "unit" => "Unidad",
        "package" => "Paquete",
        "sack" => "Saco",
        _ => valor
    };

    private static string TraducirEstadoLote(string valor) => valor switch
    {
        "available" => "Disponible",
        "depleted" => "Agotado",
        "expired" => "Vencido",
        "blocked" => "Bloqueado",
        _ => valor
    };

    private static string TraducirEntrega(string valor) => valor switch
    {
        "pickup" => "Recojo",
        "delivery" => "Delivery",
        _ => valor
    };

    private static string TraducirEstadoPedido(string valor) => valor switch
    {
        "pending_payment" => "Pago pendiente",
        "received" => "Recibido",
        "confirmed" => "Confirmado",
        "preparing" => "En preparación",
        "ready" => "Listo",
        "out_for_delivery" => "En reparto",
        "completed" => "Completado",
        "cancelled" => "Cancelado",
        _ => valor.Replace('_', ' ')
    };

    private static string TraducirPago(string valor) => valor switch
    {
        "pending" => "Pago pendiente",
        "paid" => "Pagado",
        "failed" => "Fallido",
        "refunded" => "Reembolsado",
        _ => valor
    };

    private static string TraducirEstadoCliente(string valor) => valor switch
    {
        "active" => "Activo",
        "suspended" => "Suspendido",
        "archived" => "Archivado",
        _ => valor
    };

    private static string TraducirPromocion(string valor) => valor switch
    {
        "percentage" => "Porcentaje",
        "fixed_amount" => "Monto fijo",
        "quantity" => "Por cantidad",
        "free_delivery" => "Delivery gratis",
        _ => valor.Replace('_', ' ')
    };

    private static string ValorPromocion(string tipo, long valor) => tipo switch
    {
        "percentage" => $"{valor}%",
        "fixed_amount" => Moneda(valor),
        "free_delivery" => "100% delivery",
        _ => valor.ToString("N0", CulturaPeru)
    };

    private static string TraducirRol(string valor) => valor switch
    {
        "owner" => "Propietario",
        "admin" => "Administrador",
        "driver" => "Repartidor",
        _ => valor
    };

    private static string CanalesAtencion(JsonElement item)
    {
        var canales = new List<string>();
        if (Booleano(item, "accepts_pickup")) canales.Add("Recojo");
        if (Booleano(item, "accepts_delivery")) canales.Add("Delivery");
        return canales.Count == 0 ? "Sin canales" : string.Join(" · ", canales);
    }

    private static string NombreDia(int dia) => dia switch
    {
        0 or 7 => "Domingo",
        1 => "Lunes",
        2 => "Martes",
        3 => "Miércoles",
        4 => "Jueves",
        5 => "Viernes",
        6 => "Sábado",
        _ => $"Día {dia}"
    };
}
