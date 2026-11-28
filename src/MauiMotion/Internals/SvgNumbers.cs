using System.Globalization;

namespace Reactor.Motion.Internals;

/// <summary>SVG numbers, lengths, and separated lists. Parsing is invariant.</summary>
internal static class SvgNumbers
{
    public static double ParseLength(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var i = 0;
        Skip(text, ref i);
        if (!TryRead(text, ref i, out var value))
            throw new FormatException($"'{text.Trim()}' is not a length.");

        Skip(text, ref i);
        if (HasPx(text, i))
        {
            i += 2;
            Skip(text, ref i);
        }

        if (i != text.Length)
            throw new FormatException($"'{text.Trim()}' is not a length.");

        return value;
    }

    public static List<double> ReadList(string text)
    {
        var values = new List<double>();
        var i = 0;
        while (true)
        {
            Skip(text, ref i);
            if (i >= text.Length)
                return values;
            if (!TryRead(text, ref i, out var value))
                throw new FormatException("Expected a number.");
            values.Add(value);
        }
    }

    public static void Skip(string text, ref int i)
    {
        while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ','))
            i++;
    }

    public static bool TryRead(string text, ref int index, out double value)
    {
        var start = index;
        var i = index;
        if (i < text.Length && (text[i] == '+' || text[i] == '-'))
            i++;

        var digits = TakeDigits(text, ref i);
        if (i < text.Length && text[i] == '.')
        {
            i++;
            digits |= TakeDigits(text, ref i);
        }

        if (!digits || !TakeExponent(text, ref i))
        {
            value = 0;
            return false;
        }

        if (!double.TryParse(text.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || !double.IsFinite(value))
        {
            value = 0;
            return false;
        }

        index = i;
        return true;
    }

    static bool TakeDigits(string text, ref int i)
    {
        var found = false;
        while (i < text.Length && text[i] is >= '0' and <= '9')
        {
            found = true;
            i++;
        }

        return found;
    }

    static bool TakeExponent(string text, ref int i)
    {
        if (i >= text.Length || (text[i] != 'e' && text[i] != 'E'))
            return true;

        var exponent = i + 1;
        if (exponent < text.Length && (text[exponent] == '+' || text[exponent] == '-'))
            exponent++;
        if (!TakeDigits(text, ref exponent))
            return false;

        i = exponent;
        return true;
    }

    static bool HasPx(string text, int i)
        => i + 1 < text.Length && text[i] == 'p' && text[i + 1] == 'x';
}
