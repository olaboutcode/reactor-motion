using System.Runtime.CompilerServices;
using Reactor.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// Writes one <see cref="Matrix4"/> onto a view. iOS and Mac Catalyst assign the
/// 4×4 to the layer. Android sets camera distance from the perspective row and
/// writes translation, rotation, and scale as view properties. The pivot is the
/// view's <see cref="VisualElement.AnchorX"/> and <see cref="VisualElement.AnchorY"/>.
/// A matrix that matches the last write is skipped. A channel that matches is
/// left unset. <see cref="Forget"/> drops that memory so the next write runs.
/// </summary>
internal static class MatrixPlane
{
    sealed class Written
    {
        public bool Has;
        public double Scale;
        public double Tx;
        public double Ty;
        public double Rz;
        public double Rx;
        public double Ry;
        public double Sx;
        public double Sy;
        public readonly double[] Entries = new double[16];
    }

    static readonly ConditionalWeakTable<VisualElement, Written> Cache = new();

    public static void Forget(VisualElement view)
    {
        if (Cache.TryGetValue(view, out var written))
            written.Has = false;
    }

    public static void Apply(VisualElement view, Matrix4 matrix)
    {
        if (Same(view, matrix))
            return;

#if WINDOWS
        if (!ApplyWindows(view, matrix))
            return;
#else
        ApplyPose(view, matrix);
#endif
        Store(view, matrix);
    }

    static void ApplyPose(VisualElement view, Matrix4 matrix)
    {
        // Property setters make MAUI replace the native matrix. Defer collects
        // those changes and queues one reapply after the pose is written.
        var written = Cache.GetOrCreateValue(view);
        var fresh = !written.Has;
        PerspectivePlane.DeferReapply(view);
        try
        {
            PerspectivePlane.Apply(view, matrix);
            Write(view, written, MatrixPose.From(matrix), fresh);
        }
        finally
        {
            PerspectivePlane.EndDeferReapply();
        }
    }

    static void Write(VisualElement view, Written written, MatrixPose pose, bool fresh)
    {
        if (fresh || written.Scale != 1)
        {
            view.Scale = 1;
            written.Scale = 1;
        }

        if (fresh || written.Tx != pose.TranslationX)
        {
            view.TranslationX = pose.TranslationX;
            written.Tx = pose.TranslationX;
        }

        if (fresh || written.Ty != pose.TranslationY)
        {
            view.TranslationY = pose.TranslationY;
            written.Ty = pose.TranslationY;
        }

        if (fresh || written.Rz != pose.RotationZ)
        {
            view.Rotation = pose.RotationZ;
            written.Rz = pose.RotationZ;
        }

        if (fresh || written.Rx != pose.RotationX)
        {
            view.RotationX = pose.RotationX;
            written.Rx = pose.RotationX;
        }

        if (fresh || written.Ry != pose.RotationY)
        {
            view.RotationY = pose.RotationY;
            written.Ry = pose.RotationY;
        }

        if (fresh || written.Sx != pose.ScaleX)
        {
            view.ScaleX = pose.ScaleX;
            written.Sx = pose.ScaleX;
        }

        if (fresh || written.Sy != pose.ScaleY)
        {
            view.ScaleY = pose.ScaleY;
            written.Sy = pose.ScaleY;
        }
    }

    static bool Same(VisualElement view, Matrix4 matrix)
    {
        if (!Cache.TryGetValue(view, out var written) || !written.Has)
            return false;

        for (var column = 0; column < 4; column++)
        {
            for (var row = 0; row < 4; row++)
            {
                if (written.Entries[(column * 4) + row] != matrix.Entry(row, column))
                    return false;
            }
        }

        return true;
    }

    static void Store(VisualElement view, Matrix4 matrix)
    {
        var written = Cache.GetOrCreateValue(view);
        for (var column = 0; column < 4; column++)
        {
            for (var row = 0; row < 4; row++)
                written.Entries[(column * 4) + row] = matrix.Entry(row, column);
        }

        written.Has = true;
    }

#if WINDOWS
    static bool ApplyWindows(VisualElement view, Matrix4 matrix)
    {
        if (view.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement element)
            return false;

        var width = view.Frame.Width > 0 ? view.Frame.Width : view.Width;
        var height = view.Frame.Height > 0 ? view.Frame.Height : view.Height;
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new System.Numerics.Vector3(
            (float)(view.AnchorX * width),
            (float)(view.AnchorY * height),
            0);
        visual.TransformMatrix = new System.Numerics.Matrix4x4(
            (float)MatrixMaps.RowMajor(matrix, 0, 0),
            (float)MatrixMaps.RowMajor(matrix, 0, 1),
            (float)MatrixMaps.RowMajor(matrix, 0, 2),
            (float)MatrixMaps.RowMajor(matrix, 0, 3),
            (float)MatrixMaps.RowMajor(matrix, 1, 0),
            (float)MatrixMaps.RowMajor(matrix, 1, 1),
            (float)MatrixMaps.RowMajor(matrix, 1, 2),
            (float)MatrixMaps.RowMajor(matrix, 1, 3),
            (float)MatrixMaps.RowMajor(matrix, 2, 0),
            (float)MatrixMaps.RowMajor(matrix, 2, 1),
            (float)MatrixMaps.RowMajor(matrix, 2, 2),
            (float)MatrixMaps.RowMajor(matrix, 2, 3),
            (float)MatrixMaps.RowMajor(matrix, 3, 0),
            (float)MatrixMaps.RowMajor(matrix, 3, 1),
            (float)MatrixMaps.RowMajor(matrix, 3, 2),
            (float)MatrixMaps.RowMajor(matrix, 3, 3));
        return true;
    }
#endif
}
