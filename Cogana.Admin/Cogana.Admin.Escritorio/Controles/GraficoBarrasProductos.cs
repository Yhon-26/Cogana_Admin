using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Controles;

public sealed class GraficoBarrasProductos : FrameworkElement
{
    private static readonly Brush BrochaTexto = CrearBrocha("#65716B");
    private static readonly Brush BrochaFondo = CrearBrocha("#EDF0ED");
    private static readonly Brush BrochaBarra = CrearBrocha("#111412");
    private readonly CultureInfo _cultura = new("es-PE");
    private IReadOnlyList<ProductoVendidoReporte> _datos = [];

    public void EstablecerDatos(IReadOnlyList<ProductoVendidoReporte> datos)
    {
        _datos = datos.OrderByDescending(item => item.VentasCentimos).Take(5).ToList();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dibujo)
    {
        base.OnRender(dibujo);
        if (ActualWidth < 220 || ActualHeight < 140) return;
        if (_datos.Count == 0)
        {
            DibujarTextoCentrado(dibujo, "Aún no hay productos vendidos en este periodo", 11, BrochaTexto,
                new Point(ActualWidth / 2, ActualHeight / 2));
            return;
        }

        var maximo = Math.Max(1, _datos.Max(item => item.VentasCentimos));
        var altoFila = Math.Min(52, (ActualHeight - 8) / _datos.Count);
        var anchoEtiqueta = Math.Min(180, ActualWidth * .34);
        var anchoValor = 88d;
        var anchoBarra = Math.Max(40, ActualWidth - anchoEtiqueta - anchoValor - 22);

        for (var i = 0; i < _datos.Count; i++)
        {
            var item = _datos[i];
            var y = 8 + i * altoFila;
            var nombre = item.Producto.Length > 24 ? $"{item.Producto[..22]}…" : item.Producto;
            DibujarTexto(dibujo, nombre, 10, Brushes.Black, new Point(0, y + 3));

            var rectanguloFondo = new Rect(anchoEtiqueta, y + 5, anchoBarra, 9);
            dibujo.DrawRoundedRectangle(BrochaFondo, null, rectanguloFondo, 5, 5);
            var proporcion = item.VentasCentimos / (double)maximo;
            dibujo.DrawRoundedRectangle(BrochaBarra, null,
                new Rect(anchoEtiqueta, y + 5, Math.Max(4, anchoBarra * proporcion), 9), 5, 5);

            var ventas = (item.VentasCentimos / 100m).ToString("C2", _cultura);
            DibujarTexto(dibujo, ventas, 10, BrochaTexto, new Point(anchoEtiqueta + anchoBarra + 12, y + 1));
            DibujarTexto(dibujo, $"{item.Cantidad:N0} vendidos", 8, BrochaTexto, new Point(0, y + 20));
        }
    }

    private void DibujarTexto(DrawingContext dibujo, string texto, double tamano, Brush brocha, Point punto)
    {
        dibujo.DrawText(new FormattedText(texto, _cultura, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), tamano, brocha, VisualTreeHelper.GetDpi(this).PixelsPerDip), punto);
    }

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
