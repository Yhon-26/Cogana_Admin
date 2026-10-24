using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Vistas;

public sealed class PanelOperacionTienda:ScrollViewer
{
    public event Action? Guardado;
    public bool EstaGuardando {get;private set;}
    private readonly IServicioOperacionTienda _servicio;private readonly Guid _tienda;
    private readonly StackPanel _contenido=new(){Margin=new Thickness(16)};
    private readonly TextBlock _mensaje=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)};
    private readonly TextBox _yape=new(),_yapeTelefono=new(),_plin=new(),_plinTelefono=new(),_minutos=new(),_publico=new();
    private readonly ComboBox _estado=new(){DisplayMemberPath="Nombre",SelectedValuePath="Codigo"};
    private bool _ocupado;
    public PanelOperacionTienda(IServicioOperacionTienda servicio,Guid tienda)
    {
        _servicio=servicio;_tienda=tienda;Content=_contenido;VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        AutomationProperties.SetLiveSetting(_mensaje,AutomationLiveSetting.Polite);_contenido.Children.Add(_mensaje);
        Titulo("Destinatarios y plazo de pago");Campo("Nombre del destinatario Yape",_yape);Campo("Teléfono Yape",_yapeTelefono);
        Campo("Nombre del destinatario Plin",_plin);Campo("Teléfono Plin",_plinTelefono);Campo("Plazo para reportar el pago (1–120 minutos)",_minutos);
        Boton("Guardar destinatarios de pago",async()=>
        {
            if(!int.TryParse(_minutos.Text,out var minutos)||minutos is <1 or >120)return ResultadoOperacion.Fallida("El plazo de pago debe estar entre 1 y 120 minutos.");
            bool Valido(string nombre,string telefono)
            {
                if(string.IsNullOrWhiteSpace(nombre)&&string.IsNullOrWhiteSpace(telefono))return true;
                var digitos=new string(telefono.Where(char.IsDigit).ToArray());
                return !string.IsNullOrWhiteSpace(nombre)&&digitos.Length is >=9 and <=15 && telefono.All(c=>char.IsDigit(c)||c is '+' or ' ' or '-');
            }
            if(!Valido(_yape.Text,_yapeTelefono.Text)||!Valido(_plin.Text,_plinTelefono.Text))return ResultadoOperacion.Fallida("Completa nombre y teléfono válidos de cada destinatario o deja ambos vacíos.");
            return await _servicio.GuardarPagosAsync(_tienda,new(_yape.Text,_yapeTelefono.Text,_plin.Text,_plinTelefono.Text,minutos));
        });
        Titulo("Estado operativo");
        _estado.ItemsSource=new[]{new{Codigo="closed",Nombre="Cerrado"},new{Codigo="accepting_orders",Nombre="Aceptando pedidos"},
            new{Codigo="preparing_orders",Nombre="Preparando pedidos"},new{Codigo="paused",Nombre="Pausado"}};
        Campo("Estado de atención",_estado);Campo("Mensaje público",_publico);
        Boton("Guardar estado operativo",()=>_servicio.GuardarEstadoAsync(_tienda,_estado.SelectedValue?.ToString()??"closed",_publico.Text));
        Loaded+=AlCargar;
    }
    private async void AlCargar(object sender,RoutedEventArgs e)
    {
        Loaded-=AlCargar;_ocupado=true;_contenido.IsEnabled=false;_mensaje.Text="Consultando operación y pagos…";
        try
        {
            var datos=await _servicio.ObtenerAsync(_tienda);
            _yape.Text=datos.Pagos.YapeNombre;_yapeTelefono.Text=datos.Pagos.YapeTelefono;_plin.Text=datos.Pagos.PlinNombre;_plinTelefono.Text=datos.Pagos.PlinTelefono;
            _minutos.Text=datos.Pagos.MinutosPago.ToString();_estado.SelectedValue=datos.Estado;_publico.Text=datos.Mensaje;
            foreach(var regla in datos.Reglas) AgregarRegla(regla);
            _mensaje.Text="Cada sección se guarda por separado. Los cambios se comparten con el catálogo y checkout móviles.";
        }
        catch(Exception error)when(error is HttpRequestException or JsonException or InvalidOperationException or IndexOutOfRangeException)
        {
            _mensaje.Text="No se pudo consultar esta configuración. Cierra y vuelve a abrir Configuración.";
            return;
        }
        finally{_ocupado=false;}
        _contenido.IsEnabled=true;
    }
    private void AgregarRegla(ReglaAtencion regla)
    {
        Titulo(regla.Modalidad=="pickup"?"Franjas de recojo":"Franjas de delivery");
        var franja=new TextBox{Text=regla.MinutosFranja.ToString()};var cupo=new TextBox{Text=regla.Cupo.ToString()};
        var aviso=new TextBox{Text=regla.Anticipacion.ToString()};var personal=new TextBox{Text=regla.Repartidores?.ToString()};
        Campo("Duración de franja (1–1440 minutos)",franja);Campo(regla.Modalidad=="delivery"?"Pedidos por repartidor y franja (1–1000)":"Pedidos por franja (1–1000)",cupo);
        Campo("Anticipación mínima (0–10080 minutos)",aviso);if(regla.Modalidad=="delivery")Campo("Repartidores disponibles (0–100)",personal);
        Boton(regla.Modalidad=="pickup"?"Guardar franjas de recojo":"Guardar franjas de delivery",async()=>
        {
            if(!int.TryParse(franja.Text,out var f)||f is <1 or >1440 || !int.TryParse(cupo.Text,out var c)||c is <1 or >1000 ||
                !int.TryParse(aviso.Text,out var a)||a is <0 or >10080)return ResultadoOperacion.Fallida("Revisa los rangos de franja, cupo y anticipación.");
            int? r=null;
            if(regla.Modalidad=="delivery") { if(!int.TryParse(personal.Text,out var n)||n is <0 or >100)return ResultadoOperacion.Fallida("Revisa la cantidad de repartidores (0–100).");r=n; }
            return await _servicio.GuardarReglaAsync(_tienda,new(regla.Modalidad,f,c,a,r));
        });
    }
    private void Campo(string nombre,Control control)
    {
        AutomationProperties.SetName(control,nombre);_contenido.Children.Add(new Label{Content=nombre,Target=control,Margin=new Thickness(0,8,0,0)});
        if(control is TextBox texto){texto.MaxLength=150;texto.Style=(Style)FindResource("CampoFormulario");}
        if(control is ComboBox selector)selector.Style=(Style)FindResource("SelectorFormulario");
        _contenido.Children.Add(control);
    }
    private void Titulo(string texto)=>_contenido.Children.Add(new TextBlock{Text=texto,FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,20,0,8)});
    private void Boton(string texto,Func<Task<ResultadoOperacion>> accion)
    {
        var boton=new Button{Content=texto,Style=(Style)FindResource("BotonSecundario"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,12)};
        boton.Click+=async(_,_)=>
        {
            if(_ocupado)return;_ocupado=true;EstaGuardando=true;_contenido.IsEnabled=false;_mensaje.Text="Guardando sección…";
            try{var resultado=await accion();_mensaje.Text=resultado.Mensaje;if(resultado.EsExitoso)Guardado?.Invoke();}
            finally{_ocupado=false;EstaGuardando=false;_contenido.IsEnabled=true;}
        };
        _contenido.Children.Add(boton);
    }
}
