using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Cogana.Admin.Escritorio.Controles;

/// <summary>Ondas y partículas del menú exportado de Figma; un solo reloj, sin controles por partícula.</summary>
public sealed class EfectoSeleccionMenu : FrameworkElement
{
    public static readonly DependencyProperty TiempoProperty = DependencyProperty.Register(
        nameof(Tiempo), typeof(double), typeof(EfectoSeleccionMenu),
        new FrameworkPropertyMetadata(-1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Tiempo { get => (double)GetValue(TiempoProperty); set => SetValue(TiempoProperty, value); }
    public Point Origen { get; private set; }
    private readonly KeySpline _salida = new(0, 0, .58, 1);
    private readonly KeySpline _anillo = new(.16, .7, .3, 1);
    private readonly KeySpline _particula = new(.12, .65, .25, 1);

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
        if (Tiempo < 0 || ActualWidth <= 0 || ActualHeight <= 0) return;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize), 12, 12));
        var luz = Progreso(0, 1, _salida);
        dc.PushOpacity(Envolvente(luz, .22));
        var brillo = new LinearGradientBrush();
        brillo.GradientStops.Add(new(Colors.Transparent, 0));
        brillo.GradientStops.Add(new(Color.FromArgb(56, 255, 255, 255), .5));
        brillo.GradientStops.Add(new(Colors.Transparent, 1));
        var ancho = ActualWidth * (.3 + .7 * luz);
        dc.DrawRectangle(brillo, null, new Rect((ActualWidth - ancho) / 2, 0, ancho, ActualHeight));
        dc.Pop();

        var onda = Progreso(0, .9, _salida);
        dc.PushOpacity(Envolvente(onda, .15));
        var radial = new RadialGradientBrush();
        radial.GradientStops.Add(new(Color.FromArgb(128, 255, 255, 255), 0));
        radial.GradientStops.Add(new(Color.FromArgb(20, 255, 255, 255), .6));
        radial.GradientStops.Add(new(Colors.Transparent, .72));
        dc.DrawEllipse(radial, null, Origen, 210 * onda, 210 * onda);
        dc.Pop();

        DibujarAnillo(dc, 0, 1, Color.FromArgb(179, 255, 255, 255));
        DibujarAnillo(dc, .09, 1.1, Color.FromArgb(112, 136, 136, 136));
        var destello = Progreso(0, .5, _salida);
        dc.PushOpacity(Envolvente(destello, .2));
        dc.DrawEllipse(Brushes.White, null, Origen, 4 * (.2 + 2.3 * destello), 4 * (.2 + 2.3 * destello));
        dc.Pop();
        for (var i = 0; i < 28; i++)
        {
            var progreso = Progreso(i % 4 * .018, 1.1, _particula);
            var opacidad = progreso < .12 ? progreso / .12 : progreso < .65
                ? 1 - .3 * (progreso - .12) / .53 : .7 * (1 - progreso) / .35;
            if (opacidad <= 0) continue;
            var angulo = i * Math.PI * 2 / 28;
            var distancia = (38 + i % 5 * 18) * progreso;
            var punto = Origen + new Vector(Math.Cos(angulo) * distancia, Math.Sin(angulo) * distancia);
            var escala = .2 + .8 * Math.Sin(Math.PI * progreso);
            dc.PushOpacity(opacidad);
            var brocha = i % 3 == 0 ? Brushes.DimGray : Brushes.White;
            if (i % 4 == 0)
            {
                var mitad = new Vector(Math.Cos(angulo), Math.Sin(angulo)) * (6 * escala);
                dc.DrawLine(new Pen(brocha, 1), punto - mitad, punto + mitad);
            }
            else dc.DrawEllipse(brocha, null, punto, 1.5 * escala, 1.5 * escala);
            dc.Pop();
        }
        dc.Pop();
    }

    private void DibujarAnillo(DrawingContext dc, double demora, double duracion, Color color)
    {
        var progreso = Progreso(demora, duracion, _anillo);
        dc.PushOpacity(.8 * Envolvente(progreso, .15));
        var radio = 11 * (.2 + 6.8 * progreso);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(color), 1), Origen, radio, radio);
        dc.Pop();
    }
    private double Progreso(double demora, double duracion, KeySpline curva)
        => curva.GetSplineProgress(Math.Clamp((Tiempo - demora) / duracion, 0, 1));
    private static double Envolvente(double progreso, double pico)
        => progreso < pico ? progreso / pico : (1 - progreso) / (1 - pico);
}
