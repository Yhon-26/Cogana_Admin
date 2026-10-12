using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Cogana.Admin.Escritorio.Controles;

/// <summary>Capa compartida del menú: luz dentro del botón y ondas libres sobre las filas vecinas.</summary>
public sealed class EfectoSeleccionMenu : FrameworkElement
{
    public static readonly DependencyProperty TiempoProperty = DependencyProperty.Register(
        nameof(Tiempo), typeof(double), typeof(EfectoSeleccionMenu),
        new FrameworkPropertyMetadata(-1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Tiempo { get => (double)GetValue(TiempoProperty); set => SetValue(TiempoProperty, value); }
    public Point Origen { get; private set; }
    public Rect ZonaOpcion { get; set; }
    private readonly KeySpline _salida = new(0, 0, .58, 1);
    private readonly KeySpline _anillo = new(.16, .7, .3, 1);
    private readonly KeySpline _particula = new(.12, .65, .25, 1);
    private static readonly Brush Luz = GradienteRadial((0, 128), (.6, 20), (.72, 0));
    private static readonly Brush Destello = GradienteRadial((0, 180), (.25, 140), (1, 0));
    private static readonly Brush Brillo = Congelar(new LinearGradientBrush(
        new GradientStopCollection {new(Colors.Transparent,0),new(Color.FromArgb(56,255,255,255),.5),new(Colors.Transparent,1)},
        new Point(.03,.32),new Point(.97,.68)));

    public EfectoSeleccionMenu()
    {
        IsHitTestVisible = false;
        Focusable = false;
        Unloaded += (_, _) => Detener();
    }
    public void Iniciar(Point origen, bool animar)
    {
        Detener();
        Origen = origen;
        if (!animar || !IsVisible) return;
        var reloj = new DoubleAnimation(0, 1.19, TimeSpan.FromSeconds(1.19)) { FillBehavior = FillBehavior.Stop };
        reloj.Completed += (_, _) => Detener();
        BeginAnimation(TiempoProperty, reloj, HandoffBehavior.SnapshotAndReplace);
    }
    public void Detener()
    {
        BeginAnimation(TiempoProperty, null);
        Tiempo = -1;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Tiempo < 0 || RenderSize.Width <= 0 || RenderSize.Height <= 0 || ZonaOpcion.IsEmpty) return;
        // Solo la onda de luz se recorta al botón; anillos, halo y partículas atraviesan sus bordes.
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        dc.PushClip(new RectangleGeometry(ZonaOpcion,12,12));
        var luz = Progreso(0,1,_salida);
        var ancho = ZonaOpcion.Width * (.3 + .7 * luz);
        dc.PushOpacity(Envolvente(Tiempo,1,.22,1,_salida));
        dc.DrawRectangle(Brillo,null,new Rect(ZonaOpcion.X+(ZonaOpcion.Width-ancho)/2,ZonaOpcion.Y,ancho,ZonaOpcion.Height));
        dc.Pop();
        var onda = Progreso(0,.9,_salida);
        dc.PushOpacity(Envolvente(Tiempo,.9,.15,1,_salida));
        dc.DrawEllipse(Luz,null,Origen,210*onda,210*onda);
        dc.Pop();
        dc.Pop();

        DibujarAnillo(dc,0,1,Color.FromArgb(179,255,255,255));
        DibujarAnillo(dc,.09,1.1,Color.FromArgb(112,136,136,136));
        var flash = Progreso(0,.5,_salida);
        var radioFlash = 4*(.2+2.3*flash);
        dc.PushOpacity(Envolvente(Tiempo,.5,.2,1,_salida));
        dc.DrawEllipse(Destello,null,Origen,radioFlash+22,radioFlash+22);
        dc.DrawEllipse(Brushes.White,null,Origen,radioFlash,radioFlash);
        dc.Pop();
        for(var i=0;i<28;i++)
        {
            var tiempo = Tiempo-i%4*.018;
            var progreso = Progreso(i%4*.018,1.1,_particula);
            // CSS aplica la curva a cada intervalo de opacidad, no a una envolvente del progreso global.
            var opacidad = tiempo<=.132 ? Interpolar(tiempo/.132,0,1,_particula)
                : tiempo<=.715 ? Interpolar((tiempo-.132)/.583,1,.7,_particula)
                : Interpolar((tiempo-.715)/.385,.7,0,_particula);
            if(opacidad<=0) continue;
            var angulo = i*Math.PI*2/28;
            var distancia = (38+i%5*18)*progreso;
            var punto = Origen+new Vector(Math.Cos(angulo)*distancia,Math.Sin(angulo)*distancia);
            dc.PushOpacity(opacidad);
            // Los extremos del transform CSS usan scale(.2): el tamaño de la partícula es constante.
            var brocha = (i+6)%3==0 ? Brushes.DimGray : Brushes.White;
            if(i%4==0)
            {
                var mitad = new Vector(Math.Cos(angulo),Math.Sin(angulo))*1.2;
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(70,255,255,255)),2),punto-mitad,punto+mitad);
                dc.DrawLine(new Pen(brocha,.2),punto-mitad,punto+mitad);
            }
            else
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(55,255,255,255)),null,punto,1.1,1.1);
                dc.DrawEllipse(brocha,null,punto,.3,.3);
            }
            dc.Pop();
        }
        dc.Pop();
    }
    private void DibujarAnillo(DrawingContext dc,double demora,double duracion,Color color)
    {
        var progreso = Progreso(demora,duracion,_anillo);
        var escala = .2+6.8*progreso;
        var radio = 11*escala;
        dc.PushOpacity(Envolvente(Tiempo-demora,duracion,.15,.8,_anillo));
        // En CSS scale transforma también el borde de 1px y la sombra: conservar su grosor evita el anillo diminuto anterior.
        var halo = new RadialGradientBrush();
        var exterior = radio+12;
        halo.GradientStops.Add(new(Colors.Transparent,Math.Max(0,(radio-12)/exterior)));
        halo.GradientStops.Add(new(Color.FromArgb(48,255,255,255),radio/exterior));
        halo.GradientStops.Add(new(Colors.Transparent,1));
        dc.DrawEllipse(halo,null,Origen,exterior,exterior);
        dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromArgb(18,0,0,0)),escala+2),Origen,radio,radio);
        dc.DrawEllipse(null,new Pen(new SolidColorBrush(color),escala),Origen,radio,radio);
        dc.Pop();
    }
    private double Progreso(double demora,double duracion,KeySpline curva)
        => curva.GetSplineProgress(Math.Clamp((Tiempo-demora)/duracion,0,1));
    private static double Envolvente(double tiempo,double duracion,double pico,double maximo,KeySpline curva)
    {
        var t=tiempo/duracion;
        return t<pico ? Interpolar(t/pico,0,maximo,curva) : Interpolar((t-pico)/(1-pico),maximo,0,curva);
    }
    private static double Interpolar(double t,double desde,double hasta,KeySpline curva)
        => desde+(hasta-desde)*curva.GetSplineProgress(Math.Clamp(t,0,1));
    private static Brush GradienteRadial(params (double offset,byte alpha)[] puntos)
    {
        var brocha=new RadialGradientBrush();
        foreach(var (offset,alpha) in puntos) brocha.GradientStops.Add(new(Color.FromArgb(alpha,255,255,255),offset));
        return Congelar(brocha);
    }
    private static T Congelar<T>(T recurso) where T:Freezable {recurso.Freeze();return recurso;}
}
