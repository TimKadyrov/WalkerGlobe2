using System;

namespace WalkerGlobe2.Renderer.GL3x
{
    // In OpenTK 4.x, shared GL contexts via GLFW are complex.
    // For now, finalizer cleanup is a best-effort no-op.
    internal static class FinalizerThreadContextGL3x
    {
        public static void Initialize() { }

        public delegate void DisposeCallback(bool disposing);

        public static void RunFinalizer(DisposeCallback callback)
        {
            // GL resource cleanup on finalizer thread is not supported
            // in OpenTK 4.x without explicit GLFW context sharing.
            // Resources will be cleaned up when the main context is destroyed.
        }
    }
}
