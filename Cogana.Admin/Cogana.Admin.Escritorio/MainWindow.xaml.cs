using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Cogana.Admin.Escritorio.Controles;
using System.Text;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio.ViewModels;
using Microsoft.Win32;

namespace Cogana.Admin.Escritorio;

public partial class MainWindow : Window
{
    public static readonly DependencyProperty EsMenuCompactoProperty = DependencyProperty.Register(
        nameof(EsMenuCompacto), typeof(bool), typeof(MainWindow),
        new PropertyMetadata(false, (objeto, _) => ((MainWindow)objeto).ActualizarMenu()));
    public bool EsMenuCompacto
    {
        get => (bool)GetValue(EsMenuCompactoProperty);
        set => SetValue(EsMenuCompactoProperty, value);
    }
    private void AlAlternarMenu(object sender, RoutedEventArgs e) => EsMenuCompacto = !EsMenuCompacto;
    public static readonly DependencyProperty AnchoMenuProperty = DependencyProperty.Register(
        nameof(AnchoMenu), typeof(double), typeof(MainWindow), new PropertyMetadata(252d,
            (objeto, cambio) => { var ventana = (MainWindow)objeto;
                if (ventana.ColumnaMenu is not null) ventana.ColumnaMenu.Width = new GridLength((double)cambio.NewValue); }));
    public double AnchoMenu { get => (double)GetValue(AnchoMenuProperty); set => SetValue(AnchoMenuProperty, value); }
    private void ActualizarMenu()
    {
        if (ColumnaMenu is null) return;
        var origen = AnchoMenu;
        var destino = EsMenuCompacto ? 104d : 252d;
        BeginAnimation(AnchoMenuProperty, null);
        if (IsLoaded && SystemParameters.ClientAreaAnimation)
        {
            AnchoMenu = origen;
            // transition: width .25s ease del ZIP. Sustituye el reloj desde el ancho visible.
            var transicion = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(250), FillBehavior = FillBehavior.Stop };
            transicion.KeyFrames.Add(new DiscreteDoubleKeyFrame(origen, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            transicion.KeyFrames.Add(new SplineDoubleKeyFrame(destino, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250)), new KeySpline(.25, .1, .25, 1)));
            transicion.Completed += (_, _) => { AnchoMenu = destino; BeginAnimation(AnchoMenuProperty, null); };
            BeginAnimation(AnchoMenuProperty, transicion, HandoffBehavior.SnapshotAndReplace);
        }
        else AnchoMenu = destino;
        ContenedorMenu.Padding = new Thickness(EsMenuCompacto ? 8 : 12, 16, EsMenuCompacto ? 8 : 12, 16);
        BotonAlternarMenu.Content = EsMenuCompacto ? "›" : "‹  Contraer menú";
        BotonCerrarSesion.Content = EsMenuCompacto ? "⇥" : "Cerrar sesión";
    }
    private void AnimarSeleccion(ListBoxItem opcion, Point? origen = null)
    {
        UpdateLayout();
        var posicion = opcion.TranslatePoint(new Point(), EfectoSeleccion);
        var punto = origen ?? new Point(opcion.ActualWidth / 2, opcion.ActualHeight / 2);
        EfectoSeleccion.ZonaOpcion = new Rect(posicion, new Size(opcion.ActualWidth, opcion.ActualHeight));
        EfectoSeleccion.Iniciar(posicion + (Vector)punto, SystemParameters.ClientAreaAnimation);
    }

    private void AlRestaurarPanel(object sender, RoutedEventArgs e)
    {
        if (DataContext is VentanaPrincipalViewModel viewModel) viewModel.RestaurarPanelInicial();
    }
    public IServicioCatalogoAdministrativo? ServicioCatalogo { get; init; }
    public IServicioPedidosAdministrativos? ServicioPedidos { get; init; }
    public IServicioInventarioAdministrativo? ServicioInventario { get; init; }
    public IServicioPromocionesAdministrativas? ServicioPromociones { get; init; }
    public IServicioClientesAdministrativos? ServicioClientes { get; init; }
    public IServicioUsuariosAdministrativos? ServicioUsuarios { get; init; }
    public IServicioConfiguracionTienda? ServicioConfiguracion { get; init; }
    public IServicioReportesAdministrativos? ServicioReportes { get; init; }
    public IServicioImagenesProducto? ServicioImagenes { get; init; }
    public Guid TiendaId { get; init; }

    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => BeginAnimation(AnchoMenuProperty, null);
        MenuModulos.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => EfectoSeleccion.Detener()));
        SizeChanged += (_, _) => EfectoSeleccion.Detener();
        Loaded += AlCargarVentana;
        Activated += AlActivarVentana;
    }

    private void AlActivarVentana(object? sender, EventArgs e)
    {
        if (DataContext is VentanaPrincipalViewModel viewModel)
        {
            viewModel.ActualizarFechaActual();
        }
    }

    private async void AlCargarVentana(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargarVentana;

        if (DataContext is VentanaPrincipalViewModel viewModel)
        {
            await viewModel.CargarInicioAsync();
        }
    }

    private void AlAbrirModulo(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string nombreModulo } ||
            DataContext is not VentanaPrincipalViewModel viewModel)
        {
            return;
        }

        viewModel.ModuloSeleccionado = viewModel.Modulos.FirstOrDefault(
            modulo => modulo.Nombre == nombreModulo);
    }

    private async void AlCambiarModulo(object sender, SelectionChangedEventArgs e)
    {
        EfectoSeleccion?.Detener();
        if (_opcionPresionada is null && MenuModulos.IsKeyboardFocusWithin && MenuModulos.SelectedItem is not null &&
            MenuModulos.ItemContainerGenerator.ContainerFromItem(MenuModulos.SelectedItem) is ListBoxItem opcion)
            AnimarSeleccion(opcion);
        if (DataContext is not VentanaPrincipalViewModel viewModel)
        {
            return;
        }

        viewModel.ActualizarFechaActual();
        if (viewModel.EsModuloPendiente)
        {
            await viewModel.CargarModuloActualAsync();
        }
    }

    private void AlVerImagenProducto(object sender, RoutedEventArgs e)
    {
        if (ServicioImagenes is null || DataContext is not VentanaPrincipalViewModel { PuedeVerImagen: true, FilaModuloSeleccionada: not null } vm) return;
        new Vistas.VentanaImagenProducto(ServicioImagenes, TiendaId, vm.FilaModuloSeleccionada.Id,
            vm.FilaModuloSeleccionada.Columna2) { Owner=this }.ShowDialog();
    }

    private async void AlCrearRegistro(object sender, RoutedEventArgs e)
    {
        if (DataContext is not VentanaPrincipalViewModel { ModuloSeleccionado: not null } viewModel ||
            !viewModel.PuedeCrearRegistro)
        {
            return;
        }

        Window? ventana = viewModel.ModuloSeleccionado.Nombre == "Usuarios"
            ? ServicioUsuarios is null
                ? null
                : new Vistas.VentanaUsuarioAcceso(ServicioUsuarios, TiendaId)
            : ServicioCatalogo is null
                ? null
                : new Vistas.VentanaNuevoRegistro(
                    ServicioCatalogo,
                    TiendaId,
                    viewModel.ModuloSeleccionado.Nombre, ServicioImagenes);

        if (ventana is null)
        {
            return;
        }

        ventana.Owner = this;

        if (ventana.ShowDialog() == true)
        {
            await viewModel.CargarModuloActualAsync();
        }
    }

    private async void AlGestionarPedido(object sender, RoutedEventArgs e)
    {
        if (ServicioPedidos is null ||
            DataContext is not VentanaPrincipalViewModel
            {
                FilaModuloSeleccionada: not null
            } viewModel)
        {
            return;
        }

        var ventana = new Vistas.VentanaDetallePedido(
            ServicioPedidos,
            TiendaId,
            viewModel.FilaModuloSeleccionada.Id)
        {
            Owner = this
        };

        if (ventana.ShowDialog() == true)
        {
            await viewModel.CargarModuloActualAsync();
            await viewModel.CargarInicioAsync();
        }
    }

    private void AlDobleClicFila(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not VentanaPrincipalViewModel viewModel)
        {
            return;
        }

        if (viewModel.PuedeGestionarPedido)
        {
            AlGestionarPedido(sender, e);
        }
        else if (viewModel.PuedeEditarRegistro)
        {
            AlEditarRegistro(sender, e);
        }
    }

    private const string FormatoArrastreModulo = "modulo-cogana";
    private Point? _inicioArrastreModulo;
    private ListBoxItem? _opcionPresionada;

    public Func<Task>? CerrarSesionSolicitado { get; set; }
    private bool _cerrandoSesion;
    private async void AlCerrarSesion(object sender, RoutedEventArgs e)
    {
        if (_cerrandoSesion || CerrarSesionSolicitado is null) return;
        _cerrandoSesion = true;
        IsEnabled = false;
        BotonCerrarSesion.Content = "Cerrando sesión…";
        try { await CerrarSesionSolicitado(); }
        finally
        {
            _cerrandoSesion = false;
            IsEnabled = true;
            BotonCerrarSesion.Content = EsMenuCompacto ? "⇥" : "Cerrar sesión";
        }
    }

    private void AlSoltarOpcionMenu(object sender, MouseButtonEventArgs e)
    {
        if (_opcionPresionada is not { } opcion) return;
        _opcionPresionada = null;
        _inicioArrastreModulo = null;
        opcion.ReleaseMouseCapture();
        var posicion = e.GetPosition(opcion);
        if (posicion.X >= 0 && posicion.Y >= 0 && posicion.X <= opcion.ActualWidth && posicion.Y <= opcion.ActualHeight)
        {
            opcion.IsSelected = true;
            opcion.Focus();
            AnimarSeleccion(opcion, posicion);
        }
        e.Handled = true;
    }

    private void AlPerderCapturaMenu(object sender, MouseEventArgs e)
    {
        _opcionPresionada = null;
        _inicioArrastreModulo = null;
    }

    private void AlPresionarOpcionPanel(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _inicioArrastreModulo = e.GetPosition(this);
            _opcionPresionada = sender as ListBoxItem;
            _opcionPresionada?.CaptureMouse();
            e.Handled = true;
        }
    }

    private void AlMoverOpcionPanel(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _inicioArrastreModulo is not Point inicio ||
            sender is not ListBoxItem { DataContext: ModuloAdministrativo modulo } opcion)
        {
            _inicioArrastreModulo = null;
            return;
        }

        var posicion = e.GetPosition(this);
        if (Math.Abs(posicion.X - inicio.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(posicion.Y - inicio.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (modulo.Nombre == "Inicio") return;
        _inicioArrastreModulo = null;
        _opcionPresionada = null;
        opcion.ReleaseMouseCapture();
        EfectoSeleccion.Detener();
        if (DataContext is not VentanaPrincipalViewModel viewModel) return;
        var anterior = viewModel.ModuloSeleccionado;
        viewModel.ModuloSeleccionado = viewModel.Modulos[0];
        UpdateLayout();

        try
        {
            var resultado = DragDrop.DoDragDrop(
                opcion,
                new DataObject(FormatoArrastreModulo, modulo.Nombre),
                DragDropEffects.Copy);
            if (resultado == DragDropEffects.None) viewModel.ModuloSeleccionado = anterior;
        }
        finally
        {
            OcultarDestinoModulo();
        }
    }

    private void AlArrastrarModuloSobrePanel(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(FormatoArrastreModulo))
        {
            return;
        }

        e.Effects = DragDropEffects.Copy;
        ZonaDestinoWidgets.Background = new SolidColorBrush(Color.FromArgb(12, 0, 0, 0));
        ZonaDestinoWidgets.BorderBrush = (Brush)FindResource("BrochaNegro");
        AvisoDestinoWidgets.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    private void AlSalirArrastreModulo(object sender, DragEventArgs e)
    {
        var posicion = e.GetPosition(ZonaDestinoWidgets);
        if (posicion.X <= 0 || posicion.Y <= 0 ||
            posicion.X >= ZonaDestinoWidgets.ActualWidth ||
            posicion.Y >= ZonaDestinoWidgets.ActualHeight)
        {
            OcultarDestinoModulo();
        }
    }

    private void AlSoltarModuloEnPanel(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(FormatoArrastreModulo) ||
            DataContext is not VentanaPrincipalViewModel viewModel)
        {
            return;
        }

        if (e.Data.GetData(FormatoArrastreModulo) is string nombreModulo)
        {
            viewModel.AgregarWidgetPorNombre(nombreModulo);
        }

        OcultarDestinoModulo();
        e.Handled = true;
    }

    private void OcultarDestinoModulo()
    {
        ZonaDestinoWidgets.ClearValue(Border.BackgroundProperty);
        ZonaDestinoWidgets.ClearValue(Border.BorderBrushProperty);
        AvisoDestinoWidgets.Visibility = Visibility.Collapsed;
    }

    private void AlQuitarWidget(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WidgetInicioViewModel widget } &&
            DataContext is VentanaPrincipalViewModel viewModel)
        {
            viewModel.QuitarWidget(widget);
        }
    }

    private const string FormatoArrastreTarjeta = "widget-panel";
    private Border? _tarjetaResaltada;
    private WidgetInicioViewModel? _tarjetaDestino;
    private bool _insertarDespuesDestino;
    private Point _posicionArrastreTarjeta;

    private void AlMoverTarjetaParaOrdenar(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not FrameworkElement cabecera ||
            cabecera.DataContext is not WidgetInicioViewModel widget)
        {
            return;
        }

        var posicion = e.GetPosition(cabecera);
        if (Math.Abs(posicion.X - _posicionArrastreTarjeta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(posicion.Y - _posicionArrastreTarjeta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _posicionArrastreTarjeta = posicion;

        try
        {
            DragDrop.DoDragDrop(
                cabecera,
                new DataObject(FormatoArrastreTarjeta, widget),
                DragDropEffects.Move);
        }
        finally
        {
            LimpiarResaltadoTarjeta();
        }
    }

    private void AlArrastrarTarjetaSobreTablero(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(FormatoArrastreTarjeta))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;

        _tarjetaDestino = null;
        if (EncontrarTarjeta(e.OriginalSource as DependencyObject) is { } tarjeta &&
            tarjeta.DataContext is WidgetInicioViewModel destino)
        {
            _tarjetaDestino = destino;
            var posicionEnTarjeta = e.GetPosition(tarjeta);
            _insertarDespuesDestino = posicionEnTarjeta.X > tarjeta.ActualWidth / 2;
            ResaltarTarjeta(tarjeta);
        }
        else
        {
            LimpiarResaltadoTarjeta();
        }

        e.Handled = true;
    }

    private void AlSalirArrastreTarjeta(object sender, DragEventArgs e) => LimpiarResaltadoTarjeta();

    private void AlSoltarTarjetaEnTablero(object sender, DragEventArgs e)
    {
        LimpiarResaltadoTarjeta();

        if (DataContext is VentanaPrincipalViewModel viewModel &&
            e.Data.GetData(FormatoArrastreTarjeta) is WidgetInicioViewModel widget)
        {
            viewModel.MoverWidget(widget, _tarjetaDestino, _insertarDespuesDestino);
        }

        _tarjetaDestino = null;
        e.Handled = true;
    }

    private static Border? EncontrarTarjeta(DependencyObject? origen)
    {
        var actual = origen;
        while (actual is not null)
        {
            if (actual is Border borde && borde.Parent is WrapPanel)
            {
                return borde;
            }

            actual = actual is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(actual)
                : null;
        }

        return null;
    }

    private void ResaltarTarjeta(Border tarjeta)
    {
        if (_tarjetaResaltada == tarjeta)
        {
            return;
        }

        LimpiarResaltadoTarjeta();
        _tarjetaResaltada = tarjeta;
        tarjeta.BorderBrush = (Brush)FindResource("BrochaAcento");
        tarjeta.BorderThickness = new Thickness(2);
    }

    private void LimpiarResaltadoTarjeta()
    {
        if (_tarjetaResaltada is not null)
        {
            _tarjetaResaltada.ClearValue(Border.BorderBrushProperty);
            _tarjetaResaltada.ClearValue(Border.BorderThicknessProperty);
            _tarjetaResaltada = null;
        }
    }

    private static T? BuscarAncestro<T>(DependencyObject origen) where T : DependencyObject
    {
        var actual = origen;
        while (actual is not null && actual is not T)
        {
            actual = System.Windows.Media.VisualTreeHelper.GetParent(actual);
        }

        return actual as T;
    }

    private async void AlEditarRegistro(object sender, RoutedEventArgs e)
    {
        if (DataContext is not VentanaPrincipalViewModel
            {
                ModuloSeleccionado: not null
            } viewModel ||
            !viewModel.PuedeEditarRegistro)
        {
            return;
        }

        Window? ventana;
        var fila = viewModel.FilaModuloSeleccionada;
        if (viewModel.ModuloSeleccionado.Nombre == "Reportes")
        {
            ventana = ServicioReportes is null
                ? null
                : new Vistas.VentanaReportes(ServicioReportes, TiendaId);
        }
        else if (viewModel.ModuloSeleccionado.Nombre == "Configuración")
        {
            ventana = ServicioConfiguracion is null
                ? null
                : new Vistas.VentanaConfiguracionTienda(ServicioConfiguracion, TiendaId);
        }
        else if (fila is null)
        {
            return;
        }
        else if (viewModel.ModuloSeleccionado.Nombre == "Inventario y lotes")
        {
            ventana = ServicioInventario is null
                ? null
                : new Vistas.VentanaDetalleLote(
                    ServicioInventario,
                    TiendaId,
                    fila.Id);
        }
        else if (viewModel.ModuloSeleccionado.Nombre == "Promociones")
        {
            ventana = ServicioPromociones is null
                ? null
                : new Vistas.VentanaEditarPromocion(
                    ServicioPromociones,
                    TiendaId,
                    fila.Id);
        }
        else if (viewModel.ModuloSeleccionado.Nombre == "Clientes")
        {
            ventana = ServicioClientes is null
                ? null
                : new Vistas.VentanaDetalleCliente(
                    ServicioClientes,
                    TiendaId,
                    fila.Id);
        }
        else if (viewModel.ModuloSeleccionado.Nombre == "Usuarios")
        {
            ventana = ServicioUsuarios is null
                ? null
                : new Vistas.VentanaUsuarioAcceso(
                    ServicioUsuarios,
                    TiendaId,
                    fila.Id);
        }
        else if (ServicioCatalogo is not null)
        {
            ventana = viewModel.ModuloSeleccionado.Nombre == "Productos"
                ? new Vistas.VentanaEditarProducto(
                    ServicioCatalogo,
                    TiendaId,
                    fila.Id, ServicioImagenes)
                : new Vistas.VentanaEditarCatalogo(
                    ServicioCatalogo,
                    TiendaId,
                    fila.Id,
                    viewModel.ModuloSeleccionado.Nombre);
        }
        else
        {
            ventana = null;
        }

        if (ventana is null)
        {
            return;
        }

        ventana.Owner = this;

        var esInventario = viewModel.ModuloSeleccionado.Nombre == "Inventario y lotes";
        if (ventana.ShowDialog() == true || esInventario)
        {
            await viewModel.CargarModuloActualAsync();
            await viewModel.CargarInicioAsync();
        }
    }

    private async void AlCambiarEstadoRegistro(object sender, RoutedEventArgs e)
    {
        if (ServicioCatalogo is null ||
            DataContext is not VentanaPrincipalViewModel
            {
                ModuloSeleccionado: not null,
                FilaModuloSeleccionada: not null
            } viewModel ||
            !viewModel.PuedeCambiarEstadoRegistro)
        {
            return;
        }

        var activar = !viewModel.RegistroSeleccionadoEstaActivo;
        var verbo = activar ? "activar" : "desactivar";
        var confirmacion = MessageBox.Show(
            $"¿Deseas {verbo} el registro seleccionado?",
            "Confirmar cambio",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmacion != MessageBoxResult.Yes)
        {
            return;
        }

        var resultado = await ServicioCatalogo.CambiarEstadoAsync(
            TiendaId,
            viewModel.ModuloSeleccionado.Nombre,
            viewModel.FilaModuloSeleccionada.Id,
            activar);

        if (!resultado.EsExitoso)
        {
            MessageBox.Show(resultado.Mensaje, "No se pudo completar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await viewModel.CargarModuloActualAsync();
        await viewModel.CargarInicioAsync();
    }

    private void AlExportarReporte(object sender, RoutedEventArgs e)
    {
        if (DataContext is not VentanaPrincipalViewModel viewModel ||
            !viewModel.PuedeExportarReporte)
        {
            return;
        }

        var dialogo = new SaveFileDialog
        {
            Title = "Exportar reporte de Cogana",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = $"reporte-cogana-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dialogo.ShowDialog(this) != true)
        {
            return;
        }

        var lineas = new List<string>
        {
            Csv(viewModel.Encabezado1, viewModel.Encabezado2, viewModel.Encabezado3,
                viewModel.Encabezado4, viewModel.Encabezado5, "Estado")
        };
        lineas.AddRange(viewModel.FilasModulo.Select(fila => Csv(
            fila.Columna1,
            fila.Columna2,
            fila.Columna3,
            fila.Columna4,
            fila.Columna5,
            fila.Estado)));

        File.WriteAllLines(dialogo.FileName, lineas, new UTF8Encoding(true));
        MessageBox.Show(
            "Reporte exportado correctamente.",
            "Exportación completada",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string Csv(params string[] valores) => string.Join(
        ';',
        valores.Select(valor => $"\"{valor.Replace("\"", "\"\"")}\""));
}
