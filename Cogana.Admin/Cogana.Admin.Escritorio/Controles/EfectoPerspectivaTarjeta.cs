using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Cogana.Admin.Escritorio.Controles;

/// <summary>Perspectiva real sin Viewport3D; EffectMapping mantiene alineada la interacción nativa.</summary>
public sealed class EfectoPerspectivaTarjeta : ShaderEffect
{
    private static readonly PixelShader Sombreador = CrearSombreador();
    public static readonly DependencyProperty EntradaProperty = RegisterPixelShaderSamplerProperty(
        nameof(Entrada), typeof(EfectoPerspectivaTarjeta), 0, SamplingMode.Bilinear);
    public static readonly DependencyProperty Fila1Property = DependencyProperty.Register(nameof(Fila1), typeof(Point3D),
        typeof(EfectoPerspectivaTarjeta), new UIPropertyMetadata(new Point3D(1, 0, 0), PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty Fila2Property = DependencyProperty.Register(nameof(Fila2), typeof(Point3D),
        typeof(EfectoPerspectivaTarjeta), new UIPropertyMetadata(new Point3D(0, 1, 0), PixelShaderConstantCallback(1)));
    public static readonly DependencyProperty Fila3Property = DependencyProperty.Register(nameof(Fila3), typeof(Point3D),
        typeof(EfectoPerspectivaTarjeta), new UIPropertyMetadata(new Point3D(0, 0, 1), PixelShaderConstantCallback(2)));
    public static readonly DependencyProperty ProyeccionProperty = DependencyProperty.Register(nameof(Proyeccion),
        typeof(TransformacionPerspectiva), typeof(EfectoPerspectivaTarjeta), new PropertyMetadata(null, AlCambiarProyeccion));

    public EfectoPerspectivaTarjeta()
    {
        PixelShader = Sombreador;
        UpdateShaderValue(EntradaProperty);
        UpdateShaderValue(Fila1Property);
        UpdateShaderValue(Fila2Property);
        UpdateShaderValue(Fila3Property);
    }

    public Brush Entrada { get => (Brush)GetValue(EntradaProperty); set => SetValue(EntradaProperty, value); }
    public Point3D Fila1 { get => (Point3D)GetValue(Fila1Property); set => SetValue(Fila1Property, value); }
    public Point3D Fila2 { get => (Point3D)GetValue(Fila2Property); set => SetValue(Fila2Property, value); }
    public Point3D Fila3 { get => (Point3D)GetValue(Fila3Property); set => SetValue(Fila3Property, value); }
    public TransformacionPerspectiva? Proyeccion { get => (TransformacionPerspectiva?)GetValue(ProyeccionProperty); set => SetValue(ProyeccionProperty, value); }
    protected override GeneralTransform EffectMapping => (GeneralTransform?)Proyeccion ?? Transform.Identity;

    private static void AlCambiarProyeccion(DependencyObject objeto, DependencyPropertyChangedEventArgs e)
    {
        var efecto = (EfectoPerspectivaTarjeta)objeto;
        var inversa = (TransformacionPerspectiva)((TransformacionPerspectiva?)e.NewValue ?? new TransformacionPerspectiva()).Inverse;
        efecto.Fila1 = new(inversa.A, inversa.B, inversa.C);
        efecto.Fila2 = new(inversa.D, inversa.E, inversa.F);
        efecto.Fila3 = new(inversa.G, inversa.H, inversa.I);
    }

    private static PixelShader CrearSombreador()
    {
        var sombreador = new PixelShader
        {
            UriSource = new Uri("pack://application:,,,/Cogana.Admin.Escritorio;component/Recursos/PerspectivaTarjeta.ps"),
            ShaderRenderMode = ShaderRenderMode.Auto
        };
        sombreador.Freeze();
        return sombreador;
    }

    protected override Freezable CreateInstanceCore() => new EfectoPerspectivaTarjeta();
}
