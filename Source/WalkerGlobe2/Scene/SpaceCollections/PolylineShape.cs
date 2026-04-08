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
    public class PolylineShape2 : IRenderable, IDisposable
    {
        public PolylineShape2(
            List<Vector3D> points, 
            Context context, 
            Ellipsoid globeShape, 
            ShapefileAppearance appearance)
        {
            Verify.ThrowIfNull(context);
            Verify.ThrowIfNull(globeShape);
            Verify.ThrowIfNull(appearance);
            
            _polyline = new OutlinedPolylineTexture();
            _appearance = appearance;

            int positionsCount = points.Count;
            indices = new IndicesUnsignedInt(1);
            positionAttribute = new VertexAttributeDoubleVector3("position", positionsCount);
            colorAttribute = new VertexAttributeRGBA("color", positionsCount);
            outlineColorAttribute = new VertexAttributeRGBA("outlineColor", positionsCount);
            rotanglez = new VertexAttributeFloat("rotanglez");

            for (int i = 0; i < positionsCount; i++)
            {
                positionAttribute.Values.Add(points[i]);
                colorAttribute.AddColor(appearance.PolylineColor);
                outlineColorAttribute.AddColor(appearance.PolylineOutlineColor);
                rotanglez.Values.Add(0f);

                if (i != 0)
                {
                    indices.Values.Add((uint)positionAttribute.Values.Count - 2);
                    indices.Values.Add((uint)positionAttribute.Values.Count - 1);
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

        #region ShapefileGraphics Members

        public  void Render(Context context, SceneState sceneState)
        {
            if (sceneState.DataIsUpdating) return;
            _polyline.Render(context, sceneState);
        }

        public void Dispose()
        {
            if (_polyline != null)
            {
                _polyline.Dispose();
            }
        }

        public bool Wireframe
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