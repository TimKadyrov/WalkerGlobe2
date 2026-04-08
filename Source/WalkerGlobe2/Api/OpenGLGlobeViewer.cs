using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using WalkerGlobe2.Core;
namespace WalkerGlobe2.Api
{
    /// <summary>
    /// OpenTK-based implementation of IGlobeViewer.
    /// Creates a standalone OpenGL window with 3D globe, GSO arc, lat/lon grid.
    /// </summary>
    public class OpenGLGlobeViewer : IGlobeViewer
    {
        private readonly WalkerGlobe2.WalkerGlobe _globe;
        private Color _satColor = Color.LightGray;
        private Color _satHighlightColor = Color.Yellow;

        public OpenGLGlobeViewer(string textureFile = null)
        {
            if (textureFile == null)
            {
                textureFile = WalkerGlobe.DefaultDayTexturePath;
            }

            _globe = new WalkerGlobe2.WalkerGlobe(textureFile, false, hudEnabled: true);
        }

        public void SetTime(double timeSeconds)
        {
            _globe.SetTime((int)timeSeconds);
        }

        public void UpdateSatellites(Vector3D[] positionsKm, float[] scales = null, bool[] highlighted = null)
        {
            // Convert km to meters (WalkerGlobe uses meters internally via Wgs84 in meters)
            var positionsM = new Vector3D[positionsKm.Length];
            for (int i = 0; i < positionsKm.Length; i++)
                positionsM[i] = positionsKm[i] * 1000.0;

            _globe.AddSatelliteMarkers(positionsM, "satellites", _satColor, scales, highlighted);
        }

        public void UpdateGroundStations(string key, Vector3D[] positionsKm, Color color, float[] scales = null, float alpha = 0.5f)
        {
            var positionsM = new Vector3D[positionsKm.Length];
            for (int i = 0; i < positionsKm.Length; i++)
                positionsM[i] = positionsKm[i] * 1000.0;

            _globe.AddGroundStationMarkers(positionsM, key, color, scales, alpha);
        }

        public void SetGroundPolyline(string key, Vector2D[] latLonRad, Color color)
        {
            var points = new List<Vector3D>();
            var shape = Ellipsoid.Wgs84;
            foreach (var ll in latLonRad)
                points.Add(shape.ToVector3D(new Geodetic3D(ll.X, ll.Y)));

            _globe.AddSpacePolyline(points, key, color, color);
        }

        public void SetGroundPolygons(string key, Vector2D[][] latLonRad, Color lineColor, Color fillColor)
        {
            _globe.AddPolygonShapeCollection(latLonRad, key, lineColor, fillColor);
        }

        public void SetSpacePolyline(string key, Vector3D[] positionsKm, Color color)
        {
            // Convert km to meters
            var points = new List<Vector3D>();
            foreach (var p in positionsKm)
                points.Add(p * 1000.0);

            _globe.AddSpacePolyline(points, key, color, color);
        }

        public void SetSphereMarkers(string key, Vector3D[] positionsKm, Color color, double radiusKm = 120.0, float alpha = 0.7f)
        {
            var positionsM = new Vector3D[positionsKm.Length];
            for (int i = 0; i < positionsKm.Length; i++)
                positionsM[i] = positionsKm[i] * 1000.0;

            _globe.AddSphereMarkers(positionsM, key, color, radiusKm * 1000.0, alpha);
        }

        public void SetLineSegments(string key, Vector3D[] endpointsKm, Color color)
        {
            var points = new List<Vector3D>();
            foreach (var p in endpointsKm)
                points.Add(p * 1000.0);

            _globe.AddLineSegments(points, key, color);
        }

        public void Remove(string key)
        {
            _globe.RemoveShape(key);
        }

        public Color SatelliteColor
        {
            get => _satColor;
            set => _satColor = value;
        }

        public Color SatelliteHighlightColor
        {
            get => _satHighlightColor;
            set => _satHighlightColor = value;
        }

        public bool ShowGrid { get; set; }

        public bool IsRendering => _globe.IsRendering;

        public void Run()
        {
            _globe.Run(30.0);
        }

        public void Close()
        {
            _globe.Close();
        }

        public void Dispose()
        {
            _globe.Dispose();
        }
    }
}
