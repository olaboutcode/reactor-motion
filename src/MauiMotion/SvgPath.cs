namespace Reactor.Motion;

/// <summary>
/// One followable shape read from an SVG document.
/// <paramref name="Id"/> is the element's id, or null when that id is missing, empty, or whitespace.
/// <paramref name="Data"/> is the path data, with transforms baked in, ready for
/// <see cref="Motion.Path(string, double, double, bool)"/>.
/// </summary>
/// <param name="Id">The element's id, or null when the element has none.</param>
/// <param name="Data">The <c>d</c> string <see cref="Motion.Path(string, double, double, bool)"/> walks.</param>
public readonly record struct SvgPath(string? Id, string Data)
{
    /// <summary>
    /// Reads the followable shapes in <paramref name="markup"/>, in document order.
    /// Each element's <c>transform</c>, and the transforms of its parent groups, are baked into
    /// <see cref="Data"/>. Coordinates stay in user units.
    /// A missing or empty id is null, and no id is invented. Two elements with the same id both stay.
    /// The shapes are <c>path</c>, <c>line</c>, <c>polyline</c>, <c>polygon</c>, <c>rect</c>,
    /// <c>circle</c>, and <c>ellipse</c>. A shape that is not a <c>path</c> comes back as a <c>d</c> string.
    /// Invalid markup throws. A document with no followable shapes returns an empty list.
    /// </summary>
    public static IReadOnlyList<SvgPath> Parse(string markup)
        => SvgPathReader.Read(markup, into: null);

    /// <summary>
    /// Reads the followable shapes in <paramref name="markup"/> and stretches the root
    /// <c>viewBox</c> onto <paramref name="into"/>.
    /// <paramref name="into"/> is the rectangle where the SVG is drawn, in the same coordinates the view translates in.
    /// Without a positive root <c>viewBox</c>, coordinates stay in user units.
    /// The list, the ids, and the baked transforms match <see cref="Parse(string)"/>.
    /// </summary>
    public static IReadOnlyList<SvgPath> Parse(string markup, Rect into)
        => SvgPathReader.Read(markup, into);
}
