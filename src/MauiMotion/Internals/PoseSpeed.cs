namespace Reactor.Motion.Internals;

/// <summary>
/// Value-space speed of one property, taken from an in-flight spring before
/// that player is replaced. Units are property units per second.
/// </summary>
internal readonly record struct PoseSpeed(VisualElement View, BindableProperty Property, double ValuePerSecond);

/// <summary>Converts a spring's progress velocity into property units and back.</summary>
internal static class PoseMath
{
    public static bool TryDelta(object? from, object? to, out double delta)
    {
        switch (from, to)
        {
            case (double start, double end):
                delta = end - start;
                return true;
            case (float start, float end):
                delta = end - start;
                return true;
            default:
                delta = 0;
                return false;
        }
    }

    public static double ProgressVelocity(object? from, object? to, double valuePerSecond)
    {
        if (!TryDelta(from, to, out var delta) || Math.Abs(delta) < 1e-6)
            return 0;

        var velocity = valuePerSecond / delta;
        return double.IsNaN(velocity) || double.IsInfinity(velocity) ? 0 : velocity;
    }
}
