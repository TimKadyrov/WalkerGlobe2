using System;
using System.Collections.Generic;
using System.Drawing;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    /// <summary>
    /// Renders 3D sphere markers at given positions.
    /// Used for selection highlights, point markers, etc.
    /// </summary>
    public class SphereMarkerRenderer : IRenderable, IDisposable
    {
        /// <param name="positions">Marker center positions</param>
        /// <param name="radii">Per-marker radius (null = all use defaultRadius)</param>
        /// <param name="defaultRadius">Radius when radii array is null</param>
        public SphereMarkerRenderer(
            Vector3D[] positions,
            Context context,
            double defaultRadius = 200.0,
            double[] radii = null)
        {
            _context = context;

            ShaderProgram sp = Device.CreateShaderProgram(
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.LitVS.glsl"),
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.LitFS.glsl"));
            _colorUniform = (Uniform<Vector3F>)sp.Uniforms["u_color"];
            _alphaUniform = (Uniform<float>)sp.Uniforms["u_alpha"];
            ((Uniform<bool>)sp.Uniforms["u_logarithmicDepth"]).Value = false;
            ((Uniform<float>)sp.Uniforms["u_logarithmicDepthConstant"]).Value = 1;

            _drawState = new DrawState();
            _drawState.ShaderProgram = sp;
            _drawState.RenderState.FacetCulling.Enabled = false;
            _drawState.RenderState.DepthMask = true;
            _drawState.RenderState.Blending.Enabled = true;
            _drawState.RenderState.Blending.SourceRGBFactor = SourceBlendingFactor.SourceAlpha;
            _drawState.RenderState.Blending.DestinationRGBFactor = DestinationBlendingFactor.OneMinusSourceAlpha;

            Color = Color.Cyan;
            Alpha = 0.7f;

            BuildMesh(positions, defaultRadius, radii);
        }

        private void BuildMesh(Vector3D[] positions, double defaultRadius, double[] radii)
        {
            var verts = new List<Vector3D>();
            var indices = new IndicesUnsignedInt();

            for (int i = 0; i < positions.Length; i++)
            {
                double r = (radii != null && i < radii.Length) ? radii[i] : defaultRadius;
                int baseIdx = verts.Count;
                AddSphere(verts, indices, positions[i], r, 12, baseIdx);
            }

            _mesh = CreateMeshBuffers(verts, indices);
            _hasMesh = verts.Count > 0;
        }

        private MeshBuffers CreateMeshBuffers(List<Vector3D> verts, IndicesUnsignedInt indices)
        {
            if (verts.Count == 0) return null;

            var posAttr = new VertexAttributeDoubleVector3("position", verts.Count);
            foreach (var v in verts)
                posAttr.Values.Add(v);

            var mesh = new Mesh();
            mesh.PrimitiveType = OpenTK.Graphics.OpenGL.PrimitiveType.Triangles;
            mesh.FrontFaceWindingOrder = WindingOrder.Counterclockwise;
            mesh.Attributes.Add(posAttr);
            mesh.Indices = indices;

            return Device.CreateMeshBuffers(mesh, _drawState.ShaderProgram.VertexAttributes, BufferHint.StaticDraw);
        }

        /// <summary>
        /// Adds a UV-sphere to the vertex/index lists.
        /// </summary>
        private static void AddSphere(
            List<Vector3D> verts, IndicesUnsignedInt indices,
            Vector3D center, double radius, int segments, int baseIdx)
        {
            int rings = segments;
            int sectors = segments * 2;

            // Generate vertices
            for (int r = 0; r <= rings; r++)
            {
                double phi = Math.PI * r / rings;
                double sinPhi = Math.Sin(phi);
                double cosPhi = Math.Cos(phi);

                for (int s = 0; s <= sectors; s++)
                {
                    double theta = 2.0 * Math.PI * s / sectors;
                    double x = sinPhi * Math.Cos(theta);
                    double y = sinPhi * Math.Sin(theta);
                    double z = cosPhi;
                    verts.Add(center + new Vector3D(x * radius, y * radius, z * radius));
                }
            }

            // Generate indices
            int cols = sectors + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sectors; s++)
                {
                    int i0 = baseIdx + r * cols + s;
                    int i1 = i0 + 1;
                    int i2 = i0 + cols;
                    int i3 = i2 + 1;

                    indices.AddTriangle(new TriangleIndicesUnsignedInt(i0, i2, i1));
                    indices.AddTriangle(new TriangleIndicesUnsignedInt(i1, i2, i3));
                }
            }
        }

        public void Render(Context context, SceneState sceneState)
        {
            if (_va == null && _mesh != null)
                _va = context.CreateVertexArray(_mesh);

            if (_hasMesh && _va != null)
            {
                _colorUniform.Value = new Vector3F(_color.R / 255f, _color.G / 255f, _color.B / 255f);
                _alphaUniform.Value = _alpha;
                _drawState.VertexArray = _va;
                context.Draw(OpenTK.Graphics.OpenGL.PrimitiveType.Triangles, _drawState, sceneState);
            }
        }

        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        public float Alpha
        {
            get => _alpha;
            set { _alpha = value; _alphaUniform.Value = value; }
        }

        public void Dispose()
        {
            _va?.Dispose();
            _drawState.ShaderProgram?.Dispose();
        }

        private readonly Context _context;
        private readonly DrawState _drawState;
        private readonly Uniform<Vector3F> _colorUniform;
        private readonly Uniform<float> _alphaUniform;

        private MeshBuffers _mesh;
        private VertexArray _va;
        private bool _hasMesh;
        private Color _color;
        private float _alpha;
    }
}
