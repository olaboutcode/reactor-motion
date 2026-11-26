using Reactor.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// One semi-implicit Euler step of a mass-spring-damper.
/// <see cref="Integrate"/> settles within 0.002 of a target, or at 8 seconds.
/// <see cref="Decay"/> uses damping as friction and ignores stiffness.
/// </summary>
internal static class SpringStep
{
    public static void Integrate(
        Spring spring,
        double deltaMs,
        double target,
        ref double x,
        ref double v,
        ref double elapsedMs,
        out bool settled)
    {
        if (double.IsPositiveInfinity(deltaMs))
        {
            x = target;
            v = 0;
            settled = true;
            return;
        }

        var dt = Math.Min(Math.Max(deltaMs, 0) / 1000d, 1d / 30d);
        var mass = Math.Max(spring.Mass, 1e-6);
        var stiffness = Math.Max(spring.Stiffness, 1e-6);
        var damping = Math.Max(spring.Damping, 0);
        var accel = ((-stiffness * (x - target)) - (damping * v)) / mass;
        v += accel * dt;
        x += v * dt;
        elapsedMs += Math.Max(deltaMs, 0);
        settled = (Math.Abs(x - target) < 0.002 && Math.Abs(v) < 0.002) || elapsedMs > 8000;
        if (!settled)
            return;

        x = target;
        v = 0;
    }

    public static void Decay(
        Spring spring,
        double deltaMs,
        ref double x,
        ref double v,
        ref double elapsedMs,
        out bool settled)
    {
        var mass = Math.Max(spring.Mass, 1e-6);
        var damping = Math.Max(spring.Damping, 0);
        if (double.IsPositiveInfinity(deltaMs))
        {
            if (damping > 0)
                x += v * mass / damping;
            v = 0;
            settled = true;
            return;
        }

        var dt = Math.Min(Math.Max(deltaMs, 0) / 1000d, 1d / 30d);
        var accel = -(damping * v) / mass;
        v += accel * dt;
        x += v * dt;
        elapsedMs += Math.Max(deltaMs, 0);
        settled = Math.Abs(v) < 0.002 || elapsedMs > 8000;
        if (settled)
            v = 0;
    }
}
