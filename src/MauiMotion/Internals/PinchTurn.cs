using System.Runtime.CompilerServices;

namespace Reactor.Motion.Internals;

/// <summary>
/// Reports the clockwise turn of a two-finger gesture, in degrees, since it began.
/// The scale recognizer stays the MAUI pinch. This listener only reads the angle.
/// </summary>
internal static class PinchTurn
{
#if IOS || MACCATALYST || ANDROID
    static readonly ConditionalWeakTable<VisualElement, TurnState> Entries = new();
#endif

    public static void Attach(VisualElement view, Action<double> onDegrees, Action<bool> onEnded)
    {
#if IOS || MACCATALYST || ANDROID
        Detach(view);
        var state = new TurnState(view, onDegrees, onEnded);
        Entries.Add(view, state);
        view.HandlerChanged += state.OnHandlerChanged;
        state.Hook();
#else
        _ = view;
        _ = onDegrees;
        _ = onEnded;
#endif
    }

    public static void Detach(VisualElement view)
    {
#if IOS || MACCATALYST || ANDROID
        if (!Entries.TryGetValue(view, out var state))
            return;
        view.HandlerChanged -= state.OnHandlerChanged;
        state.Unhook();
        Entries.Remove(view);
#else
        _ = view;
#endif
    }

#if IOS || MACCATALYST || ANDROID
    sealed class TurnState(VisualElement view, Action<double> onDegrees, Action<bool> onEnded)
    {
#if IOS || MACCATALYST
        UIKit.UIRotationGestureRecognizer? _rotation;
#elif ANDROID
        Android.Views.View? _platform;
        double _previous;
        double _total;
        bool _tracking;
#endif

        public void OnHandlerChanged(object? sender, EventArgs e)
            => Hook();

        public void Hook()
        {
            Unhook();
#if IOS || MACCATALYST
            if (view.Handler?.PlatformView is not UIKit.UIView platform)
                return;
            var rotation = new UIKit.UIRotationGestureRecognizer(OnRotate);
            rotation.ShouldRecognizeSimultaneously = static (_, _) => true;
            platform.AddGestureRecognizer(rotation);
            _rotation = rotation;
#elif ANDROID
            if (view.Handler?.PlatformView is not Android.Views.View platform)
                return;
            _platform = platform;
            platform.Touch += OnTouch;
#endif
        }

        public void Unhook()
        {
#if IOS || MACCATALYST
            if (_rotation is null)
                return;
            _rotation.View?.RemoveGestureRecognizer(_rotation);
            _rotation.Dispose();
            _rotation = null;
#elif ANDROID
            if (_platform is null)
                return;
            _platform.Touch -= OnTouch;
            _platform = null;
            _tracking = false;
            _total = 0;
#endif
        }

#if IOS || MACCATALYST
        void OnRotate(UIKit.UIRotationGestureRecognizer recognizer)
        {
            var state = recognizer.State;
            if (state is UIKit.UIGestureRecognizerState.Began or UIKit.UIGestureRecognizerState.Changed)
            {
                onDegrees(recognizer.Rotation * 180d / Math.PI);
                return;
            }

            if (state is UIKit.UIGestureRecognizerState.Cancelled or UIKit.UIGestureRecognizerState.Failed)
                onEnded(true);
            else if (state == UIKit.UIGestureRecognizerState.Ended)
                onEnded(false);
        }
#elif ANDROID
        void OnTouch(object? sender, Android.Views.View.TouchEventArgs args)
        {
            var motion = args.Event;
            if (motion is null)
                return;

            var action = motion.ActionMasked;
            if (action == Android.Views.MotionEventActions.Cancel)
            {
                _tracking = false;
                onEnded(true);
                return;
            }

            if (motion.PointerCount < 2)
            {
                if (!_tracking)
                    return;
                _tracking = false;
                onEnded(false);
                return;
            }

            var angle = Angle(motion);
            if (!_tracking)
            {
                _tracking = true;
                _previous = angle;
                _total = 0;
                onDegrees(0);
                return;
            }

            _total += Wrap(angle - _previous);
            _previous = angle;
            onDegrees(_total);
        }

        static double Angle(Android.Views.MotionEvent motion)
            => Math.Atan2(motion.GetY(1) - motion.GetY(0), motion.GetX(1) - motion.GetX(0)) * 180d / Math.PI;

        static double Wrap(double degrees)
        {
            while (degrees > 180)
                degrees -= 360;
            while (degrees < -180)
                degrees += 360;
            return degrees;
        }
#endif
    }
#endif
}
