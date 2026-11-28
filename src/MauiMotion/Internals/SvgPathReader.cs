using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Reactor.Motion.Internals;

/// <summary>
/// Reads followable SVG shapes in document order and bakes transforms into path data.
/// </summary>
internal static class SvgPathReader
{
    public static IReadOnlyList<SvgPath> Read(string markup, Rect? into)
    {
        ArgumentNullException.ThrowIfNull(markup);
        var document = XDocument.Parse(markup);
        var list = new List<SvgPath>();
        if (document.Root is null)
            return list;

        Collect(document.Root, list, Fit(document.Root, into));
        return list;
    }

    static void Collect(XElement element, List<SvgPath> list, Affine fit)
    {
        if (TryLocal(element, out var local))
        {
            var baked = SvgPathBake.Apply(local, TransformOf(element).Then(fit));
            list.Add(new SvgPath(ReadId(element), baked));
        }

        foreach (var child in element.Elements())
            Collect(child, list, fit);
    }

    static Affine TransformOf(XElement element)
    {
        var matrix = Affine.Identity;
        for (var node = element; node is not null; node = node.Parent)
            matrix = matrix.Then(SvgTransform.Parse(node.Attribute("transform")?.Value));

        return matrix;
    }

    static Affine Fit(XElement root, Rect? into)
    {
        var hasBox = TryViewBox(root, out var box);
        if (into is not Rect target || !hasBox)
            return Affine.Identity;

        var sx = target.Width / box.Width;
        var sy = target.Height / box.Height;
        return new Affine(sx, 0, 0, sy, target.X - box.X * sx, target.Y - box.Y * sy);
    }

    static bool TryViewBox(XElement root, out Rect box)
    {
        box = default;
        if (!string.Equals(root.Name.LocalName, "svg", StringComparison.Ordinal))
            return false;

        var text = root.Attribute("viewBox")?.Value;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var values = SvgNumbers.ReadList(text);
        if (values.Count != 4)
            throw new FormatException("A viewBox needs four numbers.");
        if (values[2] < 0 || values[3] < 0)
            throw new FormatException("A viewBox width and height must be non-negative.");
        if (values[2] == 0 || values[3] == 0)
            return false;

        box = new Rect(values[0], values[1], values[2], values[3]);
        return true;
    }

    static string? ReadId(XElement element)
    {
        var id = element.Attribute("id")?.Value;
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    static bool TryLocal(XElement element, out string local)
    {
        local = element.Name.LocalName switch
        {
            "path" => element.Attribute("d")?.Value ?? "",
            "line" => Line(element),
            "polyline" => Poly(element, close: false),
            "polygon" => Poly(element, close: true),
            "rect" => Rect(element),
            "circle" => Ellipse(element, circle: true),
            "ellipse" => Ellipse(element, circle: false),
            _ => "",
        };

        if (local.Length > 0)
            return true;

        local = "";
        return string.Equals(element.Name.LocalName, "path", StringComparison.Ordinal)
            && element.Attribute("d") is not null;
    }

    static string Line(XElement element)
    {
        var x1 = Length(element, "x1");
        var y1 = Length(element, "y1");
        var x2 = Length(element, "x2");
        var y2 = Length(element, "y2");
        return Form("M{0} {1} L{2} {3}", x1, y1, x2, y2);
    }

    static string Poly(XElement element, bool close)
    {
        var text = element.Attribute("points")?.Value;
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var values = SvgNumbers.ReadList(text);
        if (values.Count < 2 || values.Count % 2 != 0)
            throw new FormatException("A points list must have pairs.");

        var written = new StringBuilder(Form("M{0} {1}", values[0], values[1]));
        for (var i = 2; i < values.Count; i += 2)
            written.Append(Form(" L{0} {1}", values[i], values[i + 1]));
        if (close)
            written.Append(" Z");

        return written.ToString();
    }

    static string Rect(XElement element)
    {
        var x = Length(element, "x");
        var y = Length(element, "y");
        var width = RequireSize(element, "width");
        var height = RequireSize(element, "height");
        if (width == 0 || height == 0)
            return Form("M{0} {1}", x, y);

        var rx = Corner(element, "rx", "ry", width);
        var ry = Corner(element, "ry", "rx", height);
        if (rx <= 0 || ry <= 0)
            return Form("M{0} {1} H{2} V{3} H{4} Z", x, y, x + width, y + height, x);

        return Rounded(x, y, width, height, rx, ry);
    }

    static string Ellipse(XElement element, bool circle)
    {
        var cx = Length(element, "cx");
        var cy = Length(element, "cy");
        var rx = circle ? RequireRadius(element, "r") : RequireRadius(element, "rx");
        var ry = circle ? rx : RequireRadius(element, "ry");
        if (rx == 0 || ry == 0)
            return Form("M{0} {1}", cx, cy);

        var left = cx - rx;
        var right = cx + rx;
        return Form(
            "M{0} {1} A{2} {3} 0 1 1 {4} {5} A{2} {3} 0 1 1 {0} {1} Z",
            left, cy, rx, ry, right, cy);
    }

    static string Rounded(double x, double y, double width, double height, double rx, double ry)
        => Form(
            "M{0} {1} H{2} A{3} {4} 0 0 1 {5} {6} V{7} A{3} {4} 0 0 1 {8} {9} H{10} A{3} {4} 0 0 1 {11} {12} V{13} A{3} {4} 0 0 1 {0} {1} Z",
            x + rx,
            y,
            x + width - rx,
            rx,
            ry,
            x + width,
            y + ry,
            y + height - ry,
            x + width - rx,
            y + height,
            x + rx,
            x,
            y + height - ry,
            y + ry);

    static double Corner(XElement element, string name, string other, double limit)
    {
        var own = element.Attribute(name);
        var fallback = element.Attribute(other);
        if (own is null && fallback is null)
            return 0;

        var radius = SvgNumbers.ParseLength((own ?? fallback)!.Value);
        if (radius < 0)
            throw new FormatException($"A rect {name} must be non-negative.");

        return Math.Min(radius, limit / 2);
    }

    static double RequireSize(XElement element, string name)
    {
        var size = Length(element, name);
        if (size < 0)
            throw new FormatException($"A rect {name} must be non-negative.");

        return size;
    }

    static double RequireRadius(XElement element, string name)
    {
        var radius = Length(element, name);
        if (radius < 0)
            throw new FormatException($"A {element.Name.LocalName} {name} must be non-negative.");

        return radius;
    }

    static double Length(XElement element, string name)
        => SvgNumbers.ParseLength(element.Attribute(name)?.Value);

    static string Form(string format, params double[] numbers)
    {
        var printed = new object[numbers.Length];
        for (var i = 0; i < numbers.Length; i++)
            printed[i] = numbers[i].ToString("0.######", CultureInfo.InvariantCulture);

        return string.Format(CultureInfo.InvariantCulture, format, printed);
    }
}
