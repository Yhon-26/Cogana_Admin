using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Controles;

public sealed class GraficoDonaPedidos : FrameworkElement
{
    private static readonly Brush BrochaTexto = CrearBrocha("#65716B");
    private static readonly Brush BrochaVacia = CrearBrocha("#E8EBE8");
    private static readonly Brush[] Paleta =
    [
        CrearBrocha("#4B7E70"), CrearBrocha("#111412"), CrearBrocha("#D7A23B"),
        CrearBrocha("#D98178"), CrearBrocha("#7E91B8"), CrearBrocha("#A58A68")
    ];
    private readonly CultureInfo _cultura = new("es-PE");
    private IReadOnlyList<EstadoPedidoReporte> _datos = [];

    public void EstablecerDatos(IReadOnlyList<EstadoPedidoReporte> datos)
    {
        _datos = datos.Where(item => item.Pedidos > 0).OrderByDescending(item => item.Pedidos).ToList();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dibujo)
    {
        base.OnRender(dibujo);
        if (ActualWidth < 220 || ActualHeight < 150) return;

        var total = _datos.Sum(item => item.Pedidos);
        var centro = new Point(Math.Min(ActualWidth * .31, 105), ActualHeight / 2);
        var radio = Math.Min(64, Math.Min(centro.X - 12, ActualHeight / 2 - 16));
        var grosor = Math.Max(15, radio * .28);

        if (total == 0)
        {
            dibujo.DrawEllipse(null, new Pen(BrochaVacia, grosor), centro, radio, radio);
        }
        else if (_datos.Count == 1)
        {
            dibujo.DrawEllipse(null, new Pen(Paleta[0], grosor), centro, radio, radio);
        }
        else
        {
            var inicio = -90d;
            for (var i = 0; i < _datos.Count; i++)
            {
                var barrido = _datos[i].Pedidos * 360d / total;
                DibujarArco(dibujo, centro, radio, inicio, barrido, new Pen(Paleta[i % Paleta.Length], grosor));
                inicio += barrido;
            }
        }

        DibujarTextoCentrado(dibujo, total.ToString("N0", _cultura), 24, Brushes.Black, new Point(centro.X, centro.Y - 6));
        DibujarTextoCentrado(dibujo, "pedidos", 9, BrochaTexto, new Point(centro.X, centro.Y + 17));

        var xLeyenda = centro.X + radio + 34;
        if (total == 0)
        {
            DibujarTexto(dibujo, "Sin pedidos en el periodo", 10, BrochaTexto, new Point(xLeyenda, centro.Y - 7));
            return;
        }

        var visibles = _datos.Take(5).ToList();
        var y = centro.Y - (visibles.Count * 22d / 2);
        for (var i = 0; i < visibles.Count; i++)
        {
            var item = visibles[i];
            dibujo.DrawEllipse(Paleta[i % Paleta.Length], null, new Point(xLeyenda + 4, y + 7), 4, 4);
            DibujarTexto(dibujo, TraducirEstado(item.Estado), 10, Brushes.Black, new Point(xLeyenda + 15, y));
            DibujarTexto(dibujo, item.Pedidos.ToString("N0", _cultura), 10, BrochaTexto,
                new Point(Math.Max(xLeyenda + 130, ActualWidth - 40), y));
            y += 22;
        }
    }

    private static void DibujarArco(DrawingContext dibujo, Point centro, double radio, double inicio, double barrido, Pen lapiz)
    {
        const double radianes = Math.PI / 180d;
        var puntoInicio = new Point(centro.X + radio * Math.Cos(inicio * radianes), centro.Y + radio * Math.Sin(inicio * radianes));
        var fin = inicio + Math.Min(barrido, 359.99);
        var puntoFin = new Point(centro.X + radio * Math.Cos(fin * radianes), centro.Y + radio * Math.Sin(fin * radianes));
        var geometria = new StreamGeometry();
        using (var contexto = geometria.Open())
        {
            contexto.BeginFigure(puntoInicio, false, false);
            contexto.ArcTo(puntoFin, new Size(radio, radio), 0, barrido > 180, SweepDirection.Clockwise, true, false);
        }
        geometria.Freeze();
        dibujo.DrawGeometry(null, lapiz, geometria);
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

    private static string TraducirEstado(string estado) => estado switch
    {
        "draft" => "Borrador", "placed" => "Recibido", "confirmed" => "Confirmado",
        "preparing" => "En preparación", "ready" => "Listo", "out_for_delivery" => "En reparto",
        "completed" => "Completado", "cancelled" => "Cancelado", _ => estado
    };

    private static Brush CrearBrocha(string color)
    {
        var brocha = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brocha.Freeze();
        return brocha;
    }
}
