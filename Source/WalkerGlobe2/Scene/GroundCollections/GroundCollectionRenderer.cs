#region License
//
// (C) Copyright 2010 Patrick Cozzi and Kevin Ring
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    public class GroundCollectionRenderer : IRenderable, IDisposable
    {
        public GroundCollectionRenderer(
            Vector2D[][] shapes,
            Context context,
            Ellipsoid globeShape,
            ShapefileAppearance appearance)
        {
            PolygonGroundShape polygonShape = new PolygonGroundShape(shapes, context, globeShape, appearance);
            polygonShape.DepthWrite = false;
            _groundCollectionGraphics = polygonShape;
        }


        #region IRenderable Members
        public void Render(Context context, SceneState sceneState)
        {
            _groundCollectionGraphics.Render(context, sceneState);
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            if (_groundCollectionGraphics != null)
            {
                _groundCollectionGraphics.Dispose();
            }
        }
        #endregion

        public bool Wireframe
        {
            get { return _groundCollectionGraphics.Wireframe; }
            set { _groundCollectionGraphics.Wireframe = value; }
        }

        private readonly GroundCollectionGraphics _groundCollectionGraphics;
    }
}