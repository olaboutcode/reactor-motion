namespace Reactor.Motion;

/// <summary>
/// App-wide playback preferences. A null <see cref="ReduceMotion"/> reads the platform.
/// </summary>
public static class MotionPreferences
{
    /// <summary>
    /// When true, playback writes the end pose and completes once.
    /// When null, the player reads the platform reduce-motion setting.
    /// A recipe can override this with <see cref="Motion.WithReduceMotion"/>.
    /// </summary>
    public static bool? ReduceMotion { get; set; }

    internal static bool IsReduceMotionEnabled()
        => ReduceMotion ?? PlatformReduceMotion();

    static bool PlatformReduceMotion()
    {
#if IOS || MACCATALYST
        return UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif ANDROID
        var resolver = Android.App.Application.Context?.ContentResolver;
        if (resolver is null)
            return false;
        return Android.Provider.Settings.Global.GetFloat(resolver, "animator_duration_scale", 1f) == 0f;
#elif WINDOWS
        return !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
#else
        return false;
#endif
    }
}
