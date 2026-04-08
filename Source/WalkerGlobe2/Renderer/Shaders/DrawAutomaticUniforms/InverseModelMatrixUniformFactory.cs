namespace WalkerGlobe2.Renderer
{
    internal class InverseModelMatrixUniformFactory : DrawAutomaticUniformFactory
    {
        public override string Name
        {
            get { return "og_inverseModelMatrix"; }
        }

        public override DrawAutomaticUniform Create(Uniform uniform)
        {
            return new InverseModelMatrixUniform(uniform);
        }
    }
}
