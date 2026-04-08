#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using OpenTK.Graphics.OpenGL;

namespace WalkerGlobe2.Renderer.GL3x
{
    internal interface ICleanableObserver
    {
        void NotifyDirty(ICleanable value);
    }
}
