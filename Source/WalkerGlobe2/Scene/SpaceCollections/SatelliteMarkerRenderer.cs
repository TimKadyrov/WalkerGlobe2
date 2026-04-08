using System;
using System.Collections.Generic;
using System.Drawing;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    /// <summary>
    /// Renders 3D satellite markers (body + solar panels) at given positions.
    /// Supports per-satellite scale. Highlighted satellites render in a separate color.
    /// </summary>
    public class SatelliteMarkerRenderer : IRenderable, IDisposable
    {
        /// <param name="positions">ECI satellite positions</param>
        /// <param name="scales">Per-satellite scale factor (null = all 1.0)</param>
        /// <param name="highlightMask">Per-satellite highlight flag (null = none highlighted)</param>
        public SatelliteMarkerRenderer(
            Vector3D[] positions,
            Context context,
            float[] scales = null,
            bool[] highlightMask = null)
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

            Color = Color.LightBlue;
            HighlightColor = Color.LightGreen;
            _alphaUniform.Value = 0.5f;

            BuildMeshes(positions, scales, highlightMask);
        }

        private void BuildMeshes(Vector3D[] positions, float[] scales, bool[] highlightMask)
        {
            var normalVerts = new List<Vector3D>();
            var normalIndices = new IndicesUnsignedInt();
            var highlightVerts = new List<Vector3D>();
            var highlightIndices = new IndicesUnsignedInt();

            for (int i = 0; i < positions.Length; i++)
            {
                float scale = (scales != null && i < scales.Length) ? scales[i] : 1.0f;
                bool highlight = (highlightMask != null && i < highlightMask.Length) && highlightMask[i];

                var verts = highlight ? highlightVerts : normalVerts;
                var indices = highlight ? highlightIndices : normalIndices;
                int baseIdx = verts.Count;

                AddSatelliteGeometry(verts, indices, positions[i], scale, baseIdx);
            }

            _normalMesh = CreateMeshBuffers(normalVerts, normalIndices);
            _highlightMesh = CreateMeshBuffers(highlightVerts, highlightIndices);
            _hasHighlight = highlightVerts.Count > 0;
            _hasNormal = normalVerts.Count > 0;
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
        /// Adds a satellite shape: body box + 2 solar panel boxes, oriented radially.
        /// Body: 200×300×150 km scaled. Panels: 400×20×150 km scaled each.
        /// </summary>
        private static void AddSatelliteGeometry(
            List<Vector3D> verts, IndicesUnsignedInt indices,
            Vector3D position, float scale, int baseIdx)
        {
            // Orientation: "up" = radial direction from center
            Vector3D up = position.Normalize();
            // Panel direction perpendicular to up
            Vector3D hint = Math.Abs(up.Dot(Vector3D.UnitZ)) < 0.9 ? Vector3D.UnitZ : Vector3D.UnitX;
            Vector3D panelDir = up.Cross(hint).Normalize();
            Vector3D fwd = panelDir.Cross(up).Normalize();

            // Sizes in km, converted to meters to match position units
            double bodyW = 60.0e3 * scale;   // along panelDir
            double bodyH = 90.0e3 * scale;   // along up
            double bodyD = 45.0e3 * scale;   // along fwd
            double panelW = 120.0e3 * scale;
            double panelH = 6.0e3 * scale;
            double panelD = 45.0e3 * scale;
            double panelOffset = (bodyW + panelW) * 0.5;

            // Body box
            int idx = baseIdx;
            idx = AddBox(verts, indices, position, panelDir, up, fwd, bodyW, bodyH, bodyD, idx);

            // Left solar panel
            Vector3D leftPos = position + panelDir * panelOffset;
            idx = AddBox(verts, indices, leftPos, panelDir, up, fwd, panelW, panelH, panelD, idx);

            // Right solar panel
            Vector3D rightPos = position - panelDir * panelOffset;
            AddBox(verts, indices, rightPos, panelDir, up, fwd, panelW, panelH, panelD, idx);
        }

        /// <summary>
        /// Adds an oriented box (8 vertices, 12 triangles) to the vertex/index lists.
        /// Returns the next available vertex index.
        /// </summary>
        private static int AddBox(
            List<Vector3D> verts, IndicesUnsignedInt indices,
            Vector3D center, Vector3D right, Vector3D up, Vector3D fwd,
            double w, double h, double d, int baseIdx)
        {
            Vector3D hw = right * (w * 0.5);
            Vector3D hh = up * (h * 0.5);
            Vector3D hd = fwd * (d * 0.5);

            // 8 corners
            verts.Add(center - hw - hh - hd);  // 0
            verts.Add(center + hw - hh - hd);  // 1
            verts.Add(center + hw + hh - hd);  // 2
            verts.Add(center - hw + hh - hd);  // 3
            verts.Add(center - hw - hh + hd);  // 4
            verts.Add(center + hw - hh + hd);  // 5
            verts.Add(center + hw + hh + hd);  // 6
            verts.Add(center - hw + hh + hd);  // 7

            int b = baseIdx;
            // 6 faces, 2 triangles each
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+4, b+5, b+6)); // front
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+4, b+6, b+7));
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+1, b+0, b+3)); // back
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+1, b+3, b+2));
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+1, b+6, b+5)); // right
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+1, b+2, b+6));
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+2, b+3, b+7)); // top
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+2, b+7, b+6));
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+3, b+0, b+4)); // left
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+3, b+4, b+7));
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+0, b+1, b+5)); // bottom
            indices.AddTriangle(new TriangleIndicesUnsignedInt(b+0, b+5, b+4));

            return b + 8;
        }

        public void Render(Context context, SceneState sceneState)
        {
            if (_normalVA == null && _normalMesh != null)
                _normalVA = context.CreateVertexArray(_normalMesh);
            if (_highlightVA == null && _highlightMesh != null)
                _highlightVA = context.CreateVertexArray(_highlightMesh);

            if (_hasNormal && _normalVA != null)
            {
                _colorUniform.Value = new Vector3F(_color.R / 255f, _color.G / 255f, _color.B / 255f);
                _drawState.VertexArray = _normalVA;
                context.Draw(OpenTK.Graphics.OpenGL.PrimitiveType.Triangles, _drawState, sceneState);
            }

            if (_hasHighlight && _highlightVA != null)
            {
                _colorUniform.Value = new Vector3F(_highlightColor.R / 255f, _highlightColor.G / 255f, _highlightColor.B / 255f);
                _drawState.VertexArray = _highlightVA;
                context.Draw(OpenTK.Graphics.OpenGL.PrimitiveType.Triangles, _drawState, sceneState);
            }
        }

        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        public Color HighlightColor
        {
            get => _highlightColor;
            set => _highlightColor = value;
        }

        public void Dispose()
        {
            _normalVA?.Dispose();
            _highlightVA?.Dispose();
            _drawState.ShaderProgram?.Dispose();
        }

        private readonly Context _context;
        private readonly DrawState _drawState;
        private readonly Uniform<Vector3F> _colorUniform;
        private readonly Uniform<float> _alphaUniform;

        private MeshBuffers _normalMesh;
        private MeshBuffers _highlightMesh;
        private VertexArray _normalVA;
        private VertexArray _highlightVA;
        private bool _hasNormal;
        private bool _hasHighlight;
        private Color _color;
        private Color _highlightColor;
    }
}
