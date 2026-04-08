#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;


namespace WalkerGlobe2.Scene
{
    public sealed class Polyline : IDisposable
    {
        public Polyline()
        {
            _drawState = new DrawState();
            _drawState.RenderState.FacetCulling.Enabled = false;

            Show = true;
            Width = 1;
        }

        public void Set(Context context, Mesh mesh)
        {
            Verify.ThrowIfNull(context);

            if (mesh == null)
            {
                throw new ArgumentNullException("mesh");
            }

            if (mesh.PrimitiveType != OpenTK.Graphics.OpenGL.PrimitiveType.Lines &&
                mesh.PrimitiveType != OpenTK.Graphics.OpenGL.PrimitiveType.LineLoop &&
                mesh.PrimitiveType != OpenTK.Graphics.OpenGL.PrimitiveType.LineStrip)
            {
                throw new ArgumentException("mesh.PrimitiveType must be Lines, LineLoop, or LineStrip.", "mesh");
            }

            if (!mesh.Attributes.Contains("position") &&
                !mesh.Attributes.Contains("color") &&
                !mesh.Attributes.Contains("rotanglez"))
            {
                throw new ArgumentException("mesh.Attributes should contain attributes named \"position\" and \"color\".", "mesh");
            }

            if (_drawState.ShaderProgram == null)
            {
                _drawState.ShaderProgram = Device.CreateShaderProgram(
                    EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Polyline.Polyline.PolylineVS.glsl"),
                    EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Polyline.Polyline.PolylineGS.glsl"),
                    EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Polyline.Polyline.PolylineFS.glsl"));
                _fillDistance = (Uniform<float>)_drawState.ShaderProgram.Uniforms["u_fillDistance"];
            }
            
            ///////////////////////////////////////////////////////////////////
            _drawState.VertexArray = context.CreateVertexArray(mesh, _drawState.ShaderProgram.VertexAttributes, BufferHint.StaticDraw);
            _primitiveType = mesh.PrimitiveType;
        }

        public void Render(Context context, SceneState sceneState)
        {
            Verify.ThrowIfNull(context);
            Verify.ThrowIfNull(sceneState);

            if (Show)
            {
                if (_drawState.ShaderProgram != null)
                {
                    _fillDistance.Value = (float)(Width * 0.5 * sceneState.HighResolutionSnapScale);

                    context.Draw(_primitiveType, _drawState, sceneState);
                }
            }
        }

        public bool Show { get; set; }
        public double Width { get; set; }

        public bool Wireframe
        {
            get { return _drawState.RenderState.RasterizationMode == RasterizationMode.Line; }
            set { _drawState.RenderState.RasterizationMode = value ? RasterizationMode.Line : RasterizationMode.Fill; }
        }

        public bool DepthTestEnabled
        {
            get { return _drawState.RenderState.DepthTest.Enabled; }
            set { _drawState.RenderState.DepthTest.Enabled = value; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_drawState.ShaderProgram != null)
            {
                _drawState.ShaderProgram.Dispose();
            }

            if (_drawState.VertexArray != null)
            {
                _drawState.VertexArray.Dispose();
            }
        }

        #endregion

        private Uniform<float> _fillDistance;
        private readonly DrawState _drawState;
        private OpenTK.Graphics.OpenGL.PrimitiveType _primitiveType;
    }
}