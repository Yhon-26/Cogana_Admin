using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Text;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Escritorio.ViewModels;
using Microsoft.Win32;

namespace Cogana.Admin.Escritorio;

public partial class MainWindow : Window
{
    public IServicioCatalogoAdministrativo? ServicioCatalogo { get; init; }
    public IServicioPedidosAdministrativos? ServicioPedidos { get; init; }
    public IServicioInventarioAdministrativo? ServicioInventario { get; init; }
    public IServicioPromocionesAdministrativas? ServicioPromociones { get; init; }
    public IServicioClientesAdministrativos? ServicioClientes { get; init; }
    public IServicioUsuariosAdministrativos? ServicioUsuarios { get; init; }
    public IServicioConfiguracionTienda? ServicioConfiguracion { get; init; }
    public Guid TiendaId { get; init; }

    public MainWindow()
    {
        InitializeComponent();
        Loaded += AlCargarVentana;
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
        if (DataContext is VentanaPrincipalViewModel viewModel && viewModel.EsModuloPendiente)
        {
            await viewModel.CargarModuloActualAsync();
        }
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
                    viewModel.ModuloSeleccionado.Nombre);

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

    private void AlMoverElementoMenuParaArrastre(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            e.OriginalSource is not DependencyObject origen ||
            DataContext is not VentanaPrincipalViewModel)
        {
            return;
        }

        var elemento = BuscarAncestro<ListBoxItem>(origen);
        if (elemento?.Content is not ModuloAdministrativo modulo || modulo.Nombre == "Inicio")
        {
            return;
        }

        var posicion = e.GetPosition(elemento);
        if (Math.Abs(posicion.X - _posicionArrastre.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(posicion.Y - _posicionArrastre.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _posicionArrastre = posicion;
        OverlaySoltarSeccion.Visibility = Visibility.Visible;

        try
        {
            DragDrop.DoDragDrop(
                elemento,
                new DataObject(FormatoArrastreModulo, modulo.Nombre),
                DragDropEffects.Move);
        }
        finally
        {
            OverlaySoltarSeccion.Visibility = Visibility.Collapsed;
        }
    }

    private Point _posicionArrastre;

    private void AlArrastrarSobrePanel(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(FormatoArrastreModulo)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void AlSalirArrastrePanel(object sender, DragEventArgs e)
    {
        // La capa de arrastre se oculta cuando DoDragDrop termina; aquí no hay estado que limpiar.
    }

    private void AlSoltarModuloEnPanel(object sender, DragEventArgs e)
    {
        if (DataContext is not VentanaPrincipalViewModel viewModel)
        {
            return;
        }

        if (e.Data.GetData(FormatoArrastreModulo) is string nombreModulo)
        {
            viewModel.AgregarWidgetPorNombre(nombreModulo);
            viewModel.ModuloSeleccionado = viewModel.Modulos.FirstOrDefault(
                modulo => modulo.Nombre == "Inicio");
        }

        e.Handled = true;
    }

    private void AlAlternarSelectorSecciones(object sender, RoutedEventArgs e)
    {
        if (DataContext is VentanaPrincipalViewModel viewModel)
        {
            viewModel.AlternarSelectorSecciones();
        }
    }

    private void AlAgregarDesdeSelector(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string nombreModulo } &&
            DataContext is VentanaPrincipalViewModel viewModel)
        {
            viewModel.AgregarWidgetPorNombre(nombreModulo);
        }
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
        if (viewModel.ModuloSeleccionado.Nombre == "Configuración")
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
                    fila.Id)
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
