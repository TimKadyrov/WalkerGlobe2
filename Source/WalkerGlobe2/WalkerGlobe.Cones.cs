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
using System.Linq;

namespace WalkerGlobe2
{
    public partial class WalkerGlobe : IDisposable
    {
        public void AddCoverageCones(List<Vector3D> positions, List<Vector2D> targets, List<double> sat_height,
            double elevation, Color line, Color fill)
        {
            if (_coneType == CoverageDisplayMode.FilledCone)
            {
                lock (renderQueue)
                {
                    renderQueue.Enqueue(() =>
                    {
                        AddHollowConeToQueue(positions.ToArray(), targets.ToArray(), sat_height.ToArray(), elevation,
                            fill);
                    });
                }
            }

            if (_coneType == CoverageDisplayMode.OutlinedCone)
            {
                lock (renderQueue)
                {
                    renderQueue.Enqueue(() =>
                    {
                        AddOutlinedConesToQueue(positions.ToArray(), targets.ToArray(), sat_height.ToArray(), elevation,
                            line,
                            fill);
                    });
                }
            }
        }

        public void AddCoverageCones(Vector3D[] positions, Vector2D[] targets, double[] sat_height, double elevation,
            Color line, Color fill)
        {
            if (_coneType == CoverageDisplayMode.FilledCone)
                lock (renderQueue)
                {
                    renderQueue.Enqueue(() =>
                    {
                        AddHollowConeToQueue(positions, targets, sat_height, elevation, fill);
                    });
                }

            if (_coneType == CoverageDisplayMode.OutlinedCone)
                lock (renderQueue)
                {
                    renderQueue.Enqueue(() =>
                    {
                        AddOutlinedConesToQueue(positions, targets, sat_height, elevation, line, fill);
                    });
                }
        }

        private void AddHollowConeToQueue(Vector3D[] positions, Vector2D[] targets, double[] sat_height,
            double elevation, Color fill)
        {
            lock (_cones)
            {
                var cnt = positions.Count();

                _cones = new List<Cone>();
                _cones2 = new List<Cone2>();
                Vector3D[] target = new Vector3D[cnt];
                double[] radius = new double[cnt];
                double[] height = new double[cnt];
                for (int i = 0; i < cnt; i++)
                {
                    GetConeSize(elevation, targets[i], sat_height[i], out target[i], out radius[i],
                        out height[i]);
                }

                var cone = new Cone2(_context, positions, target, height, radius, 72)
                {
                    FillColor = fill,
                    FillTranslucency = 0.9f
                };
                _cones2.Add(cone);
            }
        }

        private void AddOutlinedConesToQueue(Vector3D[] positions, Vector2D[] targets, double[] sat_height,
            double elvation, Color line, Color fill)
        {
            lock (_cones)
            {
                var cnt = positions.Count();

                _cones2 = new List<Cone2>();
                if (_cones.Count == cnt)
                {
                    for (int i = 0; i < cnt; i++)
                    {
                        GetConeSize(elvation, targets[i], sat_height[i], out Vector3D trg, out double rad,
                            out double hght);
                        _cones[i].SetCone(positions[i], trg.Normalize(), hght, rad);
                    }
                }
                else
                {
                    _cones = new List<Cone>();
                    for (int i = 0; i < cnt; i++)
                    {
                        var cone = new Cone(_context, 72)
                        {
                            FillColor = fill,
                            OutlineColor = line,
                            OutlineWidth = 0.5,
                            FillTranslucency = 0.9f,
                            Show = true,
                            ShowFill = true,
                            ShowOutline = true
                        };
                        GetConeSize(elvation, targets[i], sat_height[i], out Vector3D trg, out double rad,
                            out double hght);
                        cone.SetCone(positions[i], trg.Normalize(), hght, rad);
                        _cones.Add(cone);
                    }
                }
            }
        }

        private void GetConeSize(double elevation, Vector2D target, double sat_height, out Vector3D trg, out double rad, out double hght)
        {
            trg = _globe.Shape.ToVector3D(new Geodetic3D(target.X, target.Y)).RotateAroundAxis(Vector3D.UnitZ, _sceneState.CBRotationAngleRad);
            trg = _globe.Shape.GeodeticSurfaceNormal(trg);
            const double radAt = 6378.145e3; // ITU-R S.1503 Earth radius (meters)

            double r, theta, c, si, temp;
            /* coverage_angle is global variable (in rad) */
            theta = elevation / 180.0 * Math.PI;

            c = Math.Cos(theta);
            si = Math.Sin(theta);
            r = sat_height / radAt;
            temp = Math.Sqrt(r * r - c * c);
            rad = c * (temp - si) / r;
            hght = temp * (temp - si) / r;
            rad = rad * radAt;
            hght = hght * radAt;
        }

        public void ClearCoverageCones()
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    lock (_cones)
                    {
                        _cones = new List<Cone>();
                        _cones2 = new List<Cone2>();
                    }
                });
            }
        }

        public CoverageDisplayMode CoverageRenderMode
        {
            get { return _coneType; }
            set { _coneType = value; }
        }
    }
    public enum CoverageDisplayMode { None=0, SuppliedGroundFootprint=1, OutlinedCone=2, FilledCone=3};
}