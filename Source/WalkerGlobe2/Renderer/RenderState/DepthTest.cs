#region License
//
// (C) Copyright 2009 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

namespace WalkerGlobe2.Renderer
{
    public enum DepthTestFunction
    {
        Never,
        Less,
        Equal,
        LessThanOrEqual,
        Greater,
        NotEqual,
        GreaterThanOrEqual,
        Always
    }

    public class DepthTest
    {
        public DepthTest()
        {
            Enabled = true;
            Function = DepthTestFunction.Less;
        }

        public bool Enabled { get; set; }
        public DepthTestFunction Function { get; set; }
    }
}
