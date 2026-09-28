using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioCatalogoAdministrativoSupabase(ClienteSupabaseRest cliente)
    : IServicioCatalogoAdministrativo
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<OpcionCategoria>> ObtenerCategoriasAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/categories?select=id,name" +
            $"&store_id=eq.{tiendaId:D}&is_active=eq.true&order=name.asc";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var categorias = await respuesta.Content.ReadFromJsonAsync<List<RespuestaCategoria>>(
            OpcionesJson,
            cancellationToken) ?? [];
        return categorias.Select(c => new OpcionCategoria(c.Id, c.Nombre)).ToList();
    }

    public async Task<IReadOnlyList<OpcionProducto>> ObtenerProductosAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/products?select=id,name,base_unit" +
            $"&store_id=eq.{tiendaId:D}&is_active=eq.true&order=name.asc";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var productos = await respuesta.Content.ReadFromJsonAsync<List<RespuestaProducto>>(
            OpcionesJson,
            cancellationToken) ?? [];
        return productos.Select(p => new OpcionProducto(p.Id, p.Nombre, p.UnidadBase)).ToList();
    }

    public async Task<IReadOnlyList<OpcionProveedor>> ObtenerProveedoresAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/suppliers?select=id,name" +
            $"&store_id=eq.{tiendaId:D}&is_active=eq.true&order=name.asc";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var proveedores = await respuesta.Content.ReadFromJsonAsync<List<RespuestaProveedor>>(
            OpcionesJson,
            cancellationToken) ?? [];
        return proveedores.Select(p => new OpcionProveedor(p.Id, p.Nombre)).ToList();
    }

    public async Task<ProductoDetalle?> ObtenerProductoAsync(
        Guid tiendaId,
        Guid productoId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/products" +
            "?select=id,name,sku,brand,description,category_id,base_unit,tracks_expiration,minimum_stock_quantity,is_active," +
            "product_presentations(id,is_active),inventory_lots(id,status,loose_on_hand_quantity)" +
            $"&store_id=eq.{tiendaId:D}&id=eq.{productoId:D}&limit=1";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var productos = await respuesta.Content.ReadFromJsonAsync<List<RespuestaProductoDetalle>>(
            OpcionesJson,
            cancellationToken) ?? [];
        var producto = productos.FirstOrDefault();
        if (producto is null)
        {
            return null;
        }

        var lotesDisponibles = producto.Lotes
            .Where(lote => lote.Estado == "available" && lote.Cantidad > 0)
            .ToList();
        return new ProductoDetalle(
            producto.Id,
            producto.Nombre,
            producto.Sku,
            producto.Marca,
            producto.Descripcion,
            producto.CategoriaId,
            producto.UnidadBase,
            producto.ControlaVencimiento,
            producto.StockMinimo,
            producto.EstaActivo,
            producto.Presentaciones.Count(p => p.EstaActiva),
            lotesDisponibles.Count,
            lotesDisponibles.Sum(lote => lote.Cantidad));
    }

    public async Task<CategoriaDetalle?> ObtenerCategoriaAsync(
        Guid tiendaId,
        Guid categoriaId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/categories?select=id,name,slug,sort_order,is_active" +
            $"&store_id=eq.{tiendaId:D}&id=eq.{categoriaId:D}&limit=1";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var categorias = await respuesta.Content.ReadFromJsonAsync<List<RespuestaCategoriaDetalle>>(
            OpcionesJson,
            cancellationToken) ?? [];
        var categoria = categorias.FirstOrDefault();
        return categoria is null
            ? null
            : new CategoriaDetalle(
                categoria.Id,
                categoria.Nombre,
                categoria.Identificador,
                categoria.Orden,
                categoria.EstaActiva);
    }

    public async Task<PresentacionDetalle?> ObtenerPresentacionAsync(
        Guid tiendaId,
        Guid presentacionId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/product_presentations" +
            "?select=id,product_id,name,sku,kind,base_quantity,price_cents,is_sealed,is_active" +
            $"&store_id=eq.{tiendaId:D}&id=eq.{presentacionId:D}&limit=1";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var presentaciones = await respuesta.Content.ReadFromJsonAsync<List<RespuestaPresentacionDetalle>>(
            OpcionesJson,
            cancellationToken) ?? [];
        var presentacion = presentaciones.FirstOrDefault();
        return presentacion is null
            ? null
            : new PresentacionDetalle(
                presentacion.Id,
                presentacion.ProductoId,
                presentacion.Nombre,
                presentacion.Sku,
                presentacion.Tipo,
                presentacion.CantidadBase,
                presentacion.PrecioCentimos,
                presentacion.Sellada,
                presentacion.EstaActiva);
    }

    public async Task<ProveedorDetalle?> ObtenerProveedorAsync(
        Guid tiendaId,
        Guid proveedorId,
        CancellationToken cancellationToken = default)
    {
        var ruta =
            "/rest/v1/suppliers?select=id,name,tax_id,phone,is_active" +
            $"&store_id=eq.{tiendaId:D}&id=eq.{proveedorId:D}&limit=1";
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var proveedores = await respuesta.Content.ReadFromJsonAsync<List<RespuestaProveedorDetalle>>(
            OpcionesJson,
            cancellationToken) ?? [];
        var proveedor = proveedores.FirstOrDefault();
        return proveedor is null
            ? null
            : new ProveedorDetalle(
                proveedor.Id,
                proveedor.Nombre,
                proveedor.Ruc,
                proveedor.Telefono,
                proveedor.EstaActivo);
    }

    public Task<ResultadoOperacion> CrearProductoAsync(
        Guid tiendaId,
        NuevoProducto producto,
        CancellationToken cancellationToken = default) =>
        InsertarAsync(
            "/rest/v1/products",
            new
            {
                store_id = tiendaId,
                category_id = producto.CategoriaId,
                sku = LimpiarOpcional(producto.Sku),
                name = producto.Nombre.Trim(),
                brand = LimpiarOpcional(producto.Marca),
                description = LimpiarOpcional(producto.Descripcion),
                base_unit = producto.UnidadBase,
                tracks_expiration = producto.ControlaVencimiento,
                minimum_stock_quantity = producto.StockMinimo,
                is_active = true
            },
            "Producto registrado correctamente.",
            cancellationToken);

    public async Task<ResultadoOperacion> ActualizarProductoAsync(
        Guid tiendaId,
        Guid productoId,
        NuevoProducto producto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/rest/v1/products?id=eq.{productoId:D}&store_id=eq.{tiendaId:D}")
            {
                Content = JsonContent.Create(new
                {
                    category_id = producto.CategoriaId,
                    sku = LimpiarOpcional(producto.Sku),
                    name = producto.Nombre.Trim(),
                    brand = LimpiarOpcional(producto.Marca),
                    description = LimpiarOpcional(producto.Descripcion),
                    base_unit = producto.UnidadBase,
                    tracks_expiration = producto.ControlaVencimiento,
                    minimum_stock_quantity = producto.StockMinimo,
                    updated_at = DateTimeOffset.UtcNow
                })
            };
            solicitud.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            if (respuesta.IsSuccessStatusCode)
            {
                return ResultadoOperacion.Correcta("Producto actualizado correctamente.");
            }

            return ResultadoOperacion.Fallida(await LeerErrorAsync(respuesta, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida("No fue posible actualizar el producto.");
        }
    }

    public Task<ResultadoOperacion> ActualizarCategoriaAsync(
        Guid tiendaId,
        Guid categoriaId,
        NuevaCategoria categoria,
        CancellationToken cancellationToken = default) =>
        ActualizarAsync(
            "categories",
            tiendaId,
            categoriaId,
            new
            {
                name = categoria.Nombre.Trim(),
                slug = CrearSlug(categoria.Nombre),
                sort_order = categoria.Orden,
                updated_at = DateTimeOffset.UtcNow
            },
            "Categoría actualizada correctamente.",
            cancellationToken);

    public Task<ResultadoOperacion> ActualizarPresentacionAsync(
        Guid tiendaId,
        Guid presentacionId,
        NuevaPresentacion presentacion,
        CancellationToken cancellationToken = default)
    {
        var esGranel = presentacion.Tipo == "bulk";
        return ActualizarAsync(
            "product_presentations",
            tiendaId,
            presentacionId,
            new
            {
                product_id = presentacion.ProductoId,
                sku = LimpiarOpcional(presentacion.Sku),
                name = presentacion.Nombre.Trim(),
                kind = presentacion.Tipo,
                base_quantity = presentacion.CantidadBase,
                price_mode = esGranel ? "proportional" : "fixed",
                price_cents = presentacion.PrecioCentimos,
                allows_quantity_input = esGranel,
                allows_money_input = esGranel,
                is_sealed = presentacion.Sellada,
                updated_at = DateTimeOffset.UtcNow
            },
            "Presentación actualizada correctamente.",
            cancellationToken);
    }

    public Task<ResultadoOperacion> ActualizarProveedorAsync(
        Guid tiendaId,
        Guid proveedorId,
        NuevoProveedor proveedor,
        CancellationToken cancellationToken = default) =>
        ActualizarAsync(
            "suppliers",
            tiendaId,
            proveedorId,
            new
            {
                name = proveedor.Nombre.Trim(),
                tax_id = LimpiarOpcional(proveedor.Ruc),
                phone = LimpiarOpcional(proveedor.Telefono),
                updated_at = DateTimeOffset.UtcNow
            },
            "Proveedor actualizado correctamente.",
            cancellationToken);

    public Task<ResultadoOperacion> CrearCategoriaAsync(
        Guid tiendaId,
        NuevaCategoria categoria,
        CancellationToken cancellationToken = default) =>
        InsertarAsync(
            "/rest/v1/categories",
            new
            {
                store_id = tiendaId,
                name = categoria.Nombre.Trim(),
                slug = CrearSlug(categoria.Nombre),
                sort_order = categoria.Orden,
                is_active = true
            },
            "Categoría registrada correctamente.",
            cancellationToken);

    public Task<ResultadoOperacion> CrearProveedorAsync(
        Guid tiendaId,
        NuevoProveedor proveedor,
        CancellationToken cancellationToken = default) =>
        InsertarAsync(
            "/rest/v1/suppliers",
            new
            {
                store_id = tiendaId,
                name = proveedor.Nombre.Trim(),
                tax_id = LimpiarOpcional(proveedor.Ruc),
                phone = LimpiarOpcional(proveedor.Telefono),
                is_active = true
            },
            "Proveedor registrado correctamente.",
            cancellationToken);

    public Task<ResultadoOperacion> CrearPresentacionAsync(
        Guid tiendaId,
        NuevaPresentacion presentacion,
        CancellationToken cancellationToken = default)
    {
        var esGranel = presentacion.Tipo == "bulk";
        return InsertarAsync(
            "/rest/v1/product_presentations",
            new
            {
                store_id = tiendaId,
                product_id = presentacion.ProductoId,
                sku = LimpiarOpcional(presentacion.Sku),
                name = presentacion.Nombre.Trim(),
                kind = presentacion.Tipo,
                base_quantity = presentacion.CantidadBase,
                price_mode = esGranel ? "proportional" : "fixed",
                price_cents = presentacion.PrecioCentimos,
                allows_quantity_input = esGranel,
                allows_money_input = esGranel,
                minimum_quantity = 1,
                quantity_step = 1,
                is_sealed = presentacion.Sellada,
                is_active = true
            },
            "Presentación registrada correctamente.",
            cancellationToken);
    }

    public Task<ResultadoOperacion> CrearPromocionAsync(
        Guid tiendaId,
        NuevaPromocion promocion,
        CancellationToken cancellationToken = default) =>
        InsertarAsync(
            "/rest/v1/promotions",
            new
            {
                store_id = tiendaId,
                name = promocion.Nombre.Trim(),
                description = LimpiarOpcional(promocion.Descripcion),
                promotion_type = promocion.Tipo,
                value = promocion.Valor,
                minimum_order_cents = 0,
                starts_at = promocion.Inicio,
                ends_at = promocion.Fin,
                priority = 0,
                is_stackable = false,
                is_active = true
            },
            "Promoción registrada correctamente.",
            cancellationToken);

    public async Task<ResultadoOperacion> RegistrarLoteAsync(
        Guid tiendaId,
        NuevoLote lote,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post,
                "/rest/v1/rpc/register_inventory_lot")
            {
                Content = JsonContent.Create(new
                {
                    p_store_id = tiendaId,
                    p_product_id = lote.ProductoId,
                    p_supplier_id = lote.ProveedorId,
                    p_lot_code = lote.Codigo.Trim(),
                    p_expires_on = lote.Vencimiento,
                    p_quantity = lote.Cantidad,
                    p_operation_id = lote.OperacionId,
                    p_notes = LimpiarOpcional(lote.Notas)
                })
            };
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            if (respuesta.IsSuccessStatusCode)
            {
                return ResultadoOperacion.Correcta("Lote registrado correctamente.");
            }

            return ResultadoOperacion.Fallida(await LeerErrorAsync(respuesta, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida("No fue posible registrar el lote.");
        }
    }

    public async Task<ResultadoOperacion> CambiarEstadoAsync(
        Guid tiendaId,
        string modulo,
        Guid registroId,
        bool activo,
        CancellationToken cancellationToken = default)
    {
        var tabla = modulo switch
        {
            "Productos" => "products",
            "Categorías" => "categories",
            "Presentaciones" => "product_presentations",
            "Proveedores" => "suppliers",
            "Promociones" => "promotions",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(tabla))
        {
            return ResultadoOperacion.Fallida("Este módulo no admite el cambio solicitado.");
        }

        try
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/rest/v1/{tabla}?id=eq.{registroId:D}&store_id=eq.{tiendaId:D}")
            {
                Content = JsonContent.Create(new
                {
                    is_active = activo,
                    updated_at = DateTimeOffset.UtcNow
                })
            };
            solicitud.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            if (respuesta.IsSuccessStatusCode)
            {
                return ResultadoOperacion.Correcta(
                    activo ? "Registro activado correctamente." : "Registro desactivado correctamente.");
            }

            return ResultadoOperacion.Fallida(await LeerErrorAsync(respuesta, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida("No fue posible cambiar el estado del registro.");
        }
    }

    private async Task<ResultadoOperacion> InsertarAsync(
        string ruta,
        object contenido,
        string mensajeExito,
        CancellationToken cancellationToken)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta)
            {
                Content = JsonContent.Create(contenido)
            };
            solicitud.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);

            if (respuesta.IsSuccessStatusCode)
            {
                return ResultadoOperacion.Correcta(mensajeExito);
            }

            var error = await LeerErrorAsync(respuesta, cancellationToken);
            return ResultadoOperacion.Fallida(error);
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida(
                "No fue posible guardar la información en Supabase.");
        }
    }

    private async Task<ResultadoOperacion> ActualizarAsync(
        string tabla,
        Guid tiendaId,
        Guid registroId,
        object contenido,
        string mensajeExito,
        CancellationToken cancellationToken)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/rest/v1/{tabla}?id=eq.{registroId:D}&store_id=eq.{tiendaId:D}")
            {
                Content = JsonContent.Create(contenido)
            };
            solicitud.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            return respuesta.IsSuccessStatusCode
                ? ResultadoOperacion.Correcta(mensajeExito)
                : ResultadoOperacion.Fallida(await LeerErrorAsync(respuesta, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida("No fue posible actualizar la información en Supabase.");
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

            if (error?.Codigo == "23505")
            {
                return "Ya existe un registro con esos datos.";
            }

            if (error?.Codigo == "42501")
            {
                return "La cuenta no tiene permiso para realizar esta operación.";
            }

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

    private static string? LimpiarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string CrearSlug(string texto)
    {
        var normalizado = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder();
        var separadorPendiente = false;

        foreach (var caracter in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caracter))
            {
                if (separadorPendiente && resultado.Length > 0)
                {
                    resultado.Append('-');
                }

                resultado.Append(caracter);
                separadorPendiente = false;
            }
            else
            {
                separadorPendiente = true;
            }
        }

        return resultado.ToString();
    }

    private sealed class RespuestaCategoria
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;
    }

    private sealed class RespuestaCategoriaDetalle
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;

        [JsonPropertyName("slug")]
        public string Identificador { get; init; } = string.Empty;

        [JsonPropertyName("sort_order")]
        public int Orden { get; init; }

        [JsonPropertyName("is_active")]
        public bool EstaActiva { get; init; }
    }

    private sealed class RespuestaProducto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;

        [JsonPropertyName("base_unit")]
        public string UnidadBase { get; init; } = string.Empty;
    }

    private sealed class RespuestaProductoDetalle
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;

        [JsonPropertyName("sku")]
        public string? Sku { get; init; }

        [JsonPropertyName("brand")]
        public string? Marca { get; init; }

        [JsonPropertyName("description")]
        public string? Descripcion { get; init; }

        [JsonPropertyName("category_id")]
        public Guid? CategoriaId { get; init; }

        [JsonPropertyName("base_unit")]
        public string UnidadBase { get; init; } = string.Empty;

        [JsonPropertyName("tracks_expiration")]
        public bool ControlaVencimiento { get; init; }

        [JsonPropertyName("minimum_stock_quantity")]
        public long StockMinimo { get; init; }

        [JsonPropertyName("is_active")]
        public bool EstaActivo { get; init; }

        [JsonPropertyName("product_presentations")]
        public List<RespuestaPresentacionBreve> Presentaciones { get; init; } = [];

        [JsonPropertyName("inventory_lots")]
        public List<RespuestaLoteBreve> Lotes { get; init; } = [];
    }

    private sealed class RespuestaPresentacionBreve
    {
        [JsonPropertyName("is_active")]
        public bool EstaActiva { get; init; }
    }

    private sealed class RespuestaLoteBreve
    {
        [JsonPropertyName("status")]
        public string Estado { get; init; } = string.Empty;

        [JsonPropertyName("loose_on_hand_quantity")]
        public long Cantidad { get; init; }
    }

    private sealed class RespuestaProveedor
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;
    }

    private sealed class RespuestaPresentacionDetalle
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("product_id")]
        public Guid ProductoId { get; init; }

        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;

        [JsonPropertyName("sku")]
        public string? Sku { get; init; }

        [JsonPropertyName("kind")]
        public string Tipo { get; init; } = string.Empty;

        [JsonPropertyName("base_quantity")]
        public long CantidadBase { get; init; }

        [JsonPropertyName("price_cents")]
        public long PrecioCentimos { get; init; }

        [JsonPropertyName("is_sealed")]
        public bool Sellada { get; init; }

        [JsonPropertyName("is_active")]
        public bool EstaActiva { get; init; }
    }

    private sealed class RespuestaProveedorDetalle
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("name")]
        public string Nombre { get; init; } = string.Empty;

        [JsonPropertyName("tax_id")]
        public string? Ruc { get; init; }

        [JsonPropertyName("phone")]
        public string? Telefono { get; init; }

        [JsonPropertyName("is_active")]
        public bool EstaActivo { get; init; }
    }

    private sealed class RespuestaError
    {
        [JsonPropertyName("code")]
        public string? Codigo { get; init; }

        [JsonPropertyName("message")]
        public string? Mensaje { get; init; }
    }
}
