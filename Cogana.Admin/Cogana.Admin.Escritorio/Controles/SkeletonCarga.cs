using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Automation.Peers;

namespace Cogana.Admin.Escritorio.Controles;

public enum TipoSkeleton { Resumen, Tarjeta, Tabla, PanelVacio, Reportes }

/// <summary>Marcadores visuales de carga real. Barrido, desfases y pista adaptados del CSS de Figma.</summary>
public sealed class SkeletonCarga : FrameworkElement
{
    public static readonly DependencyProperty ActivoProperty = DependencyProperty.Register(nameof(Activo),typeof(bool),typeof(SkeletonCarga),
        new PropertyMetadata(false,(o,_)=>((SkeletonCarga)o).Actualizar()));
    public static readonly DependencyProperty TipoProperty = DependencyProperty.Register(nameof(Tipo),typeof(TipoSkeleton),typeof(SkeletonCarga),
        new FrameworkPropertyMetadata(TipoSkeleton.Tabla,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TiempoProperty = DependencyProperty.Register(nameof(Tiempo),typeof(double),typeof(SkeletonCarga),
        new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    public bool Activo {get=>(bool)GetValue(ActivoProperty);set=>SetValue(ActivoProperty,value);}
    public TipoSkeleton Tipo {get=>(TipoSkeleton)GetValue(TipoProperty);set=>SetValue(TipoProperty,value);}
    public double Tiempo {get=>(double)GetValue(TiempoProperty);set=>SetValue(TiempoProperty,value);}
    private Window? _ventana;
    private readonly KeySpline _curva=new(.42,0,.58,1);
    public SkeletonCarga()
    {
        Visibility=Visibility.Collapsed;IsHitTestVisible=false;Focusable=false;
        Loaded+=(_,_)=>{_ventana=Window.GetWindow(this);if(_ventana is not null)_ventana.StateChanged+=AlEstado;
            SystemParameters.StaticPropertyChanged+=AlPreferencia;Actualizar();};
        Unloaded+=(_,_)=>{if(_ventana is not null)_ventana.StateChanged-=AlEstado;
            SystemParameters.StaticPropertyChanged-=AlPreferencia;_ventana=null;Detener();};
        IsVisibleChanged+=(_,_)=>ActualizarReloj();
    }
    protected override AutomationPeer? OnCreateAutomationPeer()=>null;
    private void AlEstado(object? sender,EventArgs e)=>ActualizarReloj();
    private void AlPreferencia(object? sender,PropertyChangedEventArgs e)
    {if(e.PropertyName==nameof(SystemParameters.ClientAreaAnimation))ActualizarReloj();}
    private void Actualizar()
    {
        Visibility=Activo?Visibility.Visible:Visibility.Collapsed;
        ActualizarReloj();
    }
    private void ActualizarReloj()
    {
        var animar=Activo&&IsLoaded&&IsVisible&&SystemParameters.ClientAreaAnimation&&_ventana?.WindowState!=WindowState.Minimized;
        if(!animar){Detener();return;}
        if(HasAnimatedProperties)return;
        BeginAnimation(TiempoProperty,new DoubleAnimation(0,187.2,TimeSpan.FromSeconds(187.2)){RepeatBehavior=RepeatBehavior.Forever});
    }
    private void Detener(){BeginAnimation(TiempoProperty,null);Tiempo=0;}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if(!Activo||ActualWidth<=0||ActualHeight<=0)return;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        switch(Tipo)
        {
            case TipoSkeleton.Resumen:Resumen(dc,4,ActualHeight);break;
            case TipoSkeleton.Reportes:
                Resumen(dc,5,110);
                dc.PushTransform(new TranslateTransform(0,130));
                Grafico(dc,new Rect(0,0,ActualWidth*.64,Math.Max(100,ActualHeight-145)));
                Grafico(dc,new Rect(ActualWidth*.66,0,ActualWidth*.34,Math.Max(100,ActualHeight-145)));
                dc.Pop();break;
            case TipoSkeleton.PanelVacio:Panel(dc);break;
            default:Tabla(dc,Tipo==TipoSkeleton.Tarjeta);break;
        }
        Pista(dc);
        dc.Pop();
    }
    private void Resumen(DrawingContext dc,int columnas,double alto)
    {
        var ancho=(ActualWidth-10*(columnas-1))/columnas;
        for(var i=0;i<columnas;i++)
        {
            var oscuro=i==columnas-1&&columnas==4;
            var caja=new Rect(i*(ancho+10),0,ancho,alto);
            dc.DrawRoundedRectangle(Brocha(oscuro?"#171717":"#FFFFFF"),new Pen(Brocha(oscuro?"#171717":"#DDDDDD"),1),caja,20,20);
            var formas=new GeometryGroup();
            Agregar(formas,new Rect(caja.X+18,22,(ancho-50)*.53,9));
            Agregar(formas,new Rect(caja.Right-36,18,18,18));
            Agregar(formas,new Rect(caja.X+18,48,oscuro?90:42,29));
            Agregar(formas,new Rect(caja.X+18,90,(ancho-36)*.65,9));
            Pintar(dc,formas,oscuro,i*.1);
        }
    }
    private void Tabla(DrawingContext dc,bool tarjeta)
    {
        var formas=new GeometryGroup();
        var inicio=tarjeta?10:54;
        var filas=tarjeta?Math.Max(3,(int)(ActualHeight/35)):Math.Max(4,(int)((ActualHeight-54)/46));
        if(!tarjeta)
        {
            dc.DrawRoundedRectangle(Brocha("#F0F1EF"),null,new Rect(0,0,ActualWidth,38),13,13);
            for(var c=0;c<6;c++)Agregar(formas,new Rect(c*ActualWidth/6+14,14,ActualWidth/6*.55,9));
        }
        for(var f=0;f<filas;f++)
        {
            var y=inicio+f*(tarjeta?35:46);
            var columnas=tarjeta?3:6;
            for(var c=0;c<columnas;c++)
            {
                var celda=ActualWidth/columnas;
                Agregar(formas,new Rect(c*celda+(tarjeta?2:14),y,Math.Max(12,celda*(c==columnas-1?.42:.65-(f%3)*.07)),tarjeta?10:12));
            }
            dc.DrawLine(new Pen(Brocha("#F0F0F0"),1),new Point(0,y+25),new Point(ActualWidth,y+25));
        }
        Pintar(dc,formas,false,0);
    }
    private void Panel(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Brocha("#F3F3F3"),null,new Rect(RenderSize),18,18);
        var formas=new GeometryGroup();var y=Math.Max(12,(ActualHeight-164)/2);var x=ActualWidth/2;
        Agregar(formas,new Rect(x-29,y,58,58),17);
        Agregar(formas,new Rect(x-62.5,y+73,125,13));
        var ancho=Math.Min(290,ActualWidth*.85);Agregar(formas,new Rect(x-ancho/2,y+94,ancho,10));
        Agregar(formas,new Rect(x-75,y+123,150,34),17);
        Pintar(dc,formas,false,0);
        if(!HasAnimatedProperties)return;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize),20,20));
        var barrido=_curva.GetSplineProgress((Tiempo%1.8)/1.8);
        dc.PushTransform(new TranslateTransform((-1+2*barrido)*ActualWidth,0));
        dc.DrawRectangle(Gradiente(101),null,new Rect(RenderSize));dc.Pop();dc.Pop();
    }
    private void Grafico(DrawingContext dc,Rect zona)
    {
        dc.DrawRoundedRectangle(Brushes.White,new Pen(Brocha("#DDDDDD"),1),zona,16,16);
        var formas=new GeometryGroup();Agregar(formas,new Rect(zona.X+18,zona.Y+20,zona.Width*.35,15));
        for(var i=0;i<7;i++)
        {
            var alto=(zona.Height-80)*(.3+(i%4)*.17);
            Agregar(formas,new Rect(zona.X+20+i*(zona.Width-40)/7,zona.Bottom-22-alto,Math.Max(8,(zona.Width-60)/9),alto));
        }
        Pintar(dc,formas,false,0);
    }
    private void Pintar(DrawingContext dc,Geometry formas,bool oscuro,double demora)
    {
        dc.DrawGeometry(Brocha(oscuro?"#363636":"#E9E9E9"),null,formas);
        if(!HasAnimatedProperties)return;
        dc.PushClip(formas);
        var fase=((Tiempo-demora)%1.6+1.6)%1.6/1.6;
        var x=(-1.1+2.2*_curva.GetSplineProgress(fase))*ActualWidth;
        dc.PushTransform(new TranslateTransform(x,0));dc.DrawRectangle(Gradiente(oscuro?(byte)36:(byte)176),null,new Rect(RenderSize));dc.Pop();dc.Pop();
    }
    private void Pista(DrawingContext dc)
    {
        if(!HasAnimatedProperties||Tipo==TipoSkeleton.Resumen)return;
        var ancho=Math.Max(0,ActualWidth-40);if(ancho==0)return;
        dc.DrawRectangle(Brocha("#05000000"),null,new Rect(20,ActualHeight-2,ancho,2));
        dc.PushClip(new RectangleGeometry(new Rect(20,ActualHeight-2,ancho,2)));
        var fase=_curva.GetSplineProgress((Tiempo%1.3)/1.3);
        dc.DrawRectangle(new LinearGradientBrush(Colors.Transparent,Color.FromRgb(119,119,119),0),null,
            new Rect(20+(-1.1+5*fase)*ancho*.35,ActualHeight-2,ancho*.35,2));dc.Pop();
    }
    private static readonly Dictionary<string,Brush> _brochas=[];
    private static Brush Brocha(string color){if(_brochas.TryGetValue(color,out var existente))return existente;var b=(SolidColorBrush)new BrushConverter().ConvertFromString(color)!;b.Freeze();_brochas[color]=b;return b;}
    private static Brush Gradiente(byte alpha)=>new LinearGradientBrush(new GradientStopCollection{
        new(Colors.Transparent,.2),new(Color.FromArgb(alpha,255,255,255),.48),new(Colors.Transparent,.76)},new Point(0,.37),new Point(1,.63));
    private static void Agregar(GeometryGroup grupo,Rect r,double radio=5)=>grupo.Children.Add(new RectangleGeometry(r,radio,radio));
}

/// <summary>Entrada corta del contenido real al finalizar su carga, sin retrasar la consulta.</summary>
public static class RevelacionContenido
{
    public static readonly DependencyProperty ActivaProperty=DependencyProperty.RegisterAttached("Activa",typeof(bool),typeof(RevelacionContenido),
        new PropertyMetadata(false,(o,e)=>{if(o is FrameworkElement elemento){if((bool)e.NewValue)elemento.IsVisibleChanged+=AlMostrar;else elemento.IsVisibleChanged-=AlMostrar;}}));
    public static bool GetActiva(DependencyObject o)=>(bool)o.GetValue(ActivaProperty);
    public static void SetActiva(DependencyObject o,bool valor)=>o.SetValue(ActivaProperty,valor);
    private static void AlMostrar(object sender,DependencyPropertyChangedEventArgs e)
    {
        var elemento=(FrameworkElement)sender;
        if(!(bool)e.NewValue){elemento.BeginAnimation(UIElement.OpacityProperty,null);return;}
        if(!elemento.IsLoaded||!SystemParameters.ClientAreaAnimation)return;
        var reloj=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(450)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        reloj.Completed+=(_,_)=>elemento.BeginAnimation(UIElement.OpacityProperty,null);
        elemento.BeginAnimation(UIElement.OpacityProperty,reloj,HandoffBehavior.SnapshotAndReplace);
    }
}
