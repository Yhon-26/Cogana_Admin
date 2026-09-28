using System.Net.Http;
using System.Windows;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaDetalleCliente : Window
{
    private readonly IServicioClientesAdministrativos _servicio;
    private readonly Guid _tiendaId;
    private readonly Guid _clienteId;

    public VentanaDetalleCliente(
        IServicioClientesAdministrativos servicio,
        Guid tiendaId,
        Guid clienteId)
    {
        _servicio = servicio;
        _tiendaId = tiendaId;
        _clienteId = clienteId;
        InitializeComponent();
        Loaded += AlCargar;
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargar;
        MensajeVentana.Text = "Cargando ficha del cliente desde Supabase...";

        try
        {
            var clienteTask = _servicio.ObtenerClienteAsync(_tiendaId, _clienteId);
            var direccionesTask = _servicio.ObtenerDireccionesAsync(_tiendaId, _clienteId);
            var preferenciasTask = _servicio.ObtenerPreferenciasAsync(_tiendaId, _clienteId);
            var pedidosTask = _servicio.ObtenerPedidosAsync(_tiendaId, _clienteId);
            await Task.WhenAll(clienteTask, direccionesTask, preferenciasTask, pedidosTask);

            var cliente = await clienteTask;
            if (cliente is null)
            {
                MensajeVentana.Text = "El cliente ya no está disponible o no pertenece a esta tienda.";
                return;
            }

            var direcciones = await direccionesTask;
            var preferencias = await preferenciasTask;
            var pedidos = await pedidosTask;
            MostrarCliente(cliente);
            MostrarPreferencias(preferencias);
            ListaDirecciones.ItemsSource = direcciones;
            ListaPedidos.ItemsSource = pedidos;

            TotalDirecciones.Text = direcciones.Count.ToString("N0");
            TotalPedidos.Text = pedidos.Count.ToString("N0");
            TotalAcumulado.Text = $"S/ {pedidos.Sum(pedido => pedido.TotalCentimos) / 100m:N2}";
            ResumenDirecciones.Text = direcciones.Count == 0
                ? "El cliente no tiene direcciones registradas."
                : $"{direcciones.Count:N0} direcciones registradas.";
            ResumenPedidos.Text = pedidos.Count == 0
                ? "El cliente todavía no tiene pedidos."
                : $"Últimos {pedidos.Count:N0} pedidos, del más reciente al más antiguo.";
            MensajeVentana.Text = "Los datos personales se muestran solo para la operación de la tienda.";
        }
        catch (HttpRequestException)
        {
            MensajeVentana.Text = "No fue posible cargar la ficha. Revisa la conexión con Supabase.";
        }
    }

    private void MostrarCliente(ClienteDetalle cliente)
    {
        NombreCliente.Text = cliente.Nombre;
        InicialCliente.Text = CrearIniciales(cliente.Nombre);
        TelefonoCliente.Text = cliente.Telefono;
        CorreoCliente.Text = cliente.Correo ?? "Sin correo";
        TextoEstado.Text = TraducirEstado(cliente.Estado);
        ClienteDesde.Text = cliente.CreadoEn.ToLocalTime().ToString("dd MMM yyyy");
        TelefonoVerificado.Text = cliente.TelefonoVerificadoEn is null ? "No" : "Sí";
        RestriccionEfectivo.Text = cliente.EfectivoRestringidoHasta is DateTimeOffset fecha && fecha > DateTimeOffset.Now
            ? $"Recojo con efectivo restringido hasta {fecha.ToLocalTime():dd/MM/yyyy HH:mm}."
            : string.Empty;

        if (cliente.Estado != "active")
        {
            EtiquetaEstado.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 222, 218));
            TextoEstado.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(154, 68, 63));
        }
    }

    private void MostrarPreferencias(PreferenciasClienteDetalle? preferencias)
    {
        if (preferencias is null)
        {
            AvisosPedidos.Text = "Sin configurar";
            AvisosPromociones.Text = "Sin configurar";
            ConsentimientoMarketing.Text = "Sin configurar";
            PreferenciaSustitucion.Text = "Contactar al cliente";
            return;
        }

        AvisosPedidos.Text = preferencias.NotificacionesPedido ? "Sí" : "No";
        AvisosPromociones.Text = preferencias.NotificacionesPromociones ? "Sí" : "No";
        ConsentimientoMarketing.Text = preferencias.ConsentimientoMarketing ? "Sí" : "No";
        PreferenciaSustitucion.Text = preferencias.SustitucionPredeterminada switch
        {
            "allow_similar" => "Permitir producto similar",
            "remove" => "Retirar producto",
            _ => "Contactar al cliente"
        };
    }

    private static string CrearIniciales(string nombre)
    {
        var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(partes.Take(2).Select(parte => char.ToUpperInvariant(parte[0])));
    }

    private static string TraducirEstado(string estado) => estado switch
    {
        "active" => "Activo",
        "restricted" => "Restringido",
        "blocked" => "Bloqueado",
        "archived" => "Archivado",
        _ => estado
    };

    private void AlCerrar(object sender, RoutedEventArgs e) => Close();
}
