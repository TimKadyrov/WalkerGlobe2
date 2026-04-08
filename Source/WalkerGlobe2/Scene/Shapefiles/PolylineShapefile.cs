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
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    internal class PolylineShapefile : ShapefileGraphics
    {
        public PolylineShapefile(
            Shapefile shapefile, 
            Context context, 
            Ellipsoid globeShape, 
            ShapefileAppearance appearance)
        {
            Verify.ThrowIfNull(shapefile);
            Verify.ThrowIfNull(context);
            Verify.ThrowIfNull(globeShape);
            Verify.ThrowIfNull(appearance);
            
            _polyline = new OutlinedPolylineTexture();
            _appearance = appearance;

            int positionsCount = 0;
            int indicesCount = 0;
            PolylineCapacities(shapefile, out positionsCount, out indicesCount);

            positionAttribute = new VertexAttributeDoubleVector3("position", positionsCount);
            colorAttribute = new VertexAttributeRGBA("color", positionsCount);
            outlineColorAttribute = new VertexAttributeRGBA("outlineColor", positionsCount);
            indices = new IndicesUnsignedInt(indicesCount);
            rotanglez = new VertexAttributeFloat("rotanglez");

            foreach (Shape shape in shapefile)
            {
                if (shape.ShapeType != ShapeType.Polyline)
                {
                    throw new NotSupportedException("The type of an individual shape does not match the Shapefile type.");
                }

                PolylineShape polylineShape = (PolylineShape)shape;

                for (int j = 0; j < polylineShape.Count; ++j)
                {
                    ShapePart part = polylineShape[j];

                    for (int i = 0; i < part.Count; ++i)
                    {
                        Vector2D point = part[i];

                        positionAttribute.Values.Add(globeShape.ToVector3D(Trig.ToRadians(new Geodetic3D(point.X, point.Y))));
                        colorAttribute.AddColor(appearance.PolylineColor);
                        outlineColorAttribute.AddColor(appearance.PolylineOutlineColor);
                        rotanglez.Values.Add(0f);

                        if (i != 0)
                        {
                            indices.Values.Add((uint)positionAttribute.Values.Count - 2);
                            indices.Values.Add((uint)positionAttribute.Values.Count - 1);
                        }
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.PrimitiveType = OpenTK.Graphics.OpenGL.PrimitiveType.Lines;
            mesh.Attributes.Add(positionAttribute);
            mesh.Attributes.Add(colorAttribute);
            mesh.Attributes.Add(outlineColorAttribute);
            mesh.Attributes.Add(rotanglez);
            mesh.Indices = indices;
            _polyline.Set(context, mesh);
            _polyline.Width = appearance.PolylineWidth;
            _polyline.OutlineWidth = appearance.PolylineOutlineWidth;
        }

        private static void PolylineCapacities(Shapefile shapefile, out int positionsCount, out int indicesCount)
        {
            int numberOfPositions = 0;
            int numberOfIndices = 0;

            foreach (Shape shape in shapefile)
            {
                if (shape.ShapeType != ShapeType.Polyline)
                {
                    throw new NotSupportedException("The type of an individual shape does not match the Shapefile type.");
                }

                PolylineShape polylineShape = (PolylineShape)shape;

                for (int j = 0; j < polylineShape.Count; ++j)
                {
                    ShapePart part = polylineShape[j];

                    numberOfPositions += part.Count;
                    numberOfIndices += (part.Count - 1) * 2;
                }
            }

            positionsCount = numberOfPositions;
            indicesCount = numberOfIndices;
        }

        #region ShapefileGraphics Members

        public override void Render(Context context, SceneState sceneState)
        {
            if (sceneState.DataIsUpdating) return;
            _polyline.Render(context, sceneState);
        }

        public override void Dispose()
        {
            if (_polyline != null)
            {
                _polyline.Dispose();
            }
        }

        public override bool Wireframe
        {
            get { return _polyline.Wireframe; }
            set { _polyline.Wireframe = value; }
        }

        #endregion

        public bool DepthWrite 
        {
            get { return _polyline.DepthWrite;  }
            set { _polyline.DepthWrite = value;  }
        }
        
        private readonly OutlinedPolylineTexture _polyline;
        VertexAttributeFloat rotanglez;
        VertexAttributeDoubleVector3 positionAttribute;
        VertexAttributeRGBA colorAttribute;
        VertexAttributeRGBA outlineColorAttribute;
        ShapefileAppearance _appearance;
        IndicesUnsignedInt indices;
    }
}