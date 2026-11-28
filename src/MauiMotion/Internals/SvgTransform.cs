namespace Reactor.Motion.Internals;

/// <summary>
/// Column-vector affine map. <c>x' = A*x + C*y + E</c> and <c>y' = B*x + D*y + F</c>.
/// <see cref="Then"/> applies this map first and the outer map after it.
/// </summary>
internal readonly record struct Affine(double A, double B, double C, double D, double E, double F)
{
    public static Affine Identity { get; } = new(1, 0, 0, 1, 0, 0);

    public bool IsIdentity
        => Nearly(A, 1) && Nearly(D, 1) && Nearly(B, 0) && Nearly(C, 0) && Nearly(E, 0) && Nearly(F, 0);

    public Affine Then(Affine outer)
        => new(
            outer.A * A + outer.C * B,
            outer.B * A + outer.D * B,
            outer.A * C + outer.C * D,
            outer.B * C + outer.D * D,
            outer.A * E + outer.C * F + outer.E,
            outer.B * E + outer.D * F + outer.F);

    public Point Map(Point point)
        => new(A * point.X + C * point.Y + E, B * point.X + D * point.Y + F);

    static bool Nearly(double value, double target)
        => Math.Abs(value - target) <= 1e-12;
}

/// <summary>SVG transform lists. The leftmost function is the outermost.</summary>
internal static class SvgTransform
{
    public static Affine Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Affine.Identity;

        var matrix = Affine.Identity;
        var i = 0;
        while (i < text.Length)
        {
            SvgNumbers.Skip(text, ref i);
            if (i >= text.Length)
                break;

            var name = ReadName(text, ref i);
            SvgNumbers.Skip(text, ref i);
            if (i >= text.Length || text[i] != '(')
                throw new FormatException($"The transform '{name}' needs parentheses.");

            i++;
            var args = ReadArgs(text, ref i);
            matrix = Function(name, args).Then(matrix);
        }

        return matrix;
    }

    static Affine Function(string name, List<double> args)
        => name switch
        {
            "matrix" when args.Count == 6 => new Affine(args[0], args[1], args[2], args[3], args[4], args[5]),
            "translate" when args.Count is 1 or 2 => Translate(args[0], args.Count == 2 ? args[1] : 0),
            "scale" when args.Count is 1 or 2 => new Affine(args[0], 0, 0, args.Count == 2 ? args[1] : args[0], 0, 0),
            "rotate" when args.Count is 1 or 3 => Rotate(args[0], args.Count == 3 ? args[1] : 0, args.Count == 3 ? args[2] : 0),
            "skewX" when args.Count == 1 => SkewX(args[0]),
            "skewY" when args.Count == 1 => SkewY(args[0]),
            _ => throw new FormatException($"The transform '{name}' is not valid."),
        };

    static Affine Translate(double x, double y)
        => new(1, 0, 0, 1, x, y);

    static Affine Rotate(double degrees, double cx, double cy)
    {
        var radians = degrees * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var spin = new Affine(cos, sin, -sin, cos, 0, 0);
        if (cx == 0 && cy == 0)
            return spin;

        return Translate(-cx, -cy).Then(spin).Then(Translate(cx, cy));
    }

    static Affine SkewX(double degrees)
    {
        var tilt = Math.Tan(degrees * Math.PI / 180);
        return new Affine(1, 0, tilt, 1, 0, 0);
    }

    static Affine SkewY(double degrees)
    {
        var tilt = Math.Tan(degrees * Math.PI / 180);
        return new Affine(1, tilt, 0, 1, 0, 0);
    }

    static string ReadName(string text, ref int i)
    {
        var start = i;
        while (i < text.Length && char.IsAsciiLetter(text[i]))
            i++;
        if (i == start)
            throw new FormatException("A transform function name is missing.");

        return text[start..i];
    }

    static List<double> ReadArgs(string text, ref int i)
    {
        var args = new List<double>();
        while (true)
        {
            SkipArg(text, ref i, args.Count == 0);
            if (i >= text.Length)
                throw new FormatException("A transform function is missing ')'.");
            if (text[i] == ')')
            {
                i++;
                return args;
            }

            if (!SvgNumbers.TryRead(text, ref i, out var value))
                throw new FormatException("A transform argument is not a number.");
            args.Add(value);
        }
    }

    static void SkipArg(string text, ref int i, bool first)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
            i++;
        if (i >= text.Length || text[i] != ',')
            return;
        if (first)
            throw new FormatException("A transform argument is missing.");

        i++;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
            i++;
    }
}
