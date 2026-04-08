#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using System.Drawing;
using System.Collections.Generic;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    internal class PolygonGroundShape : GroundCollectionGraphics
    {
        
        public PolygonGroundShape(
    Vector2D[][] shapes,
    Context context,
    Ellipsoid globeShape,
    ShapefileAppearance appearance)
        {
            Verify.ThrowIfNull(context);
            Verify.ThrowIfNull(globeShape);

            _polyline = new OutlinedPolylineTexture();
            _polygons = new List<Polygon>();

            positionAttribute = new VertexAttributeDoubleVector3("position");
            colorAttribute = new VertexAttributeRGBA("color");
            outlineColorAttribute = new VertexAttributeRGBA("outlineColor");
            indices = new IndicesUnsignedInt();
            IList<Vector3D> positions = new List<Vector3D>();

            var color = Color.FromArgb(125, appearance.PolylineColor.R, appearance.PolylineColor.G, appearance.PolylineColor.B);
            var outLineColor = Color.Transparent;

            foreach (var polygonShape in shapes)
            {
                positions.Clear();

                for (int j = 0; j < polygonShape.Length; j++)
                {
                    Vector2D point = polygonShape[j];
                    positions.Add(globeShape.ToVector3D(Trig.ToRadians(new Geodetic3D(point.X, point.Y))));

                    //
                    // For polyline
                    //
                    positionAttribute.Values.Add(globeShape.ToVector3D(Trig.ToRadians(new Geodetic3D(point.X, point.Y))));
                    colorAttribute.AddColor(color);
                    outlineColorAttribute.AddColor(outLineColor);

                    if (j != 0)
                    {
                        indices.Values.Add((uint)positionAttribute.Values.Count - 2);
                        indices.Values.Add((uint)positionAttribute.Values.Count - 1);
                    }
                }

                /* Polygon p = new Polygon(context, globeShape, positions, 0f);
                p.Color = color;
                _polygons.Add(p);*/
            }

            Mesh mesh = new Mesh
            {
                PrimitiveType = OpenTK.Graphics.OpenGL.PrimitiveType.Lines
            };
            mesh.Attributes.Add(positionAttribute);
            mesh.Attributes.Add(colorAttribute);
            mesh.Attributes.Add(outlineColorAttribute);
            mesh.Indices = indices;
            _polyline.Set(context, mesh);
        }


        #region ShapefileGraphics Members

        public override void Render(Context context, SceneState sceneState)
        {
            _polyline.Render(context, sceneState);

            foreach (Polygon polygon in _polygons)
            {
                polygon.Render(context, sceneState);
            }
        }

        public override void Dispose()
        {
            if (_polyline != null)
            {
                _polyline.Dispose();
            }

            foreach (Polygon polygon in _polygons)
            {
                polygon.Dispose();
            }
        }

        public override bool Wireframe
        {
            get { return _polyline.Wireframe; }
            set
            {
                _polyline.Wireframe = value;

                foreach (Polygon polygon in _polygons)
                {
                    polygon.Wireframe = value;
                }
            }
        }

        #endregion

        /*
        public double Width 
        {
            get { return _polyline.Width;  }
            set { _polyline.Width = value;  }
        }
        
        public double OutlineWidth 
        {
            get { return _polyline.OutlineWidth; }
            set { _polyline.OutlineWidth = value; }
        }
        */

        public bool DepthWrite
        {
            get { return _polyline.DepthWrite; }
            set
            {
                _polyline.DepthWrite = value;

                foreach (Polygon polygon in _polygons)
                {
                    polygon.DepthWrite = value;
                }
            }
        }

        private readonly OutlinedPolylineTexture _polyline;
        private readonly IList<Polygon> _polygons;
        VertexAttributeDoubleVector3 positionAttribute;
        VertexAttributeRGBA colorAttribute;
        VertexAttributeRGBA outlineColorAttribute;
        IndicesUnsignedInt indices;
    }
}