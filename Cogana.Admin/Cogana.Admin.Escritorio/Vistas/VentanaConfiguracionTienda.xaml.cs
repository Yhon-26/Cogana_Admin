using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public partial class VentanaConfiguracionTienda : Window
{
    private readonly IServicioConfiguracionTienda _servicio;
    private readonly Guid _tiendaId;
    private Guid _operacionId = Guid.NewGuid();
    private readonly ObservableCollection<HorarioFormulario> _horarios = [];
    private readonly ObservableCollection<ExcepcionFormulario> _excepciones = [];

    public VentanaConfiguracionTienda(IServicioConfiguracionTienda servicio, Guid tiendaId)
    {
        _servicio = servicio;
        _tiendaId = tiendaId;
        InitializeComponent();
        TablaHorarios.ItemsSource = _horarios;
        TablaExcepciones.ItemsSource = _excepciones;
        Loaded += AlCargar;
    }

    private async void AlCargar(object sender, RoutedEventArgs e)
    {
        Loaded -= AlCargar;
        MensajeFormulario.Text = "Cargando configuración desde Supabase...";
        try
        {
            var configuracion = await _servicio.ObtenerAsync(_tiendaId);
            if (configuracion is null)
            {
                MensajeFormulario.Text = "La tienda ya no está disponible o no tienes acceso.";
                return;
            }

            Mostrar(configuracion);
            BotonGuardar.IsEnabled = true;
            MensajeFormulario.Text = string.Empty;
        }
        catch (HttpRequestException)
        {
            MensajeFormulario.Text = "No fue posible cargar la configuración. Revisa la conexión con Supabase.";
        }
    }

    private void Mostrar(ConfiguracionTiendaDetalle configuracion)
    {
        NombreTienda.Text = configuracion.NombreTienda;
        NombreComercial.Text = configuracion.NombreComercial;
        RazonSocial.Text = configuracion.RazonSocial ?? string.Empty;
        Ruc.Text = configuracion.Ruc ?? string.Empty;
        Direccion.Text = configuracion.Direccion ?? string.Empty;
        Distrito.Text = configuracion.Distrito ?? string.Empty;
        Telefono.Text = configuracion.Telefono ?? string.Empty;
        Whatsapp.Text = configuracion.Whatsapp ?? string.Empty;
        AceptaRecojo.IsChecked = configuracion.AceptaRecojo;
        AceptaDelivery.IsChecked = configuracion.AceptaDelivery;
        AceptaEfectivoRecojo.IsChecked = configuracion.AceptaEfectivoRecojo;
        AceptaYape.IsChecked = configuracion.AceptaYape;
        AceptaPlin.IsChecked = configuracion.AceptaPlin;
        AceptaTarjeta.IsChecked = configuracion.AceptaTarjeta;
        MinutosPreparacion.Text = configuracion.MinutosPreparacion.ToString(CultureInfo.InvariantCulture);

        _horarios.Clear();
        foreach (var horario in configuracion.Horarios.OrderBy(item => item.DiaSemana))
        {
            _horarios.Add(new HorarioFormulario
            {
                DiaSemana = horario.DiaSemana,
                NombreDia = NombreDia(horario.DiaSemana),
                Cerrado = horario.Cerrado,
                Apertura = FormatearHora(horario.Apertura),
                Cierre = FormatearHora(horario.Cierre)
            });
        }

        _excepciones.Clear();
        foreach (var excepcion in configuracion.Excepciones.OrderBy(item => item.Fecha))
        {
            _excepciones.Add(new ExcepcionFormulario
            {
                Fecha = excepcion.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                Cerrado = excepcion.Cerrado,
                Apertura = FormatearHora(excepcion.Apertura),
                Cierre = FormatearHora(excepcion.Cierre),
                Mensaje = excepcion.Mensaje ?? string.Empty
            });
        }
    }

    private void AlAgregarExcepcion(object sender, RoutedEventArgs e)
    {
        var fecha = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        while (_excepciones.Any(item => TryLeerFecha(item.Fecha, out var registrada) && registrada == fecha))
        {
            fecha = fecha.AddDays(1);
        }

        var nueva = new ExcepcionFormulario
        {
            Fecha = fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            Apertura = "08:00",
            Cierre = "20:00",
            Mensaje = string.Empty
        };
        _excepciones.Add(nueva);
        TablaExcepciones.SelectedItem = nueva;
        TablaExcepciones.ScrollIntoView(nueva);
    }

    private void AlQuitarExcepcion(object sender, RoutedEventArgs e)
    {
        if (TablaExcepciones.SelectedItem is ExcepcionFormulario seleccionada)
        {
            _excepciones.Remove(seleccionada);
        }
    }

    private async void AlGuardar(object sender, RoutedEventArgs e)
    {
        TablaHorarios.CommitEdit();
        TablaExcepciones.CommitEdit();
        var validacion = Validar(out var horarios, out var excepciones);
        MensajeFormulario.Text = validacion ?? string.Empty;
        if (validacion is not null) return;

        BotonGuardar.IsEnabled = false;
        MensajeFormulario.Text = "Guardando datos, canales y horarios en Supabase...";
        var resultado = await _servicio.GuardarAsync(
            _tiendaId,
            new EdicionConfiguracionTienda(
                _operacionId,
                NombreTienda.Text,
                Limpiar(Direccion.Text),
                Limpiar(Distrito.Text),
                NombreComercial.Text,
                Limpiar(RazonSocial.Text),
                Limpiar(Ruc.Text),
                Limpiar(Telefono.Text),
                Limpiar(Whatsapp.Text),
                AceptaRecojo.IsChecked == true,
                AceptaDelivery.IsChecked == true,
                AceptaEfectivoRecojo.IsChecked == true,
                AceptaYape.IsChecked == true,
                AceptaPlin.IsChecked == true,
                AceptaTarjeta.IsChecked == true,
                int.Parse(MinutosPreparacion.Text, CultureInfo.InvariantCulture),
                horarios,
                excepciones));

        MensajeFormulario.Text = resultado.Mensaje;
        BotonGuardar.IsEnabled = true;
        if (resultado.EsExitoso)
        {
            DialogResult = true;
        }
    }

    private string? Validar(
        out IReadOnlyList<HorarioAtencionDetalle> horarios,
        out IReadOnlyList<ExcepcionHorarioDetalle> excepciones)
    {
        horarios = [];
        excepciones = [];
        if (string.IsNullOrWhiteSpace(NombreTienda.Text)) return "Ingresa el nombre de la tienda.";
        if (string.IsNullOrWhiteSpace(NombreComercial.Text)) return "Ingresa el nombre comercial.";
        if (!string.IsNullOrWhiteSpace(Ruc.Text) &&
            (Ruc.Text.Trim().Length != 11 || !Ruc.Text.Trim().All(char.IsDigit)))
            return "El RUC debe contener 11 dígitos.";
        if (AceptaRecojo.IsChecked != true && AceptaDelivery.IsChecked != true)
            return "Habilita recojo o delivery.";
        if (AceptaEfectivoRecojo.IsChecked == true && AceptaRecojo.IsChecked != true)
            return "Para aceptar efectivo al recoger, habilita el recojo en tienda.";
        if (!int.TryParse(MinutosPreparacion.Text, out var minutos) || minutos is < 1 or > 1440)
            return "El tiempo de preparación debe estar entre 1 y 1440 minutos.";

        var horariosLeidos = new List<HorarioAtencionDetalle>();
        foreach (var item in _horarios)
        {
            if (item.Cerrado)
            {
                horariosLeidos.Add(new HorarioAtencionDetalle(item.DiaSemana, null, null, true));
                continue;
            }

            if (!TryLeerHora(item.Apertura, out var apertura) || !TryLeerHora(item.Cierre, out var cierre))
                return $"Revisa el horario de {item.NombreDia}. Usa el formato 08:00.";
            if (cierre <= apertura) return $"El cierre de {item.NombreDia} debe ser posterior a la apertura.";
            horariosLeidos.Add(new HorarioAtencionDetalle(item.DiaSemana, apertura, cierre, false));
        }

        var fechas = new HashSet<DateOnly>();
        var excepcionesLeidas = new List<ExcepcionHorarioDetalle>();
        foreach (var item in _excepciones)
        {
            if (!TryLeerFecha(item.Fecha, out var fecha))
                return "Revisa las fechas especiales. Usa el formato dd/MM/aaaa.";
            if (!fechas.Add(fecha)) return $"La fecha {fecha:dd/MM/yyyy} está repetida.";

            if (item.Cerrado)
            {
                excepcionesLeidas.Add(new ExcepcionHorarioDetalle(null, fecha, null, null, true, Limpiar(item.Mensaje)));
                continue;
            }

            if (!TryLeerHora(item.Apertura, out var apertura) || !TryLeerHora(item.Cierre, out var cierre))
                return $"Revisa el horario especial del {fecha:dd/MM/yyyy}.";
            if (cierre <= apertura) return $"El cierre del {fecha:dd/MM/yyyy} debe ser posterior a la apertura.";
            excepcionesLeidas.Add(new ExcepcionHorarioDetalle(null, fecha, apertura, cierre, false, Limpiar(item.Mensaje)));
        }

        horarios = horariosLeidos;
        excepciones = excepcionesLeidas;
        return null;
    }

    private static bool TryLeerHora(string? texto, out TimeSpan hora) =>
        TimeSpan.TryParseExact(texto?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out hora) ||
        TimeSpan.TryParse(texto, CultureInfo.InvariantCulture, out hora);

    private static bool TryLeerFecha(string? texto, out DateOnly fecha) =>
        DateOnly.TryParseExact(texto?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha) ||
        DateOnly.TryParseExact(texto?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

    private static string FormatearHora(TimeSpan? hora) => hora?.ToString(@"hh\:mm") ?? string.Empty;
    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    private static string NombreDia(int dia) => dia switch
    {
        1 => "Lunes",
        2 => "Martes",
        3 => "Miércoles",
        4 => "Jueves",
        5 => "Viernes",
        6 => "Sábado",
        7 => "Domingo",
        _ => "Día"
    };

    private void AlCerrar(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed class HorarioFormulario
    {
        public int DiaSemana { get; init; }
        public string NombreDia { get; init; } = string.Empty;
        public bool Cerrado { get; set; }
        public string Apertura { get; set; } = string.Empty;
        public string Cierre { get; set; } = string.Empty;
    }

    private sealed class ExcepcionFormulario
    {
        public string Fecha { get; set; } = string.Empty;
        public bool Cerrado { get; set; }
        public string Apertura { get; set; } = string.Empty;
        public string Cierre { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }
}
