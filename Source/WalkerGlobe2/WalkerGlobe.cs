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
using System.Threading;
using System.Drawing;
using System.Collections.Generic;
using System.Timers;
using WalkerGlobe2.Scene;
using WalkerGlobe2.Renderer;
using WalkerGlobe2.Core;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WalkerGlobe2
{
    public partial class WalkerGlobe : IDisposable
    {
        /// <summary>
        /// Standalone mode: creates its own OpenTK GameWindow.
        /// </summary>
        public WalkerGlobe(string globeTextureFile, bool timer, bool hudEnabled=true)
            : this(globeTextureFile, null, timer, hudEnabled, null, 0, 0)
        {
        }

        /// <summary>
        /// Hosted mode: uses an external GL context (for WPF embedding).
        /// Pass context from GLWpfControl. No window is created.
        /// </summary>
        public WalkerGlobe(string globeTextureFile, Context externalContext, int width, int height)
            : this(globeTextureFile, null, false, false, externalContext, width, height)
        {
        }

        /// <summary>
        /// Hosted mode with pre-loaded bitmap (avoids blocking texture load).
        /// </summary>
        public WalkerGlobe(Bitmap preloadedTexture, Context externalContext, int width, int height)
            : this(null, preloadedTexture, false, false, externalContext, width, height)
        {
        }

        private WalkerGlobe(string globeTextureFile, Bitmap preloadedTexture, bool timer, bool hudEnabled, Context externalContext, int width, int height)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            void Log(string msg) { Console.WriteLine($"  [{sw.ElapsedMilliseconds,5}ms] {msg}"); sw.Restart(); }

            _hosted = externalContext != null;
            Ellipsoid globeShape = Ellipsoid.Wgs84;

            if (!_hosted)
            {
                _window =                   Device.CreateWindow(1920, 1080, "Walker Globe");
                Log("CreateWindow");
                _window.Resize +=           OnResize;
                _window.RenderFrame +=      OnRenderFrame;
                _window.Keyboard.KeyDown += OnKeyDown;
                _window.Keyboard.KeyUp +=   OnKeyUp;
                _context = _window.Context;
            }
            else
            {
                _context = externalContext;
            }

            _sceneState =               new SceneState();
            if (!_hosted)
                _camera =               new CameraLookAtPoint(_sceneState.Camera, _window, globeShape);
            _clearState =               new ClearState
            {
                Color = Color.Black
            };

            var texBitmap = preloadedTexture ?? new Bitmap(globeTextureFile);
            Log("Bitmap load");
            _globeTexture = Device.CreateTexture2D(texBitmap, TextureFormat.RedGreenBlue8, false);
            Log("Texture upload");
            if (preloadedTexture == null) texBitmap.Dispose();

            // Load night texture (city lights) if available
            string nightTexPath = FindNightTexture(globeTextureFile);
            if (nightTexPath != null)
            {
                var nightBitmap = new Bitmap(nightTexPath);
                _nightTexture = Device.CreateTexture2D(nightBitmap, TextureFormat.RedGreenBlue8, false);
                nightBitmap.Dispose();
                Log("Night texture");
            }

            _globe = new RayCastedGlobe(_context)
            {
                Texture =               _globeTexture,
                NightTexture =          _nightTexture,
                Shape =                 globeShape,
                UseAverageDepth=        true
            };
            Log("RayCastedGlobe");
            _atmosphere = new AtmosphereRenderer(_context, globeShape);
            Log("Atmosphere");
            _gridGlobe = new LatitudeLongitudeGridGlobe(_context)
            {
                Texture = _globeTexture,
                Shape = globeShape,
                GridResolutions = new GridResolutionCollection(new List<GridResolution>
                {
                    new GridResolution(
                        new Interval(0, 1e7, IntervalEndpoint.Closed, IntervalEndpoint.Open),
                        new Vector2D(1.0 / 360.0, 1.0 / 180.0)),     // 1-degree grid
                    new GridResolution(
                        new Interval(1e7, double.MaxValue),
                        new Vector2D(1.0 / 36.0, 1.0 / 18.0))        // 10-degree grid
                })
            };
            Log("LatLonGrid");
            _showGrid = false;
            _hudEnabled = hudEnabled && !_hosted;

            if (_hudEnabled)
            {
                pfc = new PrivateFontCollection();
                byte[] FontData;
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                    .Where(n => n.Contains("Px437_ToshibaLCD_8x16")).Select(n => Assembly.GetExecutingAssembly().GetManifestResourceStream(n)).FirstOrDefault())
                {
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    FontData = ms.ToArray();
                }
                unsafe
                {
                    fixed (byte* pFontData = FontData)
                    {
                        pfc.AddMemoryFont((System.IntPtr)pFontData, FontData.Length);
                    }
                }

                _hudFont = new Font(pfc.Families[0], 14, FontStyle.Bold);
                _hud = new HeadsUpDisplay(false)
                {
                    Color = Color.White
                };
            }

            aTimer = new System.Timers.Timer();
            if (timer && !_hosted)
            {
                aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                aTimer.Interval = 10;
                aTimer.Enabled = false;
                _lastTicks = 0;
            }

            _sceneState.Camera.PerspectiveNearPlaneDistance = 0.01 * globeShape.MaximumRadius;
            _sceneState.Camera.PerspectiveFarPlaneDistance = 300.0 * globeShape.MaximumRadius;
            _sceneState.Camera.ZoomToTarget(1.5*globeShape.MaximumRadius);
            renderQueue = new Queue<Action>();

            /*
            var position = globeShape.ToVector3D(Trig.ToRadians(new Geodetic3D(-100, 45, 1000000)));
            var position2 = new Vector3D(11376.75 * 1000.0, 101.8068 * 1000.0, 145.469 * 1000.0);
            AddSpaceCollection(new List<Vector3D>() { position }, "Sat_det", @"c:\Projects\PFD Findings Walker\PFD Findings Walker\Resources\sat.png");
            AddSpaceCollection(new List<Vector3D>() { position2 }, "Sat", @"c:\Projects\PFD Findings Walker\PFD Findings Walker\Resources\sat.png");
            _cone.SetCone(position2, Vector3D.UnitX, 7000*1000 , 2000*1000 );

            */
            
            /*var sh = new Vector2D[4]
            {
                new Vector2D(60.0, 45.2),
                new Vector2D(65.0, 45.2),
                new Vector2D(65.0, 40.2),
                new Vector2D(60.0, 40.2)
            };
            AddPolygonShapeCollection(new List<Vector2D[]>() { sh }, "shape", Color.LightBlue, Color.DarkBlue);*/
            _cones = new List<Cone>();
            _cones2 = new List<Cone2>();
            _coneType = CoverageDisplayMode.FilledCone;

            // GSO arc: ring at GEO altitude in equatorial plane (ECI)
            const double gsoRadius = 42164.0 * 1000.0; // 6378.137 + 35786 km in meters
            const int gsoPoints = 360;
            var gsoArc = new List<Vector3D>(gsoPoints + 1);
            for (int i = 0; i <= gsoPoints; i++)
            {
                double angle = 2.0 * Math.PI * i / gsoPoints;
                gsoArc.Add(new Vector3D(gsoRadius * Math.Cos(angle), gsoRadius * Math.Sin(angle), 0));
            }
            _gsoArc = new PolylineShape2(gsoArc, _context, globeShape,
                new ShapefileAppearance { PolylineColor = Color.Yellow, PolylineOutlineColor = Color.DarkGoldenrod, PolylineWidth = 1, PolylineOutlineWidth = 1 });
            _starField = new StarFieldRenderer(_context);
            Log("GSO arc + rest");
            UpdateHUD();
        }

        public void AddShapeFile(string shapeFile, string iconFile=null)
        {
            ///////////////////////////////////////////////////////////////////

            if (_workerWindow == null)
                _workerWindow = Device.CreateWindow(1, 1);

            _doneQueue.MessageReceived += ProcessNewShapefile;
            _requestQueue.MessageReceived += new ShapefileWorker(_workerWindow.Context, _globe.Shape, _doneQueue).Process;

            // 2ND_EDITION:  Draw order
            if (string.IsNullOrEmpty(iconFile))
                _requestQueue.Post(new ShapefileRequest(shapeFile, new ShapefileAppearance()));
            else
            _requestQueue.Post(new ShapefileRequest(shapeFile, 
                new ShapefileAppearance() { Bitmap = new Bitmap(iconFile) }));

#if SINGLE_THREADED
            _requestQueue.ProcessQueue();
#else
            _requestQueue.StartInAnotherThread();
#endif
            ///////////////////////////////////////////////////////////////////
        }

        public void AddSatelliteMarkers(Vector3D[] points, string key, Color color, float[] scales = null, bool[] highlightMask = null)
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    if (_mutuableshapes.TryGetValue(key, out IRenderable shape))
                        ((IDisposable)shape).Dispose();

                    var markers = new SatelliteMarkerRenderer(points, _context, scales, highlightMask)
                    {
                        Color = color,
                        HighlightColor = Color.LightGreen
                    };
                    if (_mutuableshapes.ContainsKey(key))
                        _mutuableshapes[key] = markers;
                    else
                        _mutuableshapes.Add(key, markers);
                });
            }
        }

        public void AddGroundStationMarkers(Vector3D[] points, string key, Color color, float[] scales = null, float alpha = 0.5f, bool ecef = true)
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    if (_mutuableshapes.TryGetValue(key, out IRenderable shape))
                        ((IDisposable)shape).Dispose();

                    var markers = new GroundStationMarkerRenderer(points, _context, scales)
                    {
                        Color = color,
                        Alpha = alpha
                    };
                    if (_mutuableshapes.ContainsKey(key))
                        _mutuableshapes[key] = markers;
                    else
                        _mutuableshapes.Add(key, markers);
                    if (ecef)
                        _ecefKeys.Add(key);
                    else
                        _ecefKeys.Remove(key);
                });
            }
        }

        public void AddSphereMarkers(Vector3D[] points, string key, Color color, double radius = 200.0, float alpha = 0.7f, double[] radii = null)
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    if (_mutuableshapes.TryGetValue(key, out IRenderable shape))
                        ((IDisposable)shape).Dispose();

                    var markers = new SphereMarkerRenderer(points, _context, radius, radii)
                    {
                        Color = color,
                        Alpha = alpha
                    };
                    if (_mutuableshapes.ContainsKey(key))
                        _mutuableshapes[key] = markers;
                    else
                        _mutuableshapes.Add(key, markers);
                });
            }
        }

        public void AddLineSegments(List<Vector3D> endpoints, string key, Color color, bool ecef = false)
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    if (_mutuableshapes.TryGetValue(key, out IRenderable shape))
                        ((IDisposable)shape).Dispose();

                    var lines = new PolylineShape2(endpoints, _context, _globe.Shape,
                        new ShapefileAppearance { PolylineColor = color, PolylineOutlineColor = color, PolylineWidth = 1, PolylineOutlineWidth = 1 });
                    if (ecef) _ecefKeys.Add(key);
                    if (_mutuableshapes.ContainsKey(key))
                        _mutuableshapes[key] = lines;
                    else
                        _mutuableshapes.Add(key, lines);
                });
            }
        }

        public void RemoveShape(string key)
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    if (_mutuableshapes.TryGetValue(key, out IRenderable shape))
                    {
                        ((IDisposable)shape).Dispose();
                        _mutuableshapes.Remove(key);
                        _ecefKeys.Remove(key);
                    }
                });
            }
        }

        public void AddSpacePolyline(List<Vector3D> points, string key, Color line, Color outLine, bool ecef = true)
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    if (_mutuableshapes.TryGetValue(key, out IRenderable shape))
                        ((IDisposable)shape).Dispose();

                    var spacePoints = new PolylineShape2(points, _context, _globe.Shape,  new ShapefileAppearance() { PolylineColor = line, PolylineOutlineColor = outLine, PolylineWidth = 1, PolylineOutlineWidth = 1 });
                    if (ecef)
                        _ecefKeys.Add(key);
                    else
                        _ecefKeys.Remove(key);
                    if (_mutuableshapes.ContainsKey(key))
                        _mutuableshapes[key] = spacePoints;
                    else
                        _mutuableshapes.Add(key, spacePoints);
                });
            }
        }

        public void OneTimeUpdate(bool displayFootprint, double time, Vector3D[] sat_points, Vector2D[][] shapes, Vector2D[] coord, double[] satheight, double elev_angle)
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    // AddPolygonShapeCollection
                    if (displayFootprint && CoverageRenderMode == CoverageDisplayMode.SuppliedGroundFootprint)
                    {
                        var line = System.Drawing.Color.LightBlue;
                        var outLine = System.Drawing.Color.Blue;
                        var key = "groundCoverage";
                        var groundShapes = new GroundCollectionRenderer(shapes, _context, _globe.Shape,
                            new ShapefileAppearance { PolylineColor = line, PolylineOutlineColor = outLine });
                        if (_mutuableshapes.ContainsKey(key))
                            _mutuableshapes[key] = groundShapes;
                        else
                            _mutuableshapes.Add(key, groundShapes);
                        _ecefKeys.Add(key);
                    }

                    //AddCoverageCones
                    if (CoverageRenderMode == CoverageDisplayMode.OutlinedCone || CoverageRenderMode == CoverageDisplayMode.FilledCone)
                    {
                        var line = System.Drawing.Color.Gray;
                        var fill = System.Drawing.Color.LightBlue;

                        if (_coneType == CoverageDisplayMode.FilledCone)
                            AddHollowConeToQueue(sat_points, coord, satheight, elev_angle, fill);
                        if (_coneType == CoverageDisplayMode.OutlinedCone)
                            AddOutlinedConesToQueue(sat_points, coord, satheight, elev_angle, line, fill);
                    }

                    //AddSpaceCollection
                    var satKey = "satellites";
                    if (_mutuableshapes.TryGetValue(satKey, out IRenderable shape))
                        ((IDisposable)shape).Dispose();

                    var satMarkers = new SatelliteMarkerRenderer(sat_points, _context);
                    if (_mutuableshapes.ContainsKey(satKey))
                        _mutuableshapes[satKey] = satMarkers;
                    else
                        _mutuableshapes.Add(satKey, satMarkers);

                    // HudUpdate
                    if (_currentTicks - _lastTicks > 100)
                    {
                        _lastTicks = _currentTicks;
                        var t = TimeSpan.FromSeconds(_sceneState.TimeInSeconds);
                        elapsedTime = $"{t.Days:D2}d:{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s";
                        UpdateHUD();
                    }

                    _currentTicks++;
                    _sceneState.TimeInSeconds = (int)time;
                });
            }
        }


        public void SetCameraCenterPoint(Vector3D center)
        {
            _camera.CenterPoint = center;
        }

        public void SetCameraPositionBehind(Vector3D position, double zoomOut)
        {
            Vector3D cameraTarget = Vector3D.Zero;
            var cameraPos = new Vector3D(position.X * zoomOut - cameraTarget.X, position.Y * zoomOut - cameraTarget.Y, position.Z * zoomOut - cameraTarget.Z);
            _camera.CenterPoint = (position - cameraTarget).Normalize();
            _camera.Camera.Eye = cameraPos;
        }

        public void AddPolygonShapeCollection(Vector2D[][] shapes, string key, Color line, Color outLine)
        {
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    var groundShapes = new GroundCollectionRenderer(shapes, _context, _globe.Shape, new ShapefileAppearance() { PolylineColor = line, PolylineOutlineColor = outLine });
                    if (_mutuableshapes.ContainsKey(key))
                        _mutuableshapes[key] = groundShapes;
                    else
                        _mutuableshapes.Add(key, groundShapes);
                    _ecefKeys.Add(key);
                });
            }
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            if (_currentTicks - _lastTicks > 100)
            {
                _lastTicks = _currentTicks;
                aTimer.Stop();
                var t = TimeSpan.FromSeconds(_sceneState.TimeInSeconds);
                elapsedTime = $"{t.Days:D2}d:{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s";
                lock (renderQueue)
                {
                    renderQueue.Enqueue(() =>
                    {
                        UpdateHUD();
                    });
                }
                aTimer.Start();
            }
            _sceneState.TimeInSeconds += TIMESTEP;
            _currentTicks++;
        }
        
        private void OnKeyDown(object sender, KeyboardKeyEventArgs e)
        {
            if (e.Key == KeyboardKey.E || e.Key == KeyboardKey.Space)
            {
                if (aTimer.Enabled)
                    aTimer.Stop();
                else
                    aTimer.Start();
                return;
            }
            else
            if (e.Key == KeyboardKey.T)
            {
                _sceneState.TimeInSeconds += 1000;
            }
            else if (e.Key == KeyboardKey.W)
            {
                _wireframe = !_wireframe;
                foreach (var shape in _shapefiles)
                    ((ShapefileRenderer)shape).Wireframe = _wireframe;
            }
            else if (e.Key == KeyboardKey.L)
            {
                _showGrid = !_showGrid;
            }
            else if (e.Key == KeyboardKey.G)
            {
                lock (renderQueue)
                {
                    renderQueue.Enqueue(() =>
                    {
                        _coneType = (CoverageDisplayMode)((((int)_coneType) + 1) % 4);
                    });
                }
            }
            else if (e.Key== KeyboardKey.Escape)
            {
                Close();
            }
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    UpdateHUD();
                });
            }
        }

        public void Close()
        {
            if (aTimer.Enabled)
                aTimer.Stop();
            if (_hosted) return;
            lock (renderQueue)
            {
                renderQueue.Enqueue(() =>
                {
                    _window.Close();
                });
                while (renderQueue.Count > 0)
                {
                    renderQueue.Dequeue().Invoke();
                }
            }
        }

        private void OnKeyUp(object sender, KeyboardKeyEventArgs e)
        {
        }

        private void UpdateHUD()
        {
            if (!_hudEnabled) return;
            string text = $"Time : {elapsedTime}\n\n" +
                $"Move camera around globe:\t\t Move mouse with Left Button down\n" +
                $"Zoom In/Out:\t\t\t\t Move mouse with Right Button down\n" +
                $"Lat/Lon grid ({(_showGrid ? "ON" : "OFF")}):\t\t\t 'l'\n" +
                $"Switch ground coverage ({_coneType}):\t 'g'\nPress 'Esc' to close.";

            if (_hud.Texture != null)
            {
                _hud.Texture.Dispose();
                _hud.Texture = null;
            }

            _hud.Texture = Device.CreateTexture2D(
                Device.CreateBitmapFromText(text, _hudFont),
                TextureFormat.RedGreenBlueAlpha8, false);
        }

        private void OnResize()
        {
            _context.Viewport = new Rectangle(0, 0, _window.Width, _window.Height);
            _sceneState.Camera.AspectRatio = _window.Width / (double)_window.Height;
        }

        /// <summary>
        /// Hosted mode: call from WPF control on resize.
        /// </summary>
        public void HandleResize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            _context.Viewport = new Rectangle(0, 0, width, height);
            _sceneState.Camera.AspectRatio = width / (double)height;
        }

        /// <summary>
        /// Hosted mode: render one frame. Call from WPF GLWpfControl.Render event.
        /// </summary>
        public void RenderOneFrame()
        {
            OnRenderFrame();
        }

        /// <summary>
        /// Expose SceneState for hosted mode camera control.
        /// </summary>
        public SceneState SceneState => _sceneState;

        public bool IsRendering { get => renderQueue != null && renderQueue.Count > 0; }

        private void OnRenderFrame()
        {
            lock (renderQueue)
            {
                while (renderQueue.Count > 0)
                {
                    renderQueue.Dequeue().Invoke();
                }
            }
            _doneQueue.ProcessQueue();

            Context context = _context;
            context.Clear(_clearState);

            // Stars: render first as background (no depth write, no depth test)
            // Center star sphere on camera eye so it never becomes visible as a shell
            if (_showStars)
            {
                var eye = _sceneState.Camera.Eye;
                _sceneState.ModelMatrix = new Matrix4D(
                    1, 0, 0, eye.X,
                    0, 1, 0, eye.Y,
                    0, 0, 1, eye.Z,
                    0, 0, 0, 1);
                _starField.Render(context, _sceneState);
            }

            // ECEF objects: globe, grid, shapefiles, ground stations — all rotate with Earth
            _sceneState.ModelMatrix = Matrix4D.RotationMatrixZ((float)_sceneState.CBRotationAngleRad);
            _globe.Render(context, _sceneState);
            if (_showAtmosphere)
                _atmosphere.Render(context, _sceneState);
            if (_showGrid)
                _gridGlobe.Render(context, _sceneState);
            foreach (IRenderable shapefile in _shapefiles)
                shapefile.Render(context, _sceneState);
            foreach (var shape in _mutuableshapes)
            {
                if (!_ecefKeys.Contains(shape.Key)) continue;
                if (shape.Key.Equals("groundCoverage") && _coneType != CoverageDisplayMode.SuppliedGroundFootprint) continue;
                shape.Value.Render(context, _sceneState);
            }

            // ECI objects: GSO arc, satellites, cones, non-ECEF mutable shapes
            _sceneState.ModelMatrix = Matrix4D.Identity;
            if (_showGsoArc)
                _gsoArc.Render(context, _sceneState);
            foreach (var shape in _mutuableshapes)
            {
                if (_ecefKeys.Contains(shape.Key)) continue;
                shape.Value.Render(context, _sceneState);
            }
            if (_coneType == CoverageDisplayMode.OutlinedCone)
                foreach (var cone in _cones)
                    cone.Render(context, _sceneState);
            if (_coneType == CoverageDisplayMode.FilledCone)
                foreach (var cone in _cones2)
                    cone.Render(context, _sceneState);

            if (_hudEnabled)
                _hud.Render(context, _sceneState);
        }

        public void ProcessNewShapefile(object sender, MessageQueueEventArgs e)
        {
            _shapefiles.Add((IRenderable)e.Message);
        }

        #region IDisposable Members

        public void Dispose()
        {
            foreach (IRenderable shapefile in _shapefiles)
            {
                ((IDisposable)shapefile).Dispose();
            }

            foreach (IRenderable shape in _mutuableshapes.Values)
            {
                ((IDisposable)shape).Dispose();
            }

            _starField.Dispose();
            _gsoArc.Dispose();
            _doneQueue.Dispose();
            _requestQueue.Dispose();
            _gridGlobe.Dispose();
            _atmosphere.Dispose();
            _globe.Dispose();
            _nightTexture?.Dispose();
            _camera?.Dispose();
            _window?.Dispose();
            _workerWindow?.Dispose();
        }

        #endregion

        public void Run(double updateRate)
        {
            _window.Run(updateRate);
        }

        public void SetTime(int timeInSeconds)
        {
            if (_currentTicks - _lastTicks > 100)
            {
                _lastTicks = _currentTicks;
                var t = TimeSpan.FromSeconds(timeInSeconds);
                elapsedTime = $"{t.Days:D2}d:{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s";
                lock (renderQueue)
                {
                    renderQueue.Enqueue(() =>
                    {
                        UpdateHUD();
                    });
                }
            }
            _sceneState.TimeInSeconds = timeInSeconds; 
            _currentTicks++;
        }

        private readonly bool _hosted;
        private readonly Context _context;
        private readonly GraphicsWindow _window;
        private readonly SceneState _sceneState;
        private readonly CameraLookAtPoint _camera;
        private readonly ClearState _clearState;
        private readonly RayCastedGlobe _globe;
        private readonly AtmosphereRenderer _atmosphere;
        private readonly LatitudeLongitudeGridGlobe _gridGlobe;
        private readonly Texture2D _globeTexture;
        private readonly Texture2D _nightTexture;
        private bool _showGrid;
        public bool ShowGrid { get => _showGrid; set => _showGrid = value; }
        public bool ShowDayNight { get => _globe.ShowDayNight; set => _globe.ShowDayNight = value; }
        private bool _showAtmosphere;
        public bool ShowAtmosphere { get => _showAtmosphere; set => _showAtmosphere = value; }
        private bool _showStars = true;
        public bool ShowStars { get => _showStars; set => _showStars = value; }
        private bool _showGsoArc = true;
        public bool ShowGsoArc { get => _showGsoArc; set => _showGsoArc = value; }
        private readonly PolylineShape2 _gsoArc;
        private readonly StarFieldRenderer _starField;

        private readonly IList<IRenderable> _shapefiles = new List<IRenderable>();
        private readonly Dictionary<string, IRenderable> _mutuableshapes = new Dictionary<string, IRenderable>();
        private readonly HashSet<string> _ecefKeys = new HashSet<string>();
        private List<Cone> _cones;
        private List<Cone2> _cones2;

        private readonly MessageQueue _requestQueue = new MessageQueue();
        private readonly MessageQueue _doneQueue = new MessageQueue();

        private GraphicsWindow _workerWindow;

        private readonly Font _hudFont;
        private readonly HeadsUpDisplay _hud;

        private bool _wireframe;
        long _lastTicks, _currentTicks=0;
        System.Timers.Timer aTimer;
        Queue <Action> renderQueue;
        private string elapsedTime;
        private const int TIMESTEP = 30;
        PrivateFontCollection pfc;
        private bool _hudEnabled;


        private CoverageDisplayMode _coneType;

        private static string FindNightTexture(string dayTexturePath)
        {
            string nightPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "NightTexture.jpg");
            if (File.Exists(nightPath)) return nightPath;
            return null;
        }

        internal static string DefaultDayTexturePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "DayTexture.jpg");
    }
    internal class ShapefileRequest
    {
        public ShapefileRequest(string filename, ShapefileAppearance appearance)
        {
            _filename = filename;
            _appearance = appearance;
        }

        public string Filename { get { return _filename; } }
        public ShapefileAppearance Appearance { get { return _appearance; } }
        private string _filename;
        private ShapefileAppearance _appearance;
    }

    internal class ShapefileWorker
    {
        public ShapefileWorker(Context context, Ellipsoid globeShape, MessageQueue doneQueue)
        {
            _context = context;
            _globeShape = globeShape;
            _doneQueue = doneQueue;
        }

        public void Process(object sender, MessageQueueEventArgs e)
        {
#if !SINGLE_THREADED
            _context.MakeCurrent();
#endif

            ShapefileRequest request = (ShapefileRequest)e.Message;
            ShapefileRenderer shapefile = new ShapefileRenderer(
                request.Filename, _context, _globeShape, request.Appearance);

#if !SINGLE_THREADED
            Fence fence = Device.CreateFence();
            while (fence.ClientWait(0) == ClientWaitResult.TimeoutExpired)
            {
                Thread.Sleep(10);   // Other work, etc.
            }
#endif

            _doneQueue.Post(shapefile);
        }

        private readonly Context _context;
        private readonly Ellipsoid _globeShape;
        private readonly MessageQueue _doneQueue;
    }
}