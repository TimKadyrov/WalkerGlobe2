#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    public interface IRenderable
    {
        void Render(Context context, SceneState sceneState);
    }
}
