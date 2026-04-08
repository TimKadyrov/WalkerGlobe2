using System;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using WalkerGlobe2.Core;

namespace WalkerGlobe2.WpfSample
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private double _timeSeconds;
        // Demo scene data
        private Vector3D[] _satPositions;
        private Vector3D[] _esPositions;       // static ECEF positions
        private float[] _satScales;
        private bool[] _satHighlight;

        // Orbital elements per satellite (for proper propagation)
        private double[] _satRaan;
        private double[] _satInclination;
        private double[] _satTrueAnomaly;

        private const double EarthR = 6378.137;       // km
        private const double Altitude = 1400.0;        // km
        private const double OrbitR = EarthR + Altitude;
        private const double Mu = 398600.4418;         // km^3/s^2
        private const double SolarDay = 86400.0;        // must match SceneState.CBRotationAngleRad

        public MainWindow()
        {
            InitializeComponent();

            // Textures are in Resources folder, copied to output at build

            globeControl.GlobeReady += OnGlobeReady;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // ~30 fps
            _timer.Tick += OnTimerTick;
        }

        private void OnGlobeReady()
        {
            BuildDemoScene();
            UpdateScene();
        }

        /// <summary>
        /// Creates a demo Walker constellation: 24 satellites in 3 orbital planes at ~1400 km altitude.
        /// Plus 4 ground stations. Stores orbital elements for proper propagation.
        /// </summary>
        private void BuildDemoScene()
        {
            const int planesCount = 3;
            const int satsPerPlane = 8;
            const double inclination = 55.0 * Math.PI / 180.0;
            int total = planesCount * satsPerPlane;

            _satPositions = new Vector3D[total];
            _satScales = new float[total];
            _satHighlight = new bool[total];
            _satRaan = new double[total];
            _satInclination = new double[total];
            _satTrueAnomaly = new double[total];

            for (int p = 0; p < planesCount; p++)
            {
                double raan = 2.0 * Math.PI * p / planesCount;
                for (int s = 0; s < satsPerPlane; s++)
                {
                    int idx = p * satsPerPlane + s;
                    double trueAnomaly = 2.0 * Math.PI * s / satsPerPlane;

                    _satRaan[idx] = raan;
                    _satInclination[idx] = inclination;
                    _satTrueAnomaly[idx] = trueAnomaly;

                    _satPositions[idx] = OrbitalElementsToECI(raan, inclination, trueAnomaly);
                    _satScales[idx] = 1.0f;
                    _satHighlight[idx] = (s == 0); // highlight first sat in each plane
                }
            }

            // Ground stations at various locations (ECEF, km)
            _esPositions = new Vector3D[]
            {
                LatLonToECF(48.86, 2.35, EarthR),    // Paris
                LatLonToECF(40.71, -74.01, EarthR),   // New York
                LatLonToECF(-33.87, 151.21, EarthR),  // Sydney
                LatLonToECF(35.68, 139.69, EarthR),   // Tokyo
            };
        }

        /// <summary>
        /// Converts orbital elements to ECI position (km).
        /// </summary>
        private static Vector3D OrbitalElementsToECI(double raan, double inc, double ta)
        {
            double xOrb = OrbitR * Math.Cos(ta);
            double yOrb = OrbitR * Math.Sin(ta);

            // Rotate by inclination (around X) then RAAN (around Z)
            double x1 = xOrb;
            double y1 = yOrb * Math.Cos(inc);
            double z1 = yOrb * Math.Sin(inc);

            double cosR = Math.Cos(raan), sinR = Math.Sin(raan);
            return new Vector3D(
                x1 * cosR - y1 * sinR,
                x1 * sinR + y1 * cosR,
                z1);
        }

        /// <summary>
        /// Rotates an ECEF position to ECI using current Earth rotation angle.
        /// </summary>
        private Vector3D EcefToEci(Vector3D ecef)
        {
            double angle = (_timeSeconds % SolarDay) / SolarDay * 2.0 * Math.PI;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            return new Vector3D(
                ecef.X * cos - ecef.Y * sin,
                ecef.X * sin + ecef.Y * cos,
                ecef.Z);
        }

        private void UpdateScene()
        {
            if (_satPositions == null) return;

            float scale = (float)sliderSatScale.Value;
            var scales = new float[_satPositions.Length];
            for (int i = 0; i < scales.Length; i++)
                scales[i] = scale;

            // All positions are ECI — satellites from orbital propagation, ground stations rotated from ECEF
            if (chkSatellites.IsChecked == true)
                globeControl.UpdateSatellites(_satPositions, System.Drawing.Color.LightBlue, scales, _satHighlight);

            if (chkGroundStations.IsChecked == true)
            {
                var esEci = new Vector3D[_esPositions.Length];
                for (int i = 0; i < _esPositions.Length; i++)
                    esEci[i] = EcefToEci(_esPositions[i]);
                globeControl.UpdateGroundStations("es", esEci, System.Drawing.Color.Yellow, ecef: false);
            }

            if (chkLinks.IsChecked == true)
                BuildLinkLines();

            globeControl.ShowGrid = chkGrid.IsChecked == true;
        }

        private void BuildLinkLines()
        {
            // Convert ground stations to ECI for comparison with satellites
            var esEci = new Vector3D[_esPositions.Length];
            for (int i = 0; i < _esPositions.Length; i++)
                esEci[i] = EcefToEci(_esPositions[i]);

            // Each highlighted satellite gets a link to nearest visible ground station
            for (int i = 0; i < _satPositions.Length; i++)
            {
                string key = $"link_{i}";
                if (!_satHighlight[i])
                {
                    globeControl.RemoveShape(key);
                    continue;
                }

                // Find nearest ground station with LOS
                double minDist = double.MaxValue;
                int nearest = -1;
                for (int j = 0; j < esEci.Length; j++)
                {
                    if (!HasLineOfSight(_satPositions[i], esEci[j], EarthR))
                        continue;
                    double d = (_satPositions[i] - esEci[j]).Magnitude;
                    if (d < minDist) { minDist = d; nearest = j; }
                }

                if (nearest < 0)
                {
                    globeControl.RemoveShape(key);
                    continue;
                }

                var pts = new Vector3D[] { _satPositions[i], esEci[nearest] };
                globeControl.SetSpacePolyline(key, pts, System.Drawing.Color.Yellow, ecef: false);
            }
        }

        /// <summary>
        /// Returns true if the straight line between two points doesn't intersect a sphere of given radius.
        /// </summary>
        private static bool HasLineOfSight(Vector3D a, Vector3D b, double radius)
        {
            // Closest point on segment AB to the origin (Earth center).
            // Clamp t to the interior of the segment (exclude endpoints) since
            // ground stations sit exactly on the surface.
            var ab = b - a;
            double t = -a.Dot(ab) / ab.Dot(ab);
            t = Math.Max(0.001, Math.Min(0.999, t));
            var closest = a + ab * t;
            return closest.Magnitude > radius;
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _timeSeconds += 30; // 30-second time steps
            globeControl.SetTime(_timeSeconds);
            txtTime.Text = $"Time: {TimeSpan.FromSeconds(_timeSeconds):d\\.hh\\:mm\\:ss}";

            // Rotate satellites (very simplified orbital motion)
            AdvanceSatellites();
            UpdateScene();
        }

        private void AdvanceSatellites()
        {
            double period = 2.0 * Math.PI * Math.Sqrt(Math.Pow(OrbitR, 3) / Mu);
            double angularRate = 2.0 * Math.PI / period;
            double dt = 30.0; // seconds per tick
            double dAngle = angularRate * dt;

            for (int i = 0; i < _satPositions.Length; i++)
            {
                _satTrueAnomaly[i] += dAngle;
                _satPositions[i] = OrbitalElementsToECI(_satRaan[i], _satInclination[i], _satTrueAnomaly[i]);
            }
        }

        #region UI Event Handlers

        private void OnPlayClick(object sender, RoutedEventArgs e)
        {
            _timer.Start();
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _timeSeconds = 0;
            txtTime.Text = "Time: 0s";
            BuildDemoScene();
            globeControl.SetTime(0);
            UpdateScene();
        }

        private void OnGridToggle(object sender, RoutedEventArgs e)
        {
            if (globeControl != null)
                globeControl.ShowGrid = chkGrid.IsChecked == true;
        }

        private void OnSatToggle(object sender, RoutedEventArgs e) => UpdateScene();
        private void OnEsToggle(object sender, RoutedEventArgs e) => UpdateScene();
        private void OnLinksToggle(object sender, RoutedEventArgs e) => UpdateScene();

        private void OnDayNightToggle(object sender, RoutedEventArgs e)
        {
            if (globeControl != null)
                globeControl.ShowDayNight = chkDayNight.IsChecked == true;
        }

        private void OnAtmosphereToggle(object sender, RoutedEventArgs e)
        {
            if (globeControl != null)
                globeControl.ShowAtmosphere = chkAtmosphere.IsChecked == true;
        }

        private void OnSatScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_satPositions != null)
                UpdateScene();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _timer.Stop();
            globeControl.Dispose();
        }

        #endregion

        private static Vector3D LatLonToECF(double latDeg, double lonDeg, double radius)
        {
            double latRad = latDeg * Math.PI / 180.0;
            double lonRad = lonDeg * Math.PI / 180.0;
            return new Vector3D(
                radius * Math.Cos(latRad) * Math.Cos(lonRad),
                radius * Math.Cos(latRad) * Math.Sin(lonRad),
                radius * Math.Sin(latRad));
        }
    }
}
