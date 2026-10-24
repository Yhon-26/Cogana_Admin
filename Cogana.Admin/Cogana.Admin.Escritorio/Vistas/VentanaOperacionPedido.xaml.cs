using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaOperacionPedido : Window
{
    private readonly IServicioPedidosAdministrativos _consulta;
    private readonly IServicioOperacionPedidos _operaciones;
    private readonly Guid _tienda, _pedidoId;
    private readonly CancellationTokenSource _cierre = new();
    private readonly Dictionary<string, Guid> _intentos = [];
    private readonly Dictionary<string, DateTimeOffset> _vencimientos = [];
    private PedidoDetalle? _pedido;
    private EntregaPedidoDetalle? _entrega;
    private List<FilaPreparacion> _cantidades = [];
    private bool _ocupada, _cerrada;
    public bool HuboCambios { get; private set; }

    public VentanaOperacionPedido(IServicioPedidosAdministrativos consulta, IServicioOperacionPedidos operaciones, Guid tienda, Guid pedido)
    {
        _consulta=consulta; _operaciones=operaciones; _tienda=tienda; _pedidoId=pedido;
        InitializeComponent();
        Loaded += async (_,_) => await CargarAsync();
        Closing += (_, e) => { if (_ocupada) { e.Cancel=true; Mensaje.Text="Espera a que termine la operación antes de cerrar."; } };
        Closed += (_,_) => { _cerrada=true; _cierre.Cancel(); _cierre.Dispose(); Codigo.Clear(); };
    }

    private async Task CargarAsync()
    {
        if (_ocupada || _cerrada) return;
        Ocupar(true); Mensaje.Text="Consultando el estado actual…";
        try
        {
            var pedido=_consulta.ObtenerPedidoAsync(_tienda,_pedidoId,_cierre.Token);
            var articulos=_consulta.ObtenerItemsAsync(_tienda,_pedidoId,_cierre.Token);
            var pagos=_consulta.ObtenerPagosAsync(_tienda,_pedidoId,_cierre.Token);
            var entrega=_consulta.ObtenerEntregaAsync(_tienda,_pedidoId,_cierre.Token);
            var sustituciones=_operaciones.ObtenerSustitucionesAsync(_tienda,_pedidoId,_cierre.Token);
            await Task.WhenAll(pedido,articulos,pagos,entrega,sustituciones);
            _pedido=await pedido; _entrega=await entrega;
            if (_pedido is null) { Mensaje.Text="El pedido ya no está disponible."; Pasos.IsEnabled=false; return; }
            Titulo.Text="Atender pedido "+_pedido.Numero;
            Resumen.Text=$"{_pedido.NombreCliente} · {_pedido.TipoEntrega} · {TextoEstado(_pedido.Estado)} · Pago: {TextoEstado(_pedido.EstadoPago)}";
            SeccionRepartidor.Visibility=EtiquetaDestinatario.Visibility=Destinatario.Visibility=_pedido.TipoEntrega=="Delivery"?Visibility.Visible:Visibility.Collapsed;
            CompletarPedido.Content=_pedido.TipoEntrega=="Recojo"?"Verificar código y confirmar recojo":"Verificar código y completar entrega";
            Pago.ItemsSource=(await pagos).Where(x=>x.CodigoEstado is "pending" or "requires_action" or "processing")
                .Where(x=>x.Proveedor is "manual_yape" or "manual_plin" or "pending_adjustment" or "pending_refund").ToList();
            Pago.SelectedIndex=Pago.Items.Count>0 ? 0 : -1;
            _cantidades=(await articulos).Select(x=>new FilaPreparacion(x)).ToList();
            Cantidades.ItemsSource=_cantidades;
            ArticuloSustitucion.ItemsSource=_cantidades;
            Sustitucion.ItemsSource=(await sustituciones).Where(x=>x.Estado=="pending").Select(x=>x with
                {Articulo=_cantidades.FirstOrDefault(a=>a.Id==x.ArticuloId)?.Producto??"Artículo"}).ToList();
            if (_pedido.TipoEntrega=="Delivery" && _entrega is null && _pedido.Estado=="ready_for_delivery")
                Repartidor.ItemsSource=await _operaciones.ObtenerRepartidoresAsync(_tienda,_cierre.Token);
            else Repartidor.ItemsSource=null;
            Mensaje.Text="Selecciona el paso que necesitas. Los importes y permisos se validan al guardar.";
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or OperationCanceledException)
        { _pedido=null; Pasos.IsEnabled=false; Mensaje.Text="No se pudo consultar el pedido. Revisa la conexión y pulsa Actualizar pedido."; }
        finally { Ocupar(false); }
    }

    private void Ocupar(bool valor)
    {
        _ocupada=valor; Actualizar.IsEnabled=Cerrar.IsEnabled=!valor;
        Pasos.IsEnabled=!valor && _pedido is not null;
        if (valor) return;
        ActualizarAcciones();
    }

    private void ActualizarAcciones()
    {
        if (_pedido is null) return;
        var pago=Pago.SelectedItem as PagoPedidoResumen;
        var terminal=_pedido.Estado is "completed" or "cancelled";
        ConfirmarPago.IsEnabled=!terminal && pago is { CodigoTipo:"charge", Proveedor: "manual_yape" or "manual_plin" };
        RechazarPago.IsEnabled=ConfirmarPago.IsEnabled && _pedido.Estado=="pending_payment";
        LiquidarDiferencia.IsEnabled=!terminal && pago?.CodigoTipo is "refund" or "adjustment";
        FinalizarPreparacion.IsEnabled=!terminal && _pedido.Estado is "preparing" or "awaiting_adjustment" && _pedido.TotalFinalCentimos is null;
        Cantidades.IsReadOnly=!FinalizarPreparacion.IsEnabled;
        PanelSustituciones.IsEnabled=_pedido.Estado is "confirmed" or "preparing" or "awaiting_adjustment" && _pedido.TotalFinalCentimos is null;
        AsignarRepartidor.IsEnabled=_pedido.TipoEntrega=="Delivery" && _pedido.Estado=="ready_for_delivery" && _entrega is null;
        CompletarPedido.IsEnabled=_pedido.Estado=="ready_for_pickup" || (_pedido.Estado is "ready_for_delivery" or "out_for_delivery" && _entrega is not null);
    }

    private void AlSeleccionarPago(object sender, SelectionChangedEventArgs e)
    {
        if (Referencia is null) return;
        Referencia.Text=(Pago.SelectedItem as PagoPedidoResumen)?.Referencia ?? string.Empty;
        ActualizarAcciones();
    }

    private bool ReferenciaValida()
    {
        if (!string.IsNullOrWhiteSpace(Referencia.Text)) return true;
        Mensaje.Text="Escribe la referencia verificada o el motivo antes de continuar."; Referencia.Focus(); return false;
    }

    private async void AlConfirmarPago(object sender, RoutedEventArgs e)
    {
        if (Pago.SelectedItem is not PagoPedidoResumen pago || !ReferenciaValida()) return;
        if (!Confirmar("Confirma que verificaste el ingreso de "+pago.Importe+" en "+pago.Metodo+".")) return;
        var referencia=Referencia.Text.Trim();
        await EjecutarAsync("cobro|"+pago.Id+"|"+referencia, id=>_operaciones.ConfirmarPagoAsync(_tienda,_pedidoId,pago.Id,referencia,id,_cierre.Token));
    }
    private async void AlRechazarPago(object sender, RoutedEventArgs e)
    {
        if (Pago.SelectedItem is not PagoPedidoResumen pago || !ReferenciaValida()) return;
        if (!Confirmar("Se rechazará el cobro y se cancelará el pedido. ¿Deseas continuar?")) return;
        var motivo=Referencia.Text.Trim();
        await EjecutarAsync("rechazo|"+pago.Id+"|"+motivo,id=>_operaciones.RechazarPagoAsync(_tienda,pago.Id,motivo,id,_cierre.Token));
    }
    private async void AlLiquidar(object sender, RoutedEventArgs e)
    {
        if (Pago.SelectedItem is not PagoPedidoResumen pago || !ReferenciaValida()) return;
        if (!Confirmar("Confirma que realizaste y verificaste el movimiento de "+pago.Importe+" ("+pago.Tipo+").")) return;
        var referencia=Referencia.Text.Trim();
        await EjecutarAsync("diferencia|"+pago.Id+"|"+referencia,id=>_operaciones.LiquidarDiferenciaAsync(_tienda,pago.Id,referencia,id,_cierre.Token));
    }
    private async void AlPreparar(object sender, RoutedEventArgs e)
    {
        Cantidades.CommitEdit(DataGridEditingUnit.Cell,true); Cantidades.CommitEdit(DataGridEditingUnit.Row,true);
        var valores=new List<CantidadPreparada>();
        foreach(var fila in _cantidades)
        {
            if (!long.TryParse(fila.Cantidad, NumberStyles.None, CultureInfo.InvariantCulture, out var cantidad))
            { Mensaje.Text="Cada cantidad debe ser un entero igual o mayor que cero en la unidad indicada."; return; }
            valores.Add(new(fila.Id,cantidad));
        }
        if (valores.Count==0 || valores.All(x=>x.Cantidad==0)) { Mensaje.Text="Prepara al menos un artículo o cancela el pedido desde el detalle."; return; }
        if (!Confirmar("Se confirmarán las cantidades y se consumirá el inventario reservado. El servidor calculará los importes finales. ¿Continuar?")) return;
        var clave="preparacion|"+string.Join(';',valores.Select(x=>x.ArticuloId+":"+x.Cantidad));
        await EjecutarAsync(clave,id=>_operaciones.FinalizarPreparacionAsync(_tienda,_pedidoId,valores,id,_cierre.Token));
    }
    private async void AlAsignar(object sender, RoutedEventArgs e)
    {
        if (Repartidor.SelectedItem is not UsuarioAdministrativoDetalle persona) { Mensaje.Text="Selecciona al personal responsable."; return; }
        await EjecutarAsync("reparto|"+persona.Id,id=>_operaciones.AsignarRepartidorAsync(_tienda,_pedidoId,persona.Id,id,_cierre.Token));
    }
    private async void AlBuscarReemplazo(object sender,RoutedEventArgs e)
    {
        if(_ocupada)return;Ocupar(true);
        try{Reemplazo.ItemsSource=await _operaciones.BuscarReemplazosAsync(_tienda,BusquedaReemplazo.Text,_cierre.Token);Mensaje.Text=$"{Reemplazo.Items.Count} coincidencias de presentación.";}
        catch(Exception error)when(error is HttpRequestException or JsonException or OperationCanceledException){Mensaje.Text="No se pudieron buscar los reemplazos. Intenta nuevamente.";}
        finally{Ocupar(false);}
    }
    private async void AlProponer(object sender,RoutedEventArgs e)
    {
        if(ArticuloSustitucion.SelectedItem is not FilaPreparacion articulo || Reemplazo.SelectedItem is not PresentacionSustitucion reemplazo)
        {Mensaje.Text="Selecciona el artículo y la presentación de reemplazo.";return;}
        if(!long.TryParse(CantidadReemplazo.Text,out var cantidad)||cantidad<=0 || !int.TryParse(PlazoReemplazo.Text,out var minutos)||minutos is <1 or >1440)
        {Mensaje.Text="Revisa la cantidad base y el plazo de respuesta.";return;}
        if(!Confirmar("Se propondrá "+reemplazo.Descripcion+" con cantidad base "+cantidad+". Se aplicará la política de sustitución elegida por el cliente. ¿Continuar?"))return;
        var clave=$"sustitucion|{articulo.Id}|{reemplazo.Id}|{cantidad}|{minutos}";
        if(!_vencimientos.TryGetValue(clave,out var vence))_vencimientos[clave]=vence=DateTimeOffset.UtcNow.AddMinutes(minutos);
        await EjecutarAsync(clave,id=>_operaciones.ProponerSustitucionAsync(_tienda,_pedidoId,articulo.Id,reemplazo.Id,cantidad,vence,id,_cierre.Token));
    }
    private async Task DecidirSustitucionAsync(bool aceptar)
    {
        if(Sustitucion.SelectedItem is not SustitucionPedido propuesta){Mensaje.Text="Selecciona una propuesta pendiente.";return;}
        if(!Confirmar(aceptar?"Confirma que el cliente aceptó esta propuesta de reemplazo.":"Confirma que el cliente rechazó esta propuesta de reemplazo."))return;
        await EjecutarAsync($"decision|{propuesta.Id}|{aceptar}",id=>_operaciones.DecidirSustitucionAsync(_tienda,propuesta.Id,aceptar,id,_cierre.Token));
    }
    private async void AlAceptarSustitucion(object sender,RoutedEventArgs e)=>await DecidirSustitucionAsync(true);
    private async void AlRechazarSustitucion(object sender,RoutedEventArgs e)=>await DecidirSustitucionAsync(false);
    private async void AlCompletar(object sender, RoutedEventArgs e)
    {
        var codigo=Codigo.Password.Trim();
        if (codigo.Length==0) { Mensaje.Text="Solicita el código al cliente e introdúcelo aquí."; Codigo.Focus(); return; }
        if (!Confirmar("Confirma que entregaste el pedido y recibiste el importe que corresponde. Se verificará el código del cliente.")) return;
        var destinatario=Destinatario.Text.Trim();
        // La clave de idempotencia no almacena el código secreto.
        await EjecutarAsync("completar",id=>_pedido?.TipoEntrega=="Recojo"
            ? _operaciones.CompletarRecojoAsync(_tienda,_pedidoId,codigo,id,_cierre.Token)
            : _operaciones.CompletarEntregaAsync(_tienda,_entrega!.Id,codigo,destinatario,id,_cierre.Token));
        Codigo.Clear();
    }

    private async Task EjecutarAsync(string clave, Func<Guid,Task<ResultadoOperacion>> operacion)
    {
        if (_ocupada || _cerrada) return;
        if (!_intentos.TryGetValue(clave,out var id)) _intentos[clave]=id=Guid.NewGuid();
        Ocupar(true); Mensaje.Text="Registrando operación…";
        ResultadoOperacion resultado;
        try { resultado=await operacion(id); }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException)
        { resultado=ResultadoOperacion.Fallida("No se pudo confirmar la operación. Actualiza el pedido antes de reintentar."); }
        finally { Ocupar(false); }
        if (resultado.EsExitoso) { HuboCambios=true; await CargarAsync(); }
        Mensaje.Text=resultado.Mensaje;
    }

    private bool Confirmar(string texto)=>MessageBox.Show(this,texto,"Confirmar operación",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
    private static string TextoEstado(string estado)=>estado switch
    {
        "pending_payment"=>"Pago pendiente", "queued"=>"En cola", "confirmed"=>"Confirmado", "preparing"=>"En preparación",
        "awaiting_adjustment"=>"Esperando diferencia", "ready_for_pickup"=>"Listo para recojo", "ready_for_delivery"=>"Listo para delivery",
        "out_for_delivery"=>"En reparto", "completed"=>"Completado", "cancelled"=>"Cancelado", "paid"=>"Pagado", "pending"=>"Pendiente",
        "processing"=>"En verificación", "requires_action"=>"Requiere acción", "refunded"=>"Reembolsado", "partially_refunded"=>"Reembolso parcial", _=>estado
    };
    private async void AlActualizar(object sender,RoutedEventArgs e)=>await CargarAsync();
    private void AlCerrar(object sender,RoutedEventArgs e)=>Close();

    public sealed class FilaPreparacion(ItemPedidoDetalle item)
    {
        public Guid Id { get; }=item.Id;
        public string Producto { get; }=item.Producto+" · "+item.Presentacion;
        public string Unidad { get; }=item.UnidadBase switch { "gram"=>"g", "milliliter"=>"ml", _=>"unid." };
        public string Cantidad { get; set; }=(item.CantidadPreparada??item.CantidadEstimada).ToString(CultureInfo.InvariantCulture);
    }
}
