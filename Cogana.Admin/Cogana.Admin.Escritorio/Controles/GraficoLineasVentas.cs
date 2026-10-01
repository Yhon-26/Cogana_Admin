using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.Controles;

public sealed class GraficoLineasVentas : FrameworkElement
{
    public static readonly DependencyProperty DatosProperty = DependencyProperty.Register(
        nameof(Datos),
        typeof(IEnumerable<VentaDiariaReporte>),
        typeof(GraficoLineasVentas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, AlCambiarDatos));

    private static readonly Brush BrochaEje = CrearBrocha("#DDE1DE");
    private static readonly Brush BrochaTexto = CrearBrocha("#65716B");
    private static readonly Brush BrochaLinea = CrearBrocha("#4B7E70");
    private static readonly Brush BrochaArea = CrearBrocha("#284B7E70");
    private readonly CultureInfo _cultura = new("es-PE");
    private readonly ToolTip _detalle = new() { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
    private IReadOnlyList<VentaDiariaReporte> _datos = [];
    private Rect _areaGrafico;

    public IEnumerable<VentaDiariaReporte>? Datos
    {
        get => (IEnumerable<VentaDiariaReporte>?)GetValue(DatosProperty);
        set => SetValue(DatosProperty, value);
    }

    public void EstablecerDatos(IReadOnlyList<VentaDiariaReporte> datos) => Datos = datos;

    private static void AlCambiarDatos(DependencyObject objeto, DependencyPropertyChangedEventArgs e)
    {
        var grafico = (GraficoLineasVentas)objeto;
        grafico._datos = (e.NewValue as IEnumerable<VentaDiariaReporte>)?.ToList() ?? [];
        grafico.InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dibujo)
    {
        base.OnRender(dibujo);
        var ancho = ActualWidth;
        var alto = ActualHeight;
        if (ancho < 160 || alto < 120) return;

        _areaGrafico = new Rect(48, 18, Math.Max(40, ancho - 64), Math.Max(40, alto - 52));
        var maximo = Math.Max(1d, _datos.Count == 0 ? 0 : _datos.Max(item => item.VentasCentimos / 100d));
        maximo = RedondearMaximo(maximo);

        for (var i = 0; i <= 4; i++)
        {
            var y = _areaGrafico.Top + (_areaGrafico.Height * i / 4d);
            dibujo.DrawLine(new Pen(BrochaEje, 1), new Point(_areaGrafico.Left, y), new Point(_areaGrafico.Right, y));
            var valor = maximo * (4 - i) / 4d;
            DibujarTexto(dibujo, AbreviarMoneda(valor), 9, BrochaTexto, new Point(0, y - 7));
        }

        if (_datos.Count == 0 || _datos.All(item => item.VentasCentimos == 0))
        {
            dibujo.DrawLine(new Pen(BrochaLinea, 2),
                new Point(_areaGrafico.Left, _areaGrafico.Bottom),
                new Point(_areaGrafico.Right, _areaGrafico.Bottom));
            DibujarTextoCentrado(dibujo, "Aún no hay ventas en este periodo", 11, BrochaTexto,
                new Point(_areaGrafico.Left + _areaGrafico.Width / 2, _areaGrafico.Top + _areaGrafico.Height / 2));
            DibujarEtiquetasFechas(dibujo);
            return;
        }

        var puntos = _datos.Select((item, indice) => new Point(
            _areaGrafico.Left + (indice * _areaGrafico.Width / Math.Max(1, _datos.Count - 1)),
            _areaGrafico.Bottom - ((item.VentasCentimos / 100d) / maximo * _areaGrafico.Height))).ToList();

        var area = new StreamGeometry();
        using (var contexto = area.Open())
        {
            contexto.BeginFigure(new Point(puntos[0].X, _areaGrafico.Bottom), true, true);
            contexto.LineTo(puntos[0], true, false);
            contexto.PolyLineTo(puntos.Skip(1).ToList(), true, false);
            contexto.LineTo(new Point(puntos[^1].X, _areaGrafico.Bottom), true, false);
        }
        area.Freeze();
        dibujo.DrawGeometry(BrochaArea, null, area);

        var linea = new StreamGeometry();
        using (var contexto = linea.Open())
        {
            contexto.BeginFigure(puntos[0], false, false);
            contexto.PolyLineTo(puntos.Skip(1).ToList(), true, false);
        }
        linea.Freeze();
        dibujo.DrawGeometry(null, new Pen(BrochaLinea, 2.5), linea);

        foreach (var punto in puntos)
        {
            dibujo.DrawEllipse(Brushes.White, new Pen(BrochaLinea, 2), punto, 3.5, 3.5);
        }

        DibujarEtiquetasFechas(dibujo);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_datos.Count == 0 || !_areaGrafico.Contains(e.GetPosition(this)))
        {
            _detalle.IsOpen = false;
            return;
        }

        var posicion = e.GetPosition(this);
        var indice = _datos.Count == 1
            ? 0
            : (int)Math.Round((posicion.X - _areaGrafico.Left) / _areaGrafico.Width * (_datos.Count - 1));
        indice = Math.Clamp(indice, 0, _datos.Count - 1);
        var item = _datos[indice];
        _detalle.Content = $"{item.Fecha:dd/MM/yyyy}\n{item.Pedidos:N0} pedidos · {(item.VentasCentimos / 100m).ToString("C2", _cultura)}";
        _detalle.IsOpen = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _detalle.IsOpen = false;
        base.OnMouseLeave(e);
    }

    private void DibujarEtiquetasFechas(DrawingContext dibujo)
    {
        if (_datos.Count == 0) return;
        var indices = new[] { 0, (_datos.Count - 1) / 2, _datos.Count - 1 }.Distinct();
        foreach (var indice in indices)
        {
            var x = _datos.Count == 1
                ? _areaGrafico.Left + _areaGrafico.Width / 2
                : _areaGrafico.Left + indice * _areaGrafico.Width / (_datos.Count - 1);
            DibujarTextoCentrado(dibujo, _datos[indice].Fecha.ToString("dd MMM", _cultura), 9, BrochaTexto,
                new Point(x, _areaGrafico.Bottom + 16));
        }
    }

    private static double RedondearMaximo(double valor)
    {
        var potencia = Math.Pow(10, Math.Floor(Math.Log10(valor)));
        return Math.Ceiling(valor / potencia) * potencia;
    }

    private string AbreviarMoneda(double valor) => valor switch
    {
        >= 1_000_000 => $"S/ {valor / 1_000_000:0.#} M",
        >= 1_000 => $"S/ {valor / 1_000:0.#} mil",
        _ => $"S/ {valor:0}"
    };

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
