#region License
//
// (C) Copyright 2009 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

namespace WalkerGlobe2.Core
{
    public class VertexAttributeHalfFloatVector4 : VertexAttribute<Vector4H>
    {
        public VertexAttributeHalfFloatVector4(string name)
            : base(name, VertexAttributeType.HalfFloatVector4)
        {
        }

        public VertexAttributeHalfFloatVector4(string name, int capacity)
            : base(name, VertexAttributeType.HalfFloatVector4, capacity)
        {
        }
    }
}
