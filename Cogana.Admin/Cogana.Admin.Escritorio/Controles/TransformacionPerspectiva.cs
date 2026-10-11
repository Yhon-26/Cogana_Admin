using System.Windows;
using System.Windows.Media;

namespace Cogana.Admin.Escritorio.Controles;

/// <summary>Homografía de un plano girado en X/Y y proyectado con cámara perspectiva.</summary>
public sealed class TransformacionPerspectiva : GeneralTransform
{
    public double A { get; init; } = 1;
    public double B { get; init; }
    public double C { get; init; }
    public double D { get; init; }
    public double E { get; init; } = 1;
    public double F { get; init; }
    public double G { get; init; }
    public double H { get; init; }
    public double I { get; init; } = 1;

    public static TransformacionPerspectiva Crear(double ancho, double alto, double giroX, double giroY)
    {
        if (!double.IsFinite(ancho) || !double.IsFinite(alto) || ancho <= 0 || alto <= 0)
            throw new ArgumentOutOfRangeException(nameof(ancho));
        if (!double.IsFinite(giroX) || !double.IsFinite(giroY)) throw new ArgumentOutOfRangeException(nameof(giroX));
        var x = Math.Clamp(giroX, -8, 8) * Math.PI / 180;
        var y = Math.Clamp(giroY, -10, 10) * Math.PI / 180;
        var distancia = Math.Max(ancho, alto) * 1.8;
        var a = Math.Cos(y);
        var b = Math.Sin(y) * Math.Sin(x) * alto / ancho;
        var e = Math.Cos(x);
        var g = Math.Sin(y) * ancho / distancia;
        var h = -Math.Cos(y) * Math.Sin(x) * alto / distancia;
        var i = 1 - (g + h) / 2;
        // Coordenadas de textura [0,1]; origen de rotación en el centro de la tarjeta.
        var transformacion = new TransformacionPerspectiva
        {
            A = a + g / 2, B = b + h / 2, C = i / 2 - (a + b) / 2,
            D = g / 2, E = e + h / 2, F = i / 2 - e / 2,
            G = g, H = h, I = i
        };
        transformacion.Freeze();
        return transformacion;
    }

    public override bool TryTransform(Point punto, out Point resultado)
    {
        var divisor = G * punto.X + H * punto.Y + I;
        resultado = default;
        if (!double.IsFinite(divisor) || Math.Abs(divisor) < 1e-9) return false;
        resultado = new Point((A * punto.X + B * punto.Y + C) / divisor,
            (D * punto.X + E * punto.Y + F) / divisor);
        return double.IsFinite(resultado.X) && double.IsFinite(resultado.Y);
    }

    public override GeneralTransform Inverse
    {
        get
        {
            var determinante = A * (E * I - F * H) - B * (D * I - F * G) + C * (D * H - E * G);
            if (Math.Abs(determinante) < 1e-9) throw new InvalidOperationException("La perspectiva no es invertible.");
            var inversa = new TransformacionPerspectiva
            {
                A = (E * I - F * H) / determinante, B = (C * H - B * I) / determinante, C = (B * F - C * E) / determinante,
                D = (F * G - D * I) / determinante, E = (A * I - C * G) / determinante, F = (C * D - A * F) / determinante,
                G = (D * H - E * G) / determinante, H = (B * G - A * H) / determinante, I = (A * E - B * D) / determinante
            };
            inversa.Freeze();
            return inversa;
        }
    }

    public override Rect TransformBounds(Rect rectangulo)
    {
        if (rectangulo.IsEmpty) return Rect.Empty;
        var limites = Rect.Empty;
        foreach (var punto in new[] { rectangulo.TopLeft, rectangulo.TopRight, rectangulo.BottomLeft, rectangulo.BottomRight })
        {
            if (!TryTransform(punto, out var proyectado)) return Rect.Empty;
            limites.Union(proyectado);
        }
        limites.Inflate(1e-12, 1e-12); // Compensa el redondeo de Rect al reconstruir Right/Bottom.
        return limites;
    }

    protected override Freezable CreateInstanceCore() => new TransformacionPerspectiva
        { A = A, B = B, C = C, D = D, E = E, F = F, G = G, H = H, I = I };
}
