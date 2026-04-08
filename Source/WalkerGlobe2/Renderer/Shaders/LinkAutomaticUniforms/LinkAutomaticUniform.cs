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
    public abstract class LinkAutomaticUniform
    {
        public abstract string Name { get; }
        public abstract void Set(Uniform uniform);
    }
}
