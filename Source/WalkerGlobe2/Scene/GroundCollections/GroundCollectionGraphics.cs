#region License
//
// (C) Copyright 2010 Patrick Cozzi and Kevin Ring
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    internal abstract class GroundCollectionGraphics : IRenderable, IDisposable
    {
        public abstract void Render(Context context, SceneState sceneState);
        public abstract void Dispose();

        public abstract bool Wireframe { get; set; }
    }
}