using System.Windows;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio.ViewModels;
using Cogana.Admin.Escritorio.Vistas;
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
    private IServicioPanelInicio? _servicioPanelInicio;
    private VentanaInicioSesion? _ventanaInicioSesion;

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

        var configuracion = ConfiguracionSupabase.DesdeVariablesDeEntorno();
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
        _servicioPanelInicio = new ServicioPanelInicioArchivo();
        var servicioAutenticacion = new ServicioAutenticacionSupabase(_clienteSupabase);
        var viewModel = new InicioSesionViewModel(servicioAutenticacion);
        viewModel.SesionIniciada += AbrirPanelPrincipal;

        _ventanaInicioSesion = new VentanaInicioSesion
        {
            DataContext = viewModel
        };

        MainWindow = _ventanaInicioSesion;
        _ventanaInicioSesion.Show();
    }

    private async void AbrirPanelPrincipal(SesionUsuario sesion)
    {
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
            TiendaId = sesion.TiendaId
        };

        MainWindow = panel;
        panel.Show();

        if (_ventanaInicioSesion is not null)
        {
            // Deja ver el "Bienvenido al sistema" de la pantalla de carga antes de cerrar.
            await Task.Delay(1200);
            _ventanaInicioSesion.Close();
            _ventanaInicioSesion = null;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _clienteSupabase?.Dispose();
        base.OnExit(e);
    }
}
