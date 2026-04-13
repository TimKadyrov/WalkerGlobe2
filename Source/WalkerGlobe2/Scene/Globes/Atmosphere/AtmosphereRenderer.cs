using System;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    public sealed class AtmosphereRenderer : IDisposable
    {
        public AtmosphereRenderer(Context context, Ellipsoid shape)
        {
            _shape = shape;

            ShaderProgram sp = Device.CreateShaderProgram(
                EmbeddedResources.GetText("WalkerGlobe.Scene.Globes.Atmosphere.Shaders.AtmosphereVS.glsl"),
                EmbeddedResources.GetText("WalkerGlobe.Scene.Globes.Atmosphere.Shaders.AtmosphereFS.glsl"));

            _globeRadiiSquared = (Uniform<Vector3F>)sp.Uniforms["u_globeRadiiSquared"];
            _atmosphereRadiusSquared = (Uniform<float>)sp.Uniforms["u_atmosphereRadiusSquared"];

            Vector3D r = shape.Radii;
            _globeRadiiSquared.Value = new Vector3F((float)(r.X * r.X), (float)(r.Y * r.Y), (float)(r.Z * r.Z));

            // Atmosphere extends ~1.5% above the surface (tight band like Google Earth)
            double outerRadius = shape.MaximumRadius * 1.015;
            _atmosphereRadiusSquared.Value = (float)(outerRadius * outerRadius);

            _renderState = new RenderState();
            _renderState.FacetCulling.Enabled = false;
            _renderState.DepthTest.Enabled = false;
            _renderState.DepthMask = false; // don't write depth — atmosphere is transparent
            _renderState.Blending.Enabled = true;
            _renderState.Blending.SourceRGBFactor = SourceBlendingFactor.SourceAlpha;
            _renderState.Blending.DestinationRGBFactor = DestinationBlendingFactor.OneMinusSourceAlpha;
            _renderState.Blending.SourceAlphaFactor = SourceBlendingFactor.One;
            _renderState.Blending.DestinationAlphaFactor = DestinationBlendingFactor.OneMinusSourceAlpha;

            _drawState = new DrawState(_renderState, sp, null);

            // Build bounding box for the atmosphere shell (slightly larger than globe)
            Vector3D atmosphereRadii = new Vector3D(outerRadius, outerRadius, outerRadius);
            Mesh mesh = BoxTessellator.Compute(2 * atmosphereRadii);
            _va = context.CreateVertexArray(mesh, sp.VertexAttributes, BufferHint.StaticDraw);
            _drawState.VertexArray = _va;
            _primitiveType = mesh.PrimitiveType;
            _renderState.FacetCulling.FrontFaceWindingOrder = mesh.FrontFaceWindingOrder;
        }

        public void Render(Context context, SceneState sceneState)
        {
            context.Draw(_primitiveType, _drawState, sceneState);
        }

        public void Dispose()
        {
            _drawState.ShaderProgram.Dispose();
            _va?.Dispose();
        }

        private readonly DrawState _drawState;
        private readonly RenderState _renderState;
        private readonly Ellipsoid _shape;
        private readonly Uniform<Vector3F> _globeRadiiSquared;
        private readonly Uniform<float> _atmosphereRadiusSquared;
        private VertexArray _va;
        private OpenTK.Graphics.OpenGL.PrimitiveType _primitiveType;
    }
}
