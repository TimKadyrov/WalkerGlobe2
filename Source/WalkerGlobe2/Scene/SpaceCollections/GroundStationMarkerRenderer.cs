using System;
using System.Collections.Generic;
using System.Drawing;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    /// <summary>
    /// Renders 3D ground station markers (mast + dish + feed horn) at given ECEF positions.
    /// </summary>
    public class GroundStationMarkerRenderer : IRenderable, IDisposable
    {
        public GroundStationMarkerRenderer(
            Vector3D[] positions,
            Context context,
            float[] scales = null)
        {
            _context = context;

            ShaderProgram sp = Device.CreateShaderProgram(
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.FillVS.glsl"),
                EmbeddedResources.GetText("WalkerGlobe.Scene.Renderables.Plane.Shaders.FillFS.glsl"));
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

            Color = Color.DarkRed;
            _alphaUniform.Value = 0.5f;

            BuildMesh(positions, scales);
        }

        private void BuildMesh(Vector3D[] positions, float[] scales)
        {
            var verts = new List<Vector3D>();
            var indices = new IndicesUnsignedInt();

            for (int i = 0; i < positions.Length; i++)
            {
                float scale = (scales != null && i < scales.Length) ? scales[i] : 1.0f;
                int baseIdx = verts.Count;
                AddGroundStationGeometry(verts, indices, positions[i], scale, baseIdx);
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
        /// Ground station: cylindrical mast + funnel dish + cylindrical feed horn.
        /// Matches radians SceneBuilder.AddGroundStation proportions.
        /// </summary>
        private static void AddGroundStationGeometry(
            List<Vector3D> verts, IndicesUnsignedInt indices,
            Vector3D position, float scale, int baseIdx)
        {
            Vector3D up = position.Normalize();
            Vector3D hint = Math.Abs(up.Dot(Vector3D.UnitZ)) < 0.9 ? Vector3D.UnitZ : Vector3D.UnitX;
            Vector3D right = up.Cross(hint).Normalize();
            Vector3D fwd = right.Cross(up).Normalize();

            int idx = baseIdx;

            // Mast: thin cylinder rising from surface
            double mastHeight = 40.0e3 * scale;
            double mastRadius = 5.0e3 * scale;
            idx = AddCone(verts, indices, position, up, right, fwd, mastRadius, mastRadius, mastHeight, 8, idx);

            // Dish: funnel opening outward (narrow base, wide top)
            Vector3D dishBase = position + up * mastHeight;
            double dishBottomRadius = 6.0e3 * scale;
            double dishTopRadius = 50.0e3 * scale;
            double dishHeight = 25.0e3 * scale;
            idx = AddCone(verts, indices, dishBase, up, right, fwd, dishBottomRadius, dishTopRadius, dishHeight, 16, idx);

            // Feed horn: small cylinder at center of dish
            double feedRadius = 3.0e3 * scale;
            double feedHeight = 35.0e3 * scale;
            AddCone(verts, indices, dishBase, up, right, fwd, feedRadius, feedRadius, feedHeight, 8, idx);
        }

        /// <summary>
        /// Adds a cone/cylinder (with caps) along the 'up' axis.
        /// bottomRadius at base, topRadius at base+up*height. segments = number of sides.
        /// </summary>
        private static int AddCone(
            List<Vector3D> verts, IndicesUnsignedInt indices,
            Vector3D baseCenter, Vector3D up, Vector3D right, Vector3D fwd,
            double bottomRadius, double topRadius, double height, int segments, int baseIdx)
        {
            int b = baseIdx;
            Vector3D topCenter = baseCenter + up * height;

            // Generate ring vertices: bottom ring, then top ring
            for (int i = 0; i < segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                Vector3D dir = right * cos + fwd * sin;
                verts.Add(baseCenter + dir * bottomRadius);  // bottom ring vertex
            }
            for (int i = 0; i < segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                Vector3D dir = right * cos + fwd * sin;
                verts.Add(topCenter + dir * topRadius);      // top ring vertex
            }

            // Center vertices for caps
            int bottomCenterIdx = b + 2 * segments;
            verts.Add(baseCenter);
            int topCenterIdx = b + 2 * segments + 1;
            verts.Add(topCenter);

            // Side faces: quad strip between bottom and top rings
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int bi = b + i;
                int bn = b + next;
                int ti = b + segments + i;
                int tn = b + segments + next;
                indices.AddTriangle(new TriangleIndicesUnsignedInt(bi, bn, tn));
                indices.AddTriangle(new TriangleIndicesUnsignedInt(bi, tn, ti));
            }

            // Bottom cap (fan)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                indices.AddTriangle(new TriangleIndicesUnsignedInt(bottomCenterIdx, b + next, b + i));
            }

            // Top cap (fan)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                indices.AddTriangle(new TriangleIndicesUnsignedInt(topCenterIdx, b + segments + i, b + segments + next));
            }

            return b + 2 * segments + 2;
        }

        public void Render(Context context, SceneState sceneState)
        {
            if (_va == null && _mesh != null)
                _va = context.CreateVertexArray(_mesh);

            if (_hasMesh && _va != null)
            {
                _colorUniform.Value = new Vector3F(_color.R / 255f, _color.G / 255f, _color.B / 255f);
                _drawState.VertexArray = _va;
                context.Draw(OpenTK.Graphics.OpenGL.PrimitiveType.Triangles, _drawState, sceneState);
            }
        }

        public Color Color
        {
            get => _color;
            set => _color = value;
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
    }
}
