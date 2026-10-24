using System.Windows;
using System.Net.Http;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio.ViewModels;
using Cogana.Admin.Escritorio.Vistas;
using Cogana.Admin.Escritorio.Servicios;
using Cogana.Admin.Infraestructura.Configuracion;
using Cogana.Admin.Infraestructura.Servicios;
using Velopack;
namespace Cogana.Admin.Escritorio;

public partial class App : Application
{
    private ClienteSupabaseRest? _clienteSupabase;
    private ServicioEstadoBackend? _servicioEstado;
    private ServicioResumenInicioSupabase? _servicioResumenInicio;
    private ServicioModulosAdministrativosSupabase? _servicioModulos;
    private ServicioCatalogoAdministrativoSupabase? _servicioCatalogo;
    private ServicioPedidosAdministrativosSupabase? _servicioPedidos;
    private ServicioInventarioAdministrativoSupabase? _servicioInventario;
    private ServicioPromocionesAdministrativasSupabase? _servicioPromociones;
    private ServicioClientesAdministrativosSupabase? _servicioClientes;
    private ServicioUsuariosAdministrativosSupabase? _servicioUsuarios;
    private ServicioConfiguracionTiendaSupabase? _servicioConfiguracion;
    private ServicioReportesAdministrativosSupabase? _servicioReportes;
    private ServicioActualizacionesVelopack? _servicioActualizaciones;
    private ServicioImagenesProducto? _servicioImagenes;
    private IServicioPanelInicio? _servicioPanelInicio;
    private VentanaInicioSesion? _ventanaInicioSesion;
    private IServicioAutenticacion? _servicioAutenticacion;
    private readonly List<ClienteSupabaseRest> _clientesAnteriores = [];
    private ConfiguracionSupabase? _configuracionSupabase;

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var aplicacion = new App();
        aplicacion.InitializeComponent();
        aplicacion.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MostrarInicioSesion();
    }

    private void MostrarInicioSesion()
    {
        var configuracion = _configuracionSupabase ??= ConfiguracionSupabase.DesdeVariablesDeEntorno();
        _clienteSupabase = new ClienteSupabaseRest(configuracion);
        _servicioEstado = new ServicioEstadoBackend(_clienteSupabase);
        _servicioResumenInicio = new ServicioResumenInicioSupabase(_clienteSupabase);
        _servicioModulos = new ServicioModulosAdministrativosSupabase(_clienteSupabase);
        _servicioCatalogo = new ServicioCatalogoAdministrativoSupabase(_clienteSupabase);
        _servicioPedidos = new ServicioPedidosAdministrativosSupabase(_clienteSupabase);
        _servicioInventario = new ServicioInventarioAdministrativoSupabase(_clienteSupabase);
        _servicioPromociones = new ServicioPromocionesAdministrativasSupabase(_clienteSupabase);
        _servicioClientes = new ServicioClientesAdministrativosSupabase(_clienteSupabase);
        _servicioUsuarios = new ServicioUsuariosAdministrativosSupabase(_clienteSupabase);
        _servicioConfiguracion = new ServicioConfiguracionTiendaSupabase(_clienteSupabase);
        _servicioReportes = new ServicioReportesAdministrativosSupabase(_clienteSupabase);
        _servicioActualizaciones = new ServicioActualizacionesVelopack();
        _servicioImagenes = new ServicioImagenesProducto(_clienteSupabase, configuracion);
        _servicioPanelInicio = new ServicioPanelInicioArchivo();
        _servicioAutenticacion = new ServicioAutenticacionSupabase(_clienteSupabase);
        var viewModel = new InicioSesionViewModel(_servicioAutenticacion);

        var cuentaRecordada = configuracion.Url is null ? null :
            new CuentaRecordadaWindows($"CoganaAdmin:Acceso:{configuracion.Url.GetLeftPart(UriPartial.Authority)}");
        _ventanaInicioSesion = new VentanaInicioSesion(cuentaRecordada)
        {
            DataContext = viewModel
        };
        _ventanaInicioSesion.AccesoConfirmado += AbrirPanelPrincipal;

        MainWindow = _ventanaInicioSesion;
        _ventanaInicioSesion.Show();
    }

    private void AbrirPanelPrincipal(SesionUsuario sesion)
    {
        // La introducción animada saluda una sola vez por equipo antes de abrir el panel.
        // Cualquier cierre (fin, salto o X) marca el flag y abre el panel normalmente.
        var estadoOnboarding = new EstadoPrimerIngreso();
        estadoOnboarding.Cargar();
        if (!estadoOnboarding.Visto)
        {
            var onboarding = new VentanaOnboarding(sesion);
            onboarding.Finalizado += () =>
            {
                estadoOnboarding.MarcarVisto(GetType().Assembly.GetName().Version?.ToString(3) ?? "desconocida");
                AbrirPanelPrincipal(sesion);
            };
            MainWindow = onboarding;
            onboarding.Show();
            if (_ventanaInicioSesion is not null)
            {
                _ventanaInicioSesion.Close();
                _ventanaInicioSesion = null;
            }
            return;
        }

        if (_servicioEstado is null ||
            _servicioResumenInicio is null ||
            _servicioModulos is null ||
            _servicioCatalogo is null ||
            _servicioPedidos is null ||
            _servicioInventario is null ||
            _servicioPromociones is null ||
            _servicioClientes is null ||
            _servicioUsuarios is null ||
            _servicioConfiguracion is null ||
            _servicioReportes is null ||
            _servicioActualizaciones is null)
        {
            return;
        }

        var panel = new MainWindow
        {
            DataContext = new VentanaPrincipalViewModel(
                _servicioEstado,
                _servicioResumenInicio,
                _servicioModulos,
                _servicioReportes,
                _servicioActualizaciones,
                _servicioPanelInicio ?? new ServicioPanelInicioArchivo(),
                sesion),
            ServicioCatalogo = _servicioCatalogo,
            ServicioPedidos = _servicioPedidos,
            ServicioInventario = _servicioInventario,
            ServicioPromociones = _servicioPromociones,
            ServicioClientes = _servicioClientes,
            ServicioUsuarios = _servicioUsuarios,
            ServicioConfiguracion = _servicioConfiguracion,
            ServicioReportes = _servicioReportes,
            ServicioImagenes = _servicioImagenes,
            TiendaId = sesion.TiendaId
        };

        panel.CerrarSesionSolicitado = async () =>
        {
            if (panel.OwnedWindows.Count > 0) return;
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                if (_servicioAutenticacion is not null)
                    await _servicioAutenticacion.CerrarSesionAsync(limite.Token);
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
            {
                // Sin conexión también se elimina el acceso local al panel.
            }
            if (_clienteSupabase is { } anterior)
            {
                anterior.LimpiarTokenAcceso();
                // Deja finalizar las consultas en curso sin reutilizar su cliente en el login.
                _clientesAnteriores.Add(anterior);
            }
            // Cada ingreso usa servicios nuevos; ninguna consulta anterior comparte su sesión.
            MostrarInicioSesion();
            panel.Close();
        };
        MainWindow = panel;
        panel.Show();

        if (_ventanaInicioSesion is not null)
        {
            // La ventana de acceso ya completó la confirmación antes de abrir el panel.
            _ventanaInicioSesion.Close();
            _ventanaInicioSesion = null;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _clienteSupabase?.Dispose();
        foreach (var anterior in _clientesAnteriores) anterior.Dispose();
        base.OnExit(e);
    }
}
