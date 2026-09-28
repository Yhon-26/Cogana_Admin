using System.Net.Mail;
using System.Net.Http;
using System.Windows;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaUsuarioAcceso : Window
{
    private readonly IServicioUsuariosAdministrativos _servicio;
    private readonly Guid _tiendaId;
    private readonly Guid? _usuarioId;

    public VentanaUsuarioAcceso(
        IServicioUsuariosAdministrativos servicio,
        Guid tiendaId,
        Guid? usuarioId = null)
    {
        _servicio = servicio;
        _tiendaId = tiendaId;
        _usuarioId = usuarioId;
        InitializeComponent();

        Rol.SelectedValue = "admin";
        if (_usuarioId is not null)
        {
            Title = "Gestionar acceso";
            TituloFormulario.Text = "Gestionar usuario";
            DescripcionFormulario.Text = "Perfil, rol y disponibilidad de acceso";
            EtiquetaModo.Text = "ACCESO EXISTENTE";
            BotonGuardar.Content = "Guardar cambios";
            SeccionContrasena.Visibility = Visibility.Collapsed;
            SeccionEstado.Visibility = Visibility.Visible;
            Loaded += AlCargar;
        }
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargar;
        BotonGuardar.IsEnabled = false;
        MensajeFormulario.Text = "Cargando usuario desde Supabase...";

        try
        {
            var usuario = await _servicio.ObtenerUsuarioAsync(_tiendaId, _usuarioId!.Value);
            if (usuario is null)
            {
                MensajeFormulario.Text = "El usuario ya no pertenece a esta tienda.";
                return;
            }

            Correo.Text = usuario.Correo;
            Correo.IsReadOnly = true;
            NombreCompleto.Text = usuario.NombreCompleto == "Sin nombre registrado"
                ? string.Empty
                : usuario.NombreCompleto;
            Telefono.Text = usuario.Telefono ?? string.Empty;
            Rol.SelectedValue = usuario.Rol;
            AccesoActivo.IsChecked = usuario.EstaActivo;
            MensajeFormulario.Text = string.Empty;
            BotonGuardar.IsEnabled = true;
        }
        catch (HttpRequestException ex)
        {
            MensajeFormulario.Text = ex.Message;
        }
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        var error = Validar();
        if (error is not null)
        {
            MensajeFormulario.Text = error;
            return;
        }

        BotonGuardar.IsEnabled = false;
        MensajeFormulario.Text = _usuarioId is null
            ? "Creando acceso seguro..."
            : "Guardando cambios de acceso...";

        var rol = Rol.SelectedValue?.ToString() ?? "admin";
        var resultado = _usuarioId is null
            ? await _servicio.InvitarAsync(
                _tiendaId,
                new InvitacionUsuarioAdministrativo(
                    Correo.Text,
                    NombreCompleto.Text,
                    Telefono.Text,
                    rol,
                    Contrasena.Password))
            : await _servicio.ActualizarAsync(
                _tiendaId,
                new EdicionUsuarioAdministrativo(
                    _usuarioId.Value,
                    NombreCompleto.Text,
                    Telefono.Text,
                    rol,
                    AccesoActivo.IsChecked == true));

        MensajeFormulario.Text = resultado.Mensaje;
        BotonGuardar.IsEnabled = true;
        if (resultado.EsExitoso)
        {
            DialogResult = true;
        }
    }

    private string? Validar()
    {
        if (string.IsNullOrWhiteSpace(Correo.Text))
            return "Ingresa el correo electrónico.";

        try
        {
            _ = new MailAddress(Correo.Text.Trim());
        }
        catch (FormatException)
        {
            return "Ingresa un correo electrónico válido.";
        }

        if (string.IsNullOrWhiteSpace(NombreCompleto.Text))
            return "Ingresa el nombre completo.";
        if (Rol.SelectedValue is null)
            return "Selecciona un rol para la tienda.";
        if (_usuarioId is null && Contrasena.Password.Length < 10)
            return "La contraseña inicial debe tener al menos 10 caracteres.";
        if (_usuarioId is null && Contrasena.Password != ConfirmarContrasena.Password)
            return "Las contraseñas no coinciden.";

        return null;
    }

    private void AlCancelar(object sender, RoutedEventArgs e) => DialogResult = false;
}
