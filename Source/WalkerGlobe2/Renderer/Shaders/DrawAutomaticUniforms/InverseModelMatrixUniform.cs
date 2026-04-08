using WalkerGlobe2.Core;

namespace WalkerGlobe2.Renderer
{
    internal class InverseModelMatrixUniform : DrawAutomaticUniform
    {
        public InverseModelMatrixUniform(Uniform uniform)
        {
            _uniform = (Uniform<Matrix4F>)uniform;
        }

        public override void Set(Context context, DrawState drawState, SceneState sceneState)
        {
            _uniform.Value = sceneState.InverseModelMatrix.ToMatrix4F();
        }

        private Uniform<Matrix4F> _uniform;
    }
}
