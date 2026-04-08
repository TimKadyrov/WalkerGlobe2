//#define SINGLE_THREADED

#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using System.Drawing;
using System.Collections.Generic;
using WalkerGlobe2.Scene;
using WalkerGlobe2.Renderer;
using WalkerGlobe2.Core;

namespace WalkerGlobe2
{
    public partial class WalkerGlobe : IDisposable
    {
        public void AddMultiBeam(Vector3D position, List<Vector2D> targets, List<double> sat_height, double elevation, List<Color> line, List<Color> fill)
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    /*lock (_cones)
                    {
                        _cones = new List<Cone>();
                        for (int i = 0; i < targets.Count; i++)
                        {
                            var cone = new Cone(_window.Context, 72)
                            {
                                FillColor = fill[i],
                                OutlineColor = line[i],
                                OutlineWidth = 0.5,
                                FillTranslucency = 0.9f,
                                Show = true,
                                ShowFill = true,
                                ShowOutline = true
                            };
                            GetConeSize(elevation, targets[i], sat_height[i], out Vector3D trg, out double rad, out double hght);
                            cone.SetCone(position, trg.Normalize(), hght, rad);
                            _cones.Add(cone);
                        }
                    }*/
                     lock (_cones2)
                    {
                        _cones2 = new List<Cone2>();
                        Vector3D[] target = new Vector3D[targets.Count];
                        double[] radius = new double[targets.Count];
                        double[] height = new double[targets.Count];
                        for (int i = 0; i < targets.Count; i++)
                        {
                            GetConeSize(elevation, targets[i], sat_height[i], out target[i], out radius[i], out height[i]);
                        }

                        var cone = new Cone2(_window.Context, new Vector3D[] { position }, target, height, radius, 72)
                        {
                            FillColor = fill[0],
                            FillTranslucency = 0.9f
                        };
                        _cones2.Add(cone);
                    }
                });
            }
        }

    }
}