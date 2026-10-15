using System.Windows;
using System.Windows.Input;

namespace Cogana.Admin.Escritorio.Controles;

/// <summary>Comandos de la cabecera común. WindowChrome conserva el arrastre y los bordes nativos.</summary>
public static class MarcoVentana
{
    public static readonly RoutedCommand AlternarTamanoCommand = new();
    public static readonly DependencyProperty ActivoProperty = DependencyProperty.RegisterAttached(
        "Activo", typeof(bool), typeof(MarcoVentana), new PropertyMetadata(false, AlActivar));
    public static bool GetActivo(DependencyObject objeto) => (bool)objeto.GetValue(ActivoProperty);
    public static void SetActivo(DependencyObject objeto, bool valor) => objeto.SetValue(ActivoProperty, valor);

    private static void AlActivar(DependencyObject objeto, DependencyPropertyChangedEventArgs cambio)
    {
        if (objeto is not Window ventana || !(bool)cambio.NewValue) return;
        ventana.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand,
            (_, e) => { SystemCommands.CloseWindow(ventana); e.Handled = true; }));
        ventana.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand,
            (_, e) => { SystemCommands.MinimizeWindow(ventana); e.Handled = true; },
            (_, e) => { e.CanExecute = ventana.ResizeMode != ResizeMode.NoResize; e.Handled = true; }));
        ventana.CommandBindings.Add(new CommandBinding(AlternarTamanoCommand,
            (_, e) =>
            {
                if (ventana.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(ventana);
                else SystemCommands.MaximizeWindow(ventana);
                e.Handled = true;
            },
            (_, e) => { e.CanExecute = ventana.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip; e.Handled = true; }));
    }
}
