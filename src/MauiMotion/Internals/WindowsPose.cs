#if WINDOWS
using System.Runtime.CompilerServices;

namespace Reactor.Motion.Internals;

/// <summary>
/// One pose matrix for a view whose recipe set perspective.
/// Bindable plane channels stay unchanged so MAUI does not apply them again.
/// </summary>
internal static class WindowsPose
{
    sealed class State
    {
        public double Tx;
        public double Ty;
        public double Rz;
        public double Rx;
        public double Ry;
        public double Sx = 1;
        public double Sy = 1;
        public bool Seeded;
    }

    static readonly ConditionalWeakTable<VisualElement, State> States = new();

    public static void Write(VisualElement view, BindableProperty property, object value, double entry)
    {
        var state = States.GetOrCreateValue(view);
        if (!state.Seeded)
            Seed(view, state);

        var number = value is double d ? d : value is float f ? f : 0;
        if (property == VisualElement.TranslationXProperty)
            state.Tx = number;
        else if (property == VisualElement.TranslationYProperty)
            state.Ty = number;
        else if (property == VisualElement.RotationProperty)
            state.Rz = number;
        else if (property == VisualElement.RotationXProperty)
            state.Rx = number;
        else if (property == VisualElement.RotationYProperty)
            state.Ry = number;
        else if (property == VisualElement.ScaleXProperty)
            state.Sx = number * view.Scale;
        else if (property == VisualElement.ScaleYProperty)
            state.Sy = number * view.Scale;
        else if (property == VisualElement.ScaleProperty)
        {
            state.Sx = view.ScaleX * number;
            state.Sy = view.ScaleY * number;
        }

        MatrixPlane.Apply(
            view,
            PerspectivePlane.PoseMatrix(state.Tx, state.Ty, state.Rx, state.Ry, state.Rz, state.Sx, state.Sy, entry));
    }

    static void Seed(VisualElement view, State state)
    {
        state.Tx = view.TranslationX;
        state.Ty = view.TranslationY;
        state.Rz = view.Rotation;
        state.Rx = view.RotationX;
        state.Ry = view.RotationY;
        state.Sx = view.ScaleX * view.Scale;
        state.Sy = view.ScaleY * view.Scale;
        state.Seeded = true;
    }
}
#endif
