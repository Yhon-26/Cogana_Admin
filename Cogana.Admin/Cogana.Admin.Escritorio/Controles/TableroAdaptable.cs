using System.Windows;
using System.Windows.Controls;

namespace Cogana.Admin.Escritorio.Controles;

/// <summary>Distribuye tarjetas en columnas iguales y filas que se ajustan al contenido.</summary>
public sealed class TableroAdaptable : Panel
{
    public static readonly DependencyProperty AnchoMinimoTarjetaProperty = DependencyProperty.Register(
        nameof(AnchoMinimoTarjeta), typeof(double), typeof(TableroAdaptable),
        new FrameworkPropertyMetadata(330d, FrameworkPropertyMetadataOptions.AffectsMeasure),
        valor => valor is double numero && double.IsFinite(numero) && numero > 0);
    public static readonly DependencyProperty MaximoColumnasProperty = DependencyProperty.Register(
        nameof(MaximoColumnas), typeof(int), typeof(TableroAdaptable),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure),
        valor => valor is int numero && numero > 0);
    public double AnchoMinimoTarjeta { get => (double)GetValue(AnchoMinimoTarjetaProperty); set => SetValue(AnchoMinimoTarjetaProperty, value); }
    public int MaximoColumnas { get => (int)GetValue(MaximoColumnasProperty); set => SetValue(MaximoColumnasProperty, value); }
    private int Columnas(double ancho) => Math.Max(1, Math.Min(Math.Min(MaximoColumnas, Math.Max(1, InternalChildren.Count)), (int)Math.Floor(ancho / AnchoMinimoTarjeta)));
    protected override Size MeasureOverride(Size disponible)
    {
        var ancho = double.IsFinite(disponible.Width) ? disponible.Width : AnchoMinimoTarjeta;
        var columnas = Columnas(ancho);
        var celda = ancho / columnas;
        double alto = 0, fila = 0;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var hijo = InternalChildren[i];
            hijo.Measure(new Size(celda, double.PositiveInfinity));
            fila = Math.Max(fila, hijo.DesiredSize.Height);
            if ((i + 1) % columnas == 0 || i == InternalChildren.Count - 1) { alto += fila; fila = 0; }
        }
        return new Size(ancho, alto);
    }
    protected override Size ArrangeOverride(Size disponible)
    {
        var columnas = Columnas(disponible.Width);
        var celda = disponible.Width / columnas;
        double y = 0;
        for (var inicio = 0; inicio < InternalChildren.Count; inicio += columnas)
        {
            var fin = Math.Min(inicio + columnas, InternalChildren.Count);
            var alto = Enumerable.Range(inicio, fin - inicio).Max(i => InternalChildren[i].DesiredSize.Height);
            for (var i = inicio; i < fin; i++) InternalChildren[i].Arrange(new Rect((i - inicio) * celda, y, celda, alto));
            y += alto;
        }
        return disponible;
    }
}
