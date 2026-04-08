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
using System.Linq;

namespace WalkerGlobe2.Scene
{
    public sealed class Cone2 : IDisposable
    {
        public Cone2(Context context, Vector3D[] apexLocation, Vector3D[] baseToApexLength,  double[] height, double[] radius, int slices)
        {
            Verify.ThrowIfNull(context);
            //
            // Pipeline Stage 2:  Triangulate
            //
            IndicesUnsignedInt indices = new IndicesUnsignedInt();
            int lastInd = 0;
            var apexCnt = apexLocation.Count();
            var vertices = new List<Vector3D>();
            for (int cind = 0; cind < apexCnt; cind++)
            {
                var v = Cone.RenderCone2(baseToApexLength[cind], apexLocation[cind], height[cind], radius[cind], slices);

                for (int i = 0; i < v.Count - 2; i++)
                {
                    var tr = new TriangleIndicesUnsignedInt(lastInd, lastInd + i + 1, lastInd + i + 2);
                    indices.AddTriangle(tr);
                }
                // last triangle
                indices.AddTriangle(new TriangleIndicesUnsignedInt(lastInd, lastInd + v.Count - 1, lastInd + 1));
                lastInd += v.Count;
                vertices.AddRange(v);
            }
            result = new TriangleMeshSubdivisionResult(vertices, indices);
            
            positionsAttribute = new VertexAttributeDoubleVector3(
                "position", (result.Indices.Values.Count / 3) + 2);

            foreach (Vector3D position in result.Positions)
            {
                positionsAttribute.Values.Add(position);
            }

            Mesh mesh = new Mesh();
            mesh.PrimitiveType = OpenTK.Graphics.OpenGL.PrimitiveType.Triangles;
            mesh.FrontFaceWindingOrder = WindingOrder.Counterclockwise;
            mesh.Attributes.Add(positionsAttribute);
            mesh.Indices = indices;

            ShaderProgram sp = Device.CreateShaderProgram(
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.FillVS.glsl"),
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.FillFS.glsl"));
            _fillLogarithmicDepth = (Uniform<bool>)sp.Uniforms["u_logarithmicDepth"];
            _fillLogarithmicDepthConstant = (Uniform<float>)sp.Uniforms["u_logarithmicDepthConstant"];
            _fillColorUniform = (Uniform<Vector3F>)sp.Uniforms["u_color"];
            _fillAlphaUniform = (Uniform<float>)sp.Uniforms["u_alpha"];

            LogarithmicDepthConstant = 1;
            FillColor = Color.Gray;
            FillTranslucency = 0.5f;
            

            _drawState = new DrawState();
            _drawState.RenderState.Blending.Enabled = true;
            _drawState.RenderState.Blending.SourceRGBFactor = SourceBlendingFactor.SourceAlpha;
            _drawState.RenderState.Blending.SourceAlphaFactor = SourceBlendingFactor.SourceAlpha;
            _drawState.RenderState.Blending.DestinationRGBFactor = DestinationBlendingFactor.OneMinusSourceAlpha;
            _drawState.RenderState.Blending.DestinationAlphaFactor = DestinationBlendingFactor.OneMinusSourceAlpha;
            _drawState.RenderState.FacetCulling.Face = CullFace.Front;
            _drawState.ShaderProgram = sp;
            _meshBuffers = Device.CreateMeshBuffers(mesh, _drawState.ShaderProgram.VertexAttributes, BufferHint.StaticDraw);
            _primitiveType = mesh.PrimitiveType;
            // Important for translucency!
            DepthWrite = false;
        }

        private void Update(Context context)
        {
            if (_meshBuffers != null)
            {
                if (_drawState.VertexArray != null)
                {
                    _drawState.VertexArray.Dispose();
                    _drawState.VertexArray = null;
                }

                _drawState.VertexArray = context.CreateVertexArray(_meshBuffers);
                _meshBuffers = null;
            }
        }

        public void Render(Context context, SceneState sceneState)
        {
            Verify.ThrowIfNull(context);
            Verify.ThrowIfNull(sceneState);

            Update(context);
            context.Draw(_primitiveType, _drawState, sceneState);
        }

        public bool Wireframe
        {
            get { return _drawState.RenderState.RasterizationMode == RasterizationMode.Line; }
            set { _drawState.RenderState.RasterizationMode = value ? RasterizationMode.Line : RasterizationMode.Fill; }
        }

        public bool BackfaceCulling
        {
            get { return _drawState.RenderState.FacetCulling.Enabled; }
            set { _drawState.RenderState.FacetCulling.Enabled = value; }
        }

        public bool DepthWrite
        {
            get { return _drawState.RenderState.DepthMask; }
            set { _drawState.RenderState.DepthMask = value; }
        }

        public Color FillColor
        {
            get { return _fillColor; }

            set
            {
                _fillColor = value;
                _fillColorUniform.Value = new Vector3F(value.R / 255.0f, value.G / 255.0f, value.B / 255.0f);
            }
        }

        public float FillTranslucency
        {
            get { return _fillTranslucency; }

            set
            {
                _fillTranslucency = value;
                _fillAlphaUniform.Value = 1.0f - value;
            }
        }

        public bool LogarithmicDepth
        {
            get { return _fillLogarithmicDepth.Value; }
            set
            {
                _fillLogarithmicDepth.Value = value;
            }
        }

        public float LogarithmicDepthConstant
        {
            get { return _fillLogarithmicDepthConstant.Value; }
            set
            {
                _fillLogarithmicDepthConstant.Value = value;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _drawState.ShaderProgram.Dispose();
            _drawState.VertexArray?.Dispose();
        }

        #endregion

        private  DrawState _drawState;
        private OpenTK.Graphics.OpenGL.PrimitiveType _primitiveType;
        private MeshBuffers _meshBuffers;       // For passing between threads
        VertexAttributeDoubleVector3 positionsAttribute;
        TriangleMeshSubdivisionResult result;

        private readonly Uniform<bool> _fillLogarithmicDepth;
        private readonly Uniform<float> _fillLogarithmicDepthConstant;
        private readonly Uniform<Vector3F> _fillColorUniform;
        private Color _fillColor;
        private readonly Uniform<float> _fillAlphaUniform;
        private float _fillTranslucency;
    }
}