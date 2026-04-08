#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
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
    internal class PointShapefile : ShapefileGraphics
    {
        public PointShapefile(
            Shapefile shapefile, 
            Context context, 
            Ellipsoid globeShape,
            ShapefileAppearance appearance)
        {
            Verify.ThrowIfNull(shapefile);
            Verify.ThrowIfNull(context);
            Verify.ThrowIfNull(globeShape);
            Verify.ThrowIfNull(appearance);
            _appearance = appearance;
            _billboards = new BillboardCollection(context, true);
            _billboards.Texture = Device.CreateTexture2D(appearance.Bitmap, TextureFormat.RedGreenBlueAlpha8, false);
            _points = new List<Vector3D>();

            foreach (Shape shape in shapefile)
            {
                if (shape.ShapeType != ShapeType.Point)
                {
                    throw new NotSupportedException("The type of an individual shape does not match the Shapefile type.");
                }

                Vector2D point = ((PointShape)shape).Position;
                var position = globeShape.ToVector3D(Trig.ToRadians(new Geodetic3D(point.X, point.Y)));
                _points.Add(position);
                Billboard billboard = new Billboard();
                billboard.Position = position;
                _billboards.Add(billboard);
            }
        }

        #region ShapefileGraphics Members
        
        public override void Render(Context context, SceneState sceneState)
        {
            _billboards.Render(context, sceneState);
        }

        public override void Dispose()
        {
            if (_billboards != null)
            {
                _billboards.Dispose();
            }
        }

        public override bool Wireframe
        {
            get { return _billboards.Wireframe; }
            set { _billboards.Wireframe = value; }
        }

        #endregion

        public bool DepthWrite 
        {
            get { return _billboards.DepthWrite; }
            set { _billboards.DepthWrite = value; }
        }

        private BillboardCollection _billboards;
        private readonly ShapefileAppearance _appearance;

        private readonly List<Vector3D> _points;
    }
}