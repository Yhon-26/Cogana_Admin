using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cogana.Admin.Escritorio.ViewModels;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaInicioSesion : Window
{
    public VentanaInicioSesion()
    {
        InitializeComponent();
        Loaded += AlCargarVentana;
    }

    private void AlCargarVentana(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargarVentana;
        CampoCorreo.Focus();
    }

    private async void AlIniciarSesion(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InicioSesionViewModel viewModel)
        {
            return;
        }

        BotonInicioSesion.IsEnabled = false;

        try
        {
            await viewModel.IniciarSesionAsync(CampoContrasena.Password);
        }
        finally
        {
            if (viewModel.EstaProcesando is false)
            {
                BotonInicioSesion.IsEnabled = true;
            }
        }
    }

    private void AlPresionarTeclaEnCampo(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AlIniciarSesion(BotonInicioSesion, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
