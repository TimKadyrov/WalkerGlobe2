using System;
using System.Drawing;
using WalkerGlobe2.Core;

namespace WalkerGlobe2.Api
{
    /// <summary>
    /// Clean API for radians integration. Wraps the OpenTK globe viewer.
    /// All positions are in ECI (inertial) frame in kilometers.
    /// Ground positions are in ECEF and rotated automatically.
    /// </summary>
    public interface IGlobeViewer : IDisposable
    {
        /// <summary>Set simulation time (updates Earth rotation).</summary>
        void SetTime(double timeSeconds);

        /// <summary>Update satellite positions (ECI, km). Supports per-satellite scale and highlighting.</summary>
        void UpdateSatellites(Vector3D[] positionsKm, float[] scales = null, bool[] highlighted = null);

        /// <summary>Add/update an ECEF polyline (e.g. coverage footprint on ground). Coordinates in radians.</summary>
        void SetGroundPolyline(string key, Vector2D[] latLonRad, Color color);

        /// <summary>Add/update ground polygons (ECEF). Coordinates in radians.</summary>
        void SetGroundPolygons(string key, Vector2D[][] latLonRad, Color lineColor, Color fillColor);

        /// <summary>Add/update ground station markers (ECEF, km). Rotates with Earth.</summary>
        void UpdateGroundStations(string key, Vector3D[] positionsKm, Color color, float[] scales = null);

        /// <summary>Add/update an ECI polyline (e.g. orbit trace). Positions in km.</summary>
        void SetSpacePolyline(string key, Vector3D[] positionsKm, Color color);

        /// <summary>Add/update sphere markers (ECI, km). Used for selection highlights.</summary>
        void SetSphereMarkers(string key, Vector3D[] positionsKm, Color color, double radiusKm = 120.0, float alpha = 0.7f);

        /// <summary>Add/update line segments (ECI, km). Pairs of endpoints: [from0, to0, from1, to1, ...]</summary>
        void SetLineSegments(string key, Vector3D[] endpointsKm, Color color);

        /// <summary>Remove a renderable by key.</summary>
        void Remove(string key);

        /// <summary>Set satellite marker colors.</summary>
        Color SatelliteColor { get; set; }
        Color SatelliteHighlightColor { get; set; }

        /// <summary>Toggle lat/lon grid overlay.</summary>
        bool ShowGrid { get; set; }

        /// <summary>Run the viewer window (blocking).</summary>
        void Run();

        /// <summary>Close the viewer window.</summary>
        void Close();

        /// <summary>True while the viewer has pending render operations.</summary>
        bool IsRendering { get; }
    }
}
