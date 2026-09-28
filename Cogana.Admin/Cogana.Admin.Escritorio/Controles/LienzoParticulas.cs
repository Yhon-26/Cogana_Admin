using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Cogana.Admin.Escritorio.Controles;

/// <summary>
/// Red de partículas conectadas del diseño de referencia: puntos de colores que
/// derivan, se atraen hacia el centro y se unen con líneas cuando están cerca.
/// Se dibuja por fotograma con un DrawingVisual; activar solo durante la carga.
/// </summary>
public sealed class LienzoParticulas : FrameworkElement
{
    private const int CantidadParticulas = 150;
    private const double DistanciaUnion = 96;

    private static readonly Color[] PaletaColores =
    [
        Color.FromRgb(0x4A, 0xDE, 0x80),
        Color.FromRgb(0x22, 0xD3, 0xEE),
        Color.FromRgb(0x81, 0x8C, 0xF8),
        Color.FromRgb(0x34, 0xD3, 0x99),
        Color.FromRgb(0x60, 0xA5, 0xFA)
    ];

    private sealed class Particula
    {
        public double X, Y, Vx, Vy, Radio, Alpha, Pulso, VelocidadPulso;
        public Color Color = Colors.Transparent;
    }

    private readonly DrawingVisual _lienzo = new();
    private readonly List<Particula> _particulas = [];
    private readonly Random _azar = new();
    private readonly Dictionary<int, Brush> _brushesPunto = [];
    private readonly Dictionary<int, Pen> _pensLinea = [];
    private readonly Dictionary<int, Brush> _brushesHalos = [];

    public LienzoParticulas()
    {
        CompositionTarget.Rendering += AlRenderizar;
    }

    /// <summary>Activa o desactiva la simulación y el dibujo.</summary>
    public bool Animando { get; set; }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _lienzo;

    private void AlRenderizar(object? sender, EventArgs e)
    {
        if (!Animando || ActualWidth < 40 || ActualHeight < 40)
        {
            return;
        }

        if (_particulas.Count == 0)
        {
            for (var i = 0; i < CantidadParticulas; i++)
            {
                _particulas.Add(new Particula
                {
                    X = _azar.NextDouble() * ActualWidth,
                    Y = _azar.NextDouble() * ActualHeight,
                    Vx = (_azar.NextDouble() - 0.5) * 0.5,
                    Vy = (_azar.NextDouble() - 0.5) * 0.5,
                    Radio = _azar.NextDouble() * 2 + 0.3,
                    Alpha = _azar.NextDouble() * 0.6 + 0.1,
                    Color = PaletaColores[_azar.Next(PaletaColores.Length)],
                    Pulso = _azar.NextDouble() * Math.PI * 2,
                    VelocidadPulso = _azar.NextDouble() * 0.03 + 0.01
                });
            }
        }

        var centroX = ActualWidth / 2;
        var centroY = ActualHeight / 2;

        using (var ctx = _lienzo.RenderOpen())
        {
            // Líneas entre partículas cercanas (alpha según distancia)
            for (var i = 0; i < _particulas.Count; i++)
            {
                var a = _particulas[i];
                for (var j = i + 1; j < _particulas.Count; j++)
                {
                    var b = _particulas[j];
                    var dx = a.X - b.X;
                    var dy = a.Y - b.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist >= DistanciaUnion)
                    {
                        continue;
                    }

                    var alpha = 0.05 * (1 - dist / DistanciaUnion);
                    var pen = PenLinea(alpha);
                    ctx.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, b.Y));
                }
            }

            // Partículas con halo suave y pulso de opacidad
            foreach (var p in _particulas)
            {
                p.X += p.Vx;
                p.Y += p.Vy;
                p.Pulso += p.VelocidadPulso;
                if (p.X < 0) p.X = ActualWidth;
                if (p.X > ActualWidth) p.X = 0;
                if (p.Y < 0) p.Y = ActualHeight;
                if (p.Y > ActualHeight) p.Y = 0;

                var dx = centroX - p.X;
                var dy = centroY - p.Y;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 1)
                {
                    p.Vx += dx / dist * 0.002;
                    p.Vy += dy / dist * 0.002;
                }
                p.Vx *= 0.998;
                p.Vy *= 0.998;

                var alpha = p.Alpha * (0.7 + 0.3 * Math.Sin(p.Pulso));
                var centro = new Point(p.X, p.Y);
                ctx.DrawEllipse(BrushHalos(p.Color, alpha), null, centro, p.Radio * 2.4, p.Radio * 2.4);
                ctx.DrawEllipse(BrushPunto(p.Color, alpha), null, centro, p.Radio, p.Radio);
            }
        }
    }

    private static byte CubetaAlpha(double alpha) =>
        (byte)Math.Clamp((int)(alpha * 12), 0, 11);

    private Brush BrushPunto(Color color, double alpha)
    {
        var cubeta = (color.GetHashCode() % 5, CubetaAlpha(alpha)).GetHashCode();
        if (_brushesPunto.TryGetValue(cubeta, out var brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(Color.FromArgb(
            (byte)(alpha * 255), color.R, color.G, color.B));
        brush.Freeze();
        _brushesPunto[cubeta] = brush;
        return brush;
    }

    private Brush BrushHalos(Color color, double alpha)
    {
        var cubeta = (color.GetHashCode() % 5, CubetaAlpha(alpha) + 100).GetHashCode();
        if (_brushesHalos.TryGetValue(cubeta, out var brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(Color.FromArgb(
            (byte)(alpha * 255 * 0.25), color.R, color.G, color.B));
        brush.Freeze();
        _brushesHalos[cubeta] = brush;
        return brush;
    }

    private Pen PenLinea(double alpha)
    {
        var cubeta = CubetaAlpha(alpha);
        if (_pensLinea.TryGetValue(cubeta, out var pen))
        {
            return pen;
        }

        pen = new Pen(new SolidColorBrush(Color.FromArgb(
            (byte)(alpha * 255), 0x4A, 0xDE, 0x80)), 0.5);
        pen.Freeze();
        _pensLinea[cubeta] = pen;
        return pen;
    }
}
