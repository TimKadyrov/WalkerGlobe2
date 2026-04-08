#region License
//
// (C) Copyright 2010 Patrick Cozzi, Deron Ohlarik, and Kevin Ring
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using OpenTK.Graphics.OpenGL;

namespace WalkerGlobe2.Renderer.GL3x
{
    internal sealed class FenceNameGL3x : IDisposable
    {
        public FenceNameGL3x()
        {
            _value = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, (WaitSyncFlags)0);
        }

        ~FenceNameGL3x()
        {
            FinalizerThreadContextGL3x.RunFinalizer(Dispose);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IntPtr Value
        {
            get { return _value.Value; }
        }

        private void Dispose(bool disposing)
        {
            if (_value != null)
            {
                GL.DeleteSync(_value.Value);
                _value = null;
            }
        }

        private Nullable<IntPtr> _value;
    }
}
