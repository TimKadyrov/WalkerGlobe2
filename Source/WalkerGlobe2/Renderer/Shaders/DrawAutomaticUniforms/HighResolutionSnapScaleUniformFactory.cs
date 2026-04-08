#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

namespace WalkerGlobe2.Renderer
{
    internal class HighResolutionSnapScaleUniformFactory : DrawAutomaticUniformFactory
    {
        #region HighResolutionSnapScaleUniformFactory Members

        public override string Name
        {
            get { return "og_highResolutionSnapScale"; }
        }

        public override DrawAutomaticUniform Create(Uniform uniform)
        {
            return new HighResolutionSnapScaleUniform(uniform);
        }

        #endregion
    }

    internal class EarthRotationAngleUniformFactory : DrawAutomaticUniformFactory
    {
        #region EarthRotationAngle Members

        public override string Name
        {
            get { return "rotanglez"; }
        }

        public override DrawAutomaticUniform Create(Uniform uniform)
        {
            return new EarthRotationAngle(uniform);
        }

        #endregion
    }
}
