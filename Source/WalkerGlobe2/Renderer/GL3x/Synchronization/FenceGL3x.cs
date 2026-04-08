#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using WalkerGlobe2.Renderer;
using OpenTK.Graphics.OpenGL;

namespace WalkerGlobe2.Renderer.GL3x
{
    public class FenceGL3x : Fence
    {
        public FenceGL3x()
        {
            _name = new FenceNameGL3x();
        }

        public override void ServerWait()
        {
            GL.WaitSync(_name.Value, (WaitSyncFlags)0, (long)All.TimeoutIgnored);
        }

        public override ClientWaitResult ClientWait()
        {
            return ClientWait((int)All.TimeoutIgnored);
        }

        public override ClientWaitResult ClientWait(int timeoutInNanoseconds)
        {
            if ((timeoutInNanoseconds < 0) && (timeoutInNanoseconds != (int)All.TimeoutIgnored))
            {
                throw new ArgumentOutOfRangeException("timeoutInNanoseconds");
            }

            WaitSyncStatus result = GL.ClientWaitSync(_name.Value, (ClientWaitSyncFlags)0, timeoutInNanoseconds);

            switch (result)
            {
                case WaitSyncStatus.AlreadySignaled:
                    return ClientWaitResult.AlreadySignaled;
                case WaitSyncStatus.ConditionSatisfied:
                    return ClientWaitResult.Signaled;
                case WaitSyncStatus.TimeoutExpired:
                    return ClientWaitResult.TimeoutExpired;
            }

            return ClientWaitResult.TimeoutExpired;     // ArbSync.WaitFailed
        }

        public override SynchronizationStatus Status()
        {
            int length;
            int status;

            GL.GetSync(_name.Value, SyncParameterName.SyncStatus, 1, out length, out status);

            if (status == (int)All.Unsignaled)
            {
                return SynchronizationStatus.Unsignaled;
            }
            else
            {
                return SynchronizationStatus.Signaled;
            }
        }

        #region Disposable Members

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _name.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion

        private FenceNameGL3x _name;
    }
}
