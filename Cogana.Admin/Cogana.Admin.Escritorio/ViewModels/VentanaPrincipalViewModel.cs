using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.ViewModels;

public sealed class VentanaPrincipalViewModel : BaseViewModel
{
    private readonly IServicioEstadoBackend _servicioEstadoBackend;
    private readonly IServicioResumenInicio _servicioResumenInicio;
    private readonly IServicioModulosAdministrativos _servicioModulos;
    private readonly IServicioReportesAdministrativos _servicioReportes;
    private readonly IServicioActualizaciones _servicioActualizaciones;
    private readonly IServicioPanelInicio _servicioPanelInicio;
    private readonly SesionUsuario _sesion;
    private ModuloAdministrativo? _moduloSeleccionado;
    private string _estadoConexion = "Conexión pendiente";
    private bool _conexionExitosa;
    private bool _estaCargandoInicio;
    private int _productosActivos;
    private int _categoriasActivas;
    private int _lotesPorVencer;
    private int _pedidosPendientes;
    private int _proveedoresActivos;
    private string _nombreTienda = "Tienda Cogana";
    private string _distritoTienda = string.Empty;
    private string _estadoTienda = "Sin consultar";
    private string _detalleEstadoTienda = "Conecta para actualizar";
    private string _resumenActualizado = "Pendiente de actualización";
    private readonly List<FilaModuloDatos> _todasFilasModulo = [];
    private bool _estaCargandoModulo;
    private long _versionCargaModulo;
    private CancellationTokenSource? _cargaModulo;
    private string _textoBusqueda = string.Empty;
    private string _mensajeModulo = "Selecciona Actualizar para consultar Supabase.";
    private string _encabezado1 = "Nombre";
    private string _encabezado2 = "Detalle";
    private string _encabezado3 = "Información";
    private string _encabezado4 = "Estado";
    private string _encabezado5 = "Actualización";
    private string _accionPrincipal = "Actualizar";
    private int _totalModulo;
    private FilaModuloDatos? _filaModuloSeleccionada;
    private InfoActualizacionDisponible? _actualizacionPendiente;
    private string _mensajeActualizacion = string.Empty;
    private string _textoBotonActualizacion = "Buscar actualizaciones";

    public VentanaPrincipalViewModel(
        IServicioEstadoBackend servicioEstadoBackend,
        IServicioResumenInicio servicioResumenInicio,
        IServicioModulosAdministrativos servicioModulos,
        IServicioReportesAdministrativos servicioReportes,
        IServicioActualizaciones servicioActualizaciones,
        IServicioPanelInicio servicioPanelInicio,
        SesionUsuario sesion)
    {
        _servicioEstadoBackend = servicioEstadoBackend;
        _servicioResumenInicio = servicioResumenInicio;
        _servicioModulos = servicioModulos;
        _servicioReportes = servicioReportes;
        _servicioActualizaciones = servicioActualizaciones;
        _servicioPanelInicio = servicioPanelInicio;
        _sesion = sesion;
        Modulos =
        [
            new("Inicio", "Resumen general del negocio", "I"),
            new("Productos", "Catálogo y precios", "P"),
            new("Categorías", "Organización del catálogo", "C"),
            new("Presentaciones", "Granel, unidades, paquetes y sacos", "PR"),
            new("Inventario y lotes", "Stock, movimientos y vencimientos", "L"),
            new("Proveedores", "Abastecimiento de la tienda", "PV"),
            new("Pedidos", "Preparación, recojo y delivery", "PE"),
            new("Clientes", "Datos y direcciones", "CL"),
            new("Promociones", "Descuentos y vigencias", "%"),
            new("Reportes", "Ventas, inventario y pedidos", "R"),
            new("Usuarios", "Accesos del personal", "U"),
            new("Configuración", "Tienda, horarios y operación", "CO")
        ];

        PedidosRecientes = [];
        FilasModulo = [];
        WidgetsInicio = [];
        ModuloSeleccionado = Modulos[0];
        RestaurarWidgetsGuardados();
        VerificarConexionCommand = new ComandoAsincrono(CargarInicioAsync);
        ActualizarModuloCommand = new ComandoAsincrono(CargarModuloActualAsync);
        BuscarActualizacionesCommand = new ComandoAsincrono(GestionarActualizacionAsync);
    }

    public ObservableCollection<ModuloAdministrativo> Modulos { get; }
    public ObservableCollection<PedidoRecienteResumen> PedidosRecientes { get; }
    public ObservableCollection<FilaModuloDatos> FilasModulo { get; }
    public ObservableCollection<WidgetInicioViewModel> WidgetsInicio { get; }
    public ICommand VerificarConexionCommand { get; }
    public ICommand ActualizarModuloCommand { get; }
    public ICommand BuscarActualizacionesCommand { get; }
    public string FechaActual => DateTime.Now.ToString(
        "dddd, d 'de' MMMM",
        new CultureInfo("es-PE"));

    public void ActualizarFechaActual() => Notificar(nameof(FechaActual));

    public string CorreoUsuario => _sesion.Correo;
    public string RolUsuario => _sesion.Rol == "owner" ? "Propietario" : "Administrador";
    public string InicialesUsuario => string.IsNullOrWhiteSpace(_sesion.Correo)
        ? "CA"
        : _sesion.Correo[..Math.Min(2, _sesion.Correo.Length)].ToUpperInvariant();
    public string VersionActual => _servicioActualizaciones.VersionActual;
    public bool TieneWidgetsInicio => WidgetsInicio.Count > 0;
    /// <summary>Añade un módulo como sección del panel desde el menú izquierdo.</summary>
    public void AgregarWidgetPorNombre(string nombreModulo)
    {
        if (string.IsNullOrWhiteSpace(nombreModulo) || nombreModulo == "Inicio")
        {
            return;
        }

        if (WidgetsInicio.Any(widget => widget.Nombre == nombreModulo))
        {
            return;
        }

        var modulo = Modulos.FirstOrDefault(m => m.Nombre == nombreModulo);
        if (modulo is null)
        {
            return;
        }

        var widget = new WidgetInicioViewModel(modulo.Nombre, modulo.Inicial);
        widget.QuitarSolicitado += QuitarWidget;
        WidgetsInicio.Add(widget);

        Notificar(nameof(TieneWidgetsInicio));
        GuardarWidgets();

        _ = CargarDatosWidgetAsync(widget);
    }

    public void QuitarWidget(WidgetInicioViewModel widget)
    {
        if (WidgetsInicio.Remove(widget))
        {
            widget.QuitarSolicitado -= QuitarWidget;
            Notificar(nameof(TieneWidgetsInicio));
            GuardarWidgets();
        }
    }

    /// <summary>
    /// Reordena una tarjeta del panel: se inserta antes o después de la tarjeta destino.
    /// Si no hay destino, la mueve al final.
    /// </summary>
    public void MoverWidget(WidgetInicioViewModel widget, WidgetInicioViewModel? destino, bool insertarDespues)
    {
        if (widget is null)
        {
            return;
        }

        if (destino is null)
        {
            MoverWidgetAlFinal(widget);
            return;
        }

        var indiceActual = WidgetsInicio.IndexOf(widget);
        var indiceDestino = WidgetsInicio.IndexOf(destino);
        if (indiceActual < 0 || indiceDestino < 0 || indiceActual == indiceDestino)
        {
            return;
        }

        int nuevoIndice;
        if (insertarDespues)
        {
            nuevoIndice = indiceActual < indiceDestino ? indiceDestino : indiceDestino + 1;
        }
        else
        {
            nuevoIndice = indiceActual < indiceDestino ? indiceDestino - 1 : indiceDestino;
        }

        WidgetsInicio.Move(indiceActual, nuevoIndice);
        GuardarWidgets();
    }

    public void MoverWidgetAlFinal(WidgetInicioViewModel widget)
    {
        var indice = WidgetsInicio.IndexOf(widget);
        if (indice < 0 || indice == WidgetsInicio.Count - 1)
        {
            return;
        }

        WidgetsInicio.Move(indice, WidgetsInicio.Count - 1);
        GuardarWidgets();
    }

    private void RestaurarWidgetsGuardados()
    {
        foreach (var nombre in _servicioPanelInicio.Cargar())
        {
            var modulo = Modulos.FirstOrDefault(m => m.Nombre == nombre);
            if (modulo is not null)
            {
                var widget = new WidgetInicioViewModel(modulo.Nombre, modulo.Inicial);
                widget.QuitarSolicitado += QuitarWidget;
                WidgetsInicio.Add(widget);
            }
        }

        if (WidgetsInicio.Count == 0 && !_servicioPanelInicio.TieneConfiguracionGuardada)
        {
            // Primera ejecución: panel sugerido. El usuario puede quitarlos o añadir otros.
            foreach (var nombre in (string[])["Pedidos", "Productos", "Inventario y lotes"])
            {
                var modulo = Modulos.First(m => m.Nombre == nombre);
                var widget = new WidgetInicioViewModel(modulo.Nombre, modulo.Inicial);
                widget.QuitarSolicitado += QuitarWidget;
                WidgetsInicio.Add(widget);
            }
        }
    }

    private void GuardarWidgets() =>
        _servicioPanelInicio.Guardar([.. WidgetsInicio.Select(widget => widget.Nombre)]);

    public void RestaurarPanelInicial()
    {
        foreach (var nombre in new[] { "Pedidos", "Productos", "Inventario y lotes" })
            AgregarWidgetPorNombre(nombre);
    }

    private async Task CargarDatosWidgetAsync(WidgetInicioViewModel widget)
    {
        widget.IndicarCarga();

        try
        {
            if (widget.EsReporte)
            {
                var fechaFin = DateOnly.FromDateTime(DateTime.Today);
                var reporte = await _servicioReportes.ObtenerAsync(
                    _sesion.TiendaId,
                    fechaFin.AddDays(-29),
                    fechaFin);
                widget.AplicarReporte(reporte);
                return;
            }

            var resultado = await _servicioModulos.ObtenerAsync(widget.Nombre, _sesion.TiendaId);

            if (resultado.EsExitoso && resultado.Datos is not null)
            {
                widget.AplicarDatos(resultado.Datos);
            }
            else
            {
                widget.IndicarFallo(resultado.Mensaje);
            }
        }
        catch (Exception)
        {
            widget.IndicarFallo("No se pudo consultar el módulo en Supabase.");
        }
    }

    private async Task RefrescarWidgetsAsync()
    {
        if (WidgetsInicio.Count == 0)
        {
            return;
        }

        await Task.WhenAll(WidgetsInicio.Select(CargarDatosWidgetAsync));
    }

    public ModuloAdministrativo? ModuloSeleccionado
    {
        get => _moduloSeleccionado;
        set
        {
            if (_moduloSeleccionado == value)
            {
                return;
            }
            _moduloSeleccionado = value;
            _versionCargaModulo++;
            _cargaModulo?.Cancel();
            EstaCargandoModulo = false;
            _todasFilasModulo.Clear(); FilasModulo.Clear(); FilaModuloSeleccionada = null; TotalModulo = 0;
            Notificar(nameof(TieneFilasModulo));
            Notificar(nameof(EsModuloInicio));
            Notificar(nameof(EsModuloPendiente));
            Notificar(nameof(PuedeCrearRegistro));
            Notificar(nameof(PuedeEditarRegistro));
            Notificar(nameof(EsModuloProductos));
            Notificar(nameof(PuedeVerImagen));
            Notificar(nameof(TextoEditarRegistro));
            Notificar(nameof(PuedeGestionarPedido));
            Notificar(nameof(PuedeCambiarEstadoRegistro));
            Notificar(nameof(TextoAccionEstado));
            Notificar(nameof(PuedeExportarReporte));
            TextoBusqueda = string.Empty;
            MensajeModulo = EsModuloInicio
                ? string.Empty
                : "Cargando información del módulo...";
            // La selección visual puede iniciar la consulta de inmediato: notificar después de limpiar la anterior.
            Notificar(nameof(ModuloSeleccionado));
        }
    }

    public bool EsModuloInicio => ModuloSeleccionado?.Nombre == "Inicio";
    public bool EsModuloPendiente => !EsModuloInicio;
    public bool PuedeCrearRegistro =>
        ModuloSeleccionado?.Nombre is
            "Productos" or "Categorías" or "Presentaciones" or "Inventario y lotes" or "Proveedores" or "Promociones" ||
        (ModuloSeleccionado?.Nombre == "Usuarios" && _sesion.Rol == "owner");

    public string EstadoConexion
    {
        get => _estadoConexion;
        private set => Establecer(ref _estadoConexion, value);
    }

    public bool ConexionExitosa
    {
        get => _conexionExitosa;
        private set => Establecer(ref _conexionExitosa, value);
    }

    public bool EstaCargandoInicio
    {
        get => _estaCargandoInicio;
        private set => Establecer(ref _estaCargandoInicio, value);
    }

    public int ProductosActivos
    {
        get => _productosActivos;
        private set => Establecer(ref _productosActivos, value);
    }

    public int CategoriasActivas
    {
        get => _categoriasActivas;
        private set => Establecer(ref _categoriasActivas, value);
    }

    public int LotesPorVencer
    {
        get => _lotesPorVencer;
        private set => Establecer(ref _lotesPorVencer, value);
    }

    public int PedidosPendientes
    {
        get => _pedidosPendientes;
        private set => Establecer(ref _pedidosPendientes, value);
    }

    public int ProveedoresActivos
    {
        get => _proveedoresActivos;
        private set => Establecer(ref _proveedoresActivos, value);
    }

    public string NombreTienda
    {
        get => _nombreTienda;
        private set => Establecer(ref _nombreTienda, value);
    }

    public string DistritoTienda
    {
        get => _distritoTienda;
        private set => Establecer(ref _distritoTienda, value);
    }

    public string EstadoTienda
    {
        get => _estadoTienda;
        private set => Establecer(ref _estadoTienda, value);
    }

    public string DetalleEstadoTienda
    {
        get => _detalleEstadoTienda;
        private set => Establecer(ref _detalleEstadoTienda, value);
    }

    public string ResumenActualizado
    {
        get => _resumenActualizado;
        private set => Establecer(ref _resumenActualizado, value);
    }

    public bool TienePedidosRecientes => PedidosRecientes.Count > 0;
    public bool TieneFilasModulo => FilasModulo.Count > 0;
    public bool PuedeExportarReporte =>
        false;

    public bool EstaCargandoModulo
    {
        get => _estaCargandoModulo;
        private set => Establecer(ref _estaCargandoModulo, value);
    }

    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set
        {
            if (Establecer(ref _textoBusqueda, value))
            {
                AplicarFiltroModulo();
            }
        }
    }

    public string MensajeModulo
    {
        get => _mensajeModulo;
        private set => Establecer(ref _mensajeModulo, value);
    }

    public string Encabezado1
    {
        get => _encabezado1;
        private set => Establecer(ref _encabezado1, value);
    }

    public string Encabezado2
    {
        get => _encabezado2;
        private set => Establecer(ref _encabezado2, value);
    }

    public string Encabezado3
    {
        get => _encabezado3;
        private set => Establecer(ref _encabezado3, value);
    }

    public string Encabezado4
    {
        get => _encabezado4;
        private set => Establecer(ref _encabezado4, value);
    }

    public string Encabezado5
    {
        get => _encabezado5;
        private set => Establecer(ref _encabezado5, value);
    }

    public string AccionPrincipal
    {
        get => _accionPrincipal;
        private set => Establecer(ref _accionPrincipal, value);
    }

    public int TotalModulo
    {
        get => _totalModulo;
        private set => Establecer(ref _totalModulo, value);
    }

    public FilaModuloDatos? FilaModuloSeleccionada
    {
        get => _filaModuloSeleccionada;
        set
        {
            if (Establecer(ref _filaModuloSeleccionada, value))
            {
                Notificar(nameof(PuedeGestionarPedido));
                Notificar(nameof(PuedeEditarRegistro));
                Notificar(nameof(PuedeVerImagen));
                Notificar(nameof(TextoEditarRegistro));
                Notificar(nameof(PuedeCambiarEstadoRegistro));
                Notificar(nameof(TextoAccionEstado));
            }
        }
    }

    public bool EsModuloProductos => ModuloSeleccionado?.Nombre == "Productos";
    public bool PuedeVerImagen => EsModuloProductos && FilaModuloSeleccionada is not null;

    public bool PuedeGestionarPedido =>
        ModuloSeleccionado?.Nombre == "Pedidos" &&
        FilaModuloSeleccionada is { CodigoEstado.Length: > 0 };

    public bool PuedeEditarRegistro =>
        ModuloSeleccionado?.Nombre is "Reportes" or "Configuración" ||
        (FilaModuloSeleccionada is not null &&
         ModuloSeleccionado?.Nombre is
            "Productos" or "Categorías" or "Presentaciones" or "Inventario y lotes" or "Proveedores" or "Clientes" or "Promociones") ||
        (FilaModuloSeleccionada is not null &&
         ModuloSeleccionado?.Nombre == "Usuarios" &&
         _sesion.Rol == "owner");

    public string TextoEditarRegistro => ModuloSeleccionado?.Nombre switch
    {
        "Inventario y lotes" => "Ver lote",
        "Clientes" => "Ver cliente",
        "Usuarios" => "Gestionar acceso",
        "Reportes" => "Abrir reportes",
        "Configuración" => "Editar configuración",
        _ => "Editar"
    };

    public bool PuedeCambiarEstadoRegistro =>
        FilaModuloSeleccionada is not null &&
        ModuloSeleccionado?.Nombre is
            "Productos" or "Categorías" or "Presentaciones" or "Proveedores" or "Promociones";

    public bool RegistroSeleccionadoEstaActivo => FilaModuloSeleccionada?.Estado is
        "Activo" or "Activa" or "Habilitado" or "Visible";

    public string TextoAccionEstado => RegistroSeleccionadoEstaActivo ? "Desactivar" : "Activar";

    public string MensajeActualizacion
    {
        get => _mensajeActualizacion;
        private set => Establecer(ref _mensajeActualizacion, value);
    }

    public string TextoBotonActualizacion
    {
        get => _textoBotonActualizacion;
        private set => Establecer(ref _textoBotonActualizacion, value);
    }

    public async Task GestionarActualizacionAsync()
    {
        if (!_servicioActualizaciones.EstaInstaladoComoPaquete)
        {
            MensajeActualizacion = "Estás ejecutando la app en desarrollo: las actualizaciones "
                + "solo se verifican desde la versión instalada.";
            return;
        }

        if (_actualizacionPendiente is null)
        {
            MensajeActualizacion = "Buscando actualizaciones...";

            var disponible = await _servicioActualizaciones.BuscarNuevaVersionAsync();

            if (disponible is null)
            {
                MensajeActualizacion = $"Ya tienes la versión más reciente (v{VersionActual}).";
                return;
            }

            _actualizacionPendiente = disponible;
            TextoBotonActualizacion = $"Instalar v{disponible.VersionNueva}";
            MensajeActualizacion = $"Versión {disponible.VersionNueva} disponible. "
                + "Pulsa de nuevo para instalar y reiniciar.";
            return;
        }

        var pendiente = _actualizacionPendiente;
        TextoBotonActualizacion = "Descargando...";
        MensajeActualizacion = $"Descargando versión {pendiente.VersionNueva}...";

        var progreso = new Progress<int>(porcentaje =>
            MensajeActualizacion = $"Descargando versión {pendiente.VersionNueva}... {porcentaje}%");

        await _servicioActualizaciones.DescargarAsync(pendiente, progreso);

        MensajeActualizacion = "Instalando la nueva versión. La aplicación se reiniciará...";
        _servicioActualizaciones.InstalarYReiniciar(pendiente);
    }

    public async Task CargarInicioAsync()
    {
        if (EstaCargandoInicio)
        {
            return;
        }

        EstaCargandoInicio = true;
        EstadoConexion = "Actualizando información...";

        try
        {
            var conexion = await _servicioEstadoBackend.ProbarConexionAsync();
            ConexionExitosa = conexion.EsExitosa;
            EstadoConexion = conexion.Mensaje;

            if (!conexion.EsExitosa)
            {
                foreach (var widget in WidgetsInicio) widget.IndicarFallo(conexion.Mensaje);
                return;
            }

            var resultado = await _servicioResumenInicio.ObtenerAsync(_sesion.TiendaId);
            if (!resultado.EsExitoso || resultado.Resumen is null)
            {
                EstadoConexion = resultado.Mensaje;
                ConexionExitosa = false;
                foreach (var widget in WidgetsInicio) widget.IndicarFallo(resultado.Mensaje);
                return;
            }

            AplicarResumen(resultado.Resumen);
            EstadoConexion = "Conexión activa con Cogana/Supabase.";
            await RefrescarWidgetsAsync();
        }
        catch (Exception)
        {
            ConexionExitosa = false;
            EstadoConexion = "No se pudo actualizar el resumen. Revisa la conexión e intenta nuevamente.";
            foreach (var widget in WidgetsInicio) widget.IndicarFallo(EstadoConexion);
        }
        finally
        {
            EstaCargandoInicio = false;
        }
    }

    public async Task CargarModuloActualAsync()
    {
        if (EstaCargandoModulo || EsModuloInicio || ModuloSeleccionado is null) return;
        var modulo = ModuloSeleccionado.Nombre;
        var version = ++_versionCargaModulo;
        using var cancelacion = new CancellationTokenSource();
        _cargaModulo = cancelacion;
        EstaCargandoModulo = true;
        FilaModuloSeleccionada = null;
        MensajeModulo = $"Consultando {modulo.ToLowerInvariant()}...";
        try
        {
            var resultado = await _servicioModulos.ObtenerAsync(modulo, _sesion.TiendaId, cancelacion.Token);
            // Una respuesta anterior no puede terminar el skeleton ni reemplazar el módulo actual.
            if (version != _versionCargaModulo) return;
            if (!resultado.EsExitoso || resultado.Datos is null)
            {
                _todasFilasModulo.Clear(); FilasModulo.Clear(); TotalModulo = 0;
                MensajeModulo = resultado.Mensaje; Notificar(nameof(TieneFilasModulo)); return;
            }
            AplicarModulo(resultado.Datos);
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested) { }
        catch (Exception)
        {
            if (version == _versionCargaModulo)
            {
                _todasFilasModulo.Clear(); FilasModulo.Clear(); TotalModulo = 0;
                MensajeModulo = "No se pudo consultar el módulo. Revisa la conexión e intenta actualizar.";
                Notificar(nameof(TieneFilasModulo));
            }
        }
        finally
        {
            if (version == _versionCargaModulo) EstaCargandoModulo = false;
            if (ReferenceEquals(_cargaModulo, cancelacion)) _cargaModulo = null;
        }
    }

    private void AplicarModulo(ModuloDatos datos)
    {
        Encabezado1 = datos.Encabezado1;
        Encabezado2 = datos.Encabezado2;
        Encabezado3 = datos.Encabezado3;
        Encabezado4 = datos.Encabezado4;
        Encabezado5 = datos.Encabezado5;
        AccionPrincipal = datos.AccionPrincipal;

        _todasFilasModulo.Clear();
        _todasFilasModulo.AddRange(datos.Filas);
        FilaModuloSeleccionada = null;
        TotalModulo = _todasFilasModulo.Count;
        MensajeModulo = TotalModulo == 0
            ? "No hay registros para mostrar en este módulo."
            : $"{TotalModulo:N0} registros obtenidos desde Supabase.";
        AplicarFiltroModulo();
    }

    private void AplicarFiltroModulo()
    {
        if (FilasModulo is null)
        {
            return;
        }

        var termino = TextoBusqueda.Trim();
        var filas = string.IsNullOrWhiteSpace(termino)
            ? _todasFilasModulo
            : _todasFilasModulo
                .Where(fila => fila.TextoBusqueda.Contains(
                    termino,
                    StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        FilasModulo.Clear();
        foreach (var fila in filas)
        {
            FilasModulo.Add(fila);
        }

        if (filas.Count == 0 && !string.IsNullOrWhiteSpace(termino) && TotalModulo > 0)
        {
            MensajeModulo = $"Sin coincidencias para «{termino}» entre {TotalModulo:N0} registros.";
        }

        Notificar(nameof(TieneFilasModulo));
        Notificar(nameof(PuedeExportarReporte));
    }

    private void AplicarResumen(ResumenInicio resumen)
    {
        NombreTienda = resumen.NombreTienda;
        DistritoTienda = resumen.Distrito;
        ProductosActivos = resumen.ProductosActivos;
        CategoriasActivas = resumen.CategoriasActivas;
        LotesPorVencer = resumen.LotesPorVencer;
        PedidosPendientes = resumen.PedidosPendientes;
        ProveedoresActivos = resumen.ProveedoresActivos;
        EstadoTienda = TraducirEstadoTienda(resumen.EstadoTienda);
        DetalleEstadoTienda = string.IsNullOrWhiteSpace(resumen.MensajeTienda)
            ? "Estado operativo registrado en Supabase"
            : resumen.MensajeTienda;
        ResumenActualizado = $"Actualizado {resumen.ActualizadoEn:HH:mm}";

        PedidosRecientes.Clear();
        foreach (var pedido in resumen.PedidosRecientes)
        {
            PedidosRecientes.Add(pedido);
        }

        Notificar(nameof(TienePedidosRecientes));
    }

    private static string TraducirEstadoTienda(string estado) => estado switch
    {
        "open" => "Abierta",
        "closed" => "Cerrada",
        "paused" => "Pausada",
        "busy" => "Alta demanda",
        _ => "Sin estado"
    };
}
