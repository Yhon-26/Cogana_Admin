using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Controles;

public sealed class GraficoBarrasStock : FrameworkElement
{
    private static readonly Brush BrochaTexto = CrearBrocha("#65716B");
    private static readonly Brush BrochaFondo = CrearBrocha("#F2E4D0");
    private static readonly Brush BrochaCritica = CrearBrocha("#D98178");
    private static readonly Brush BrochaAlerta = CrearBrocha("#D7A23B");
    private readonly CultureInfo _cultura = new("es-PE");
    private IReadOnlyList<StockBajoReporte> _datos = [];

    public void EstablecerDatos(IReadOnlyList<StockBajoReporte> datos)
    {
        _datos = datos
            .OrderBy(item => item.StockMinimo <= 0 ? 1 : item.StockActual / (double)item.StockMinimo)
            .Take(6)
            .ToList();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dibujo)
    {
        base.OnRender(dibujo);
        if (ActualWidth < 260 || ActualHeight < 150) return;
        if (_datos.Count == 0)
        {
            DibujarTextoCentrado(dibujo, "El inventario no presenta alertas de stock", 11, BrochaTexto,
                new Point(ActualWidth / 2, ActualHeight / 2));
            return;
        }

        var altoFila = Math.Min(38, (ActualHeight - 6) / _datos.Count);
        var anchoEtiqueta = Math.Min(205, ActualWidth * .36);
        var anchoValor = 126d;
        var anchoBarra = Math.Max(50, ActualWidth - anchoEtiqueta - anchoValor - 26);

        for (var i = 0; i < _datos.Count; i++)
        {
            var item = _datos[i];
            var y = 5 + i * altoFila;
            var nombre = item.Producto.Length > 28 ? $"{item.Producto[..26]}…" : item.Producto;
            DibujarTexto(dibujo, nombre, 10, Brushes.Black, new Point(0, y));

            var proporcion = item.StockMinimo <= 0 ? 0 : Math.Clamp(item.StockActual / (double)item.StockMinimo, 0, 1);
            var color = proporcion <= .5 ? BrochaCritica : BrochaAlerta;
            dibujo.DrawRoundedRectangle(BrochaFondo, null, new Rect(anchoEtiqueta, y + 3, anchoBarra, 10), 5, 5);
            dibujo.DrawRoundedRectangle(color, null,
                new Rect(anchoEtiqueta, y + 3, Math.Max(4, anchoBarra * proporcion), 10), 5, 5);
            DibujarTexto(dibujo,
                $"{item.StockActual.ToString("N0", _cultura)} / {item.StockMinimo.ToString("N0", _cultura)} {item.Unidad}",
                9, BrochaTexto, new Point(anchoEtiqueta + anchoBarra + 12, y));
        }
    }

    private void DibujarTexto(DrawingContext dibujo, string texto, double tamano, Brush brocha, Point punto) =>
        dibujo.DrawText(new FormattedText(texto, _cultura, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), tamano, brocha, VisualTreeHelper.GetDpi(this).PixelsPerDip), punto);

    private void DibujarTextoCentrado(DrawingContext dibujo, string texto, double tamano, Brush brocha, Point centro)
    {
        var formato = new FormattedText(texto, _cultura, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), tamano, brocha, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dibujo.DrawText(formato, new Point(centro.X - formato.Width / 2, centro.Y - formato.Height / 2));
    }

    private static Brush CrearBrocha(string color)
    {
        var brocha = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brocha.Freeze();
        return brocha;
    }
}
