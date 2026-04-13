using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;
using OpenTK.Graphics.OpenGL;
using WalkerGlobe2.Renderer.GL3x;

namespace WalkerGlobe2.Api
{
    /// <summary>
    /// WPF UserControl that hosts an OpenGL globe renderer via WGL + WindowsFormsHost.
    /// No GLFW dependency.
    /// </summary>
    public partial class GlobeWpfControl : UserControl, IDisposable
    {
        [DllImport("opengl32.dll", SetLastError = true)]
        private static extern IntPtr wglCreateContext(IntPtr hdc);

        [DllImport("opengl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

        [DllImport("opengl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool wglDeleteContext(IntPtr hglrc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR ppfd);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR ppfd);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SwapBuffers(IntPtr hdc);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize;
            public ushort nVersion;
            public uint dwFlags;
            public byte iPixelType;
            public byte cColorBits;
            public byte cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift;
            public byte cAlphaBits, cAlphaShift;
            public byte cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits;
            public byte cDepthBits;
            public byte cStencilBits;
            public byte cAuxBuffers;
            public byte iLayerType;
            public byte bReserved;
            public uint dwLayerMask, dwVisibleMask, dwDamageMask;
        }

        private const uint PFD_DRAW_TO_WINDOW = 0x00000004;
        private const uint PFD_SUPPORT_OPENGL = 0x00000020;
        private const uint PFD_DOUBLEBUFFER   = 0x00000001;

        private WalkerGlobe2.WalkerGlobe _globe;
        private Context _context;
        private bool _initialized;
        private Bitmap _preloadedTexture;
        private volatile bool _textureReady;


        private System.Windows.Forms.Panel _glPanel;
        private IntPtr _hdc;
        private IntPtr _hglrc;

        // Mouse orbit control state
        private bool _leftDrag;
        private bool _rightDrag;
        private System.Windows.Point _lastMouse;

        public GlobeWpfControl()
        {
            InitializeComponent();

            _glPanel = new System.Windows.Forms.Panel { Dock = System.Windows.Forms.DockStyle.Fill };
            WfHost.Child = _glPanel;

            // Initialize WGL context once the panel handle is ready
            _glPanel.HandleCreated += (s, e) => InitializeWgl();

            // Mouse handling for camera orbit — must be on the WinForms panel,
            // because WindowsFormsHost routes input to the hosted control.
            _glPanel.MouseDown += OnWfMouseDown;
            _glPanel.MouseUp += OnWfMouseUp;
            _glPanel.MouseMove += OnWfMouseMove;
            _glPanel.MouseWheel += OnWfMouseWheel;

            // Render on WPF composition
            CompositionTarget.Rendering += OnCompositionRender;

            // Preload texture on background thread
            PreloadTextureAsync();
        }

        /// <summary>
        /// Path to the globe texture file. Must be set before the control loads.
        /// </summary>
        public string TextureFile { get; set; }

        /// <summary>
        /// Fired once the GL context and globe renderer are ready.
        /// </summary>
        public event Action GlobeReady;

        private void InitializeWgl()
        {
            _hdc = GetDC(_glPanel.Handle);

            var pfd = new PIXELFORMATDESCRIPTOR
            {
                nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>(),
                nVersion = 1,
                dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
                iPixelType = 0, // PFD_TYPE_RGBA
                cColorBits = 32,
                cDepthBits = 24,
                cStencilBits = 8,
                iLayerType = 0, // PFD_MAIN_PLANE
            };

            int pixelFormat = ChoosePixelFormat(_hdc, ref pfd);
            if (pixelFormat == 0)
                throw new InvalidOperationException("ChoosePixelFormat failed");
            if (!SetPixelFormat(_hdc, pixelFormat, ref pfd))
                throw new InvalidOperationException("SetPixelFormat failed");

            _hglrc = wglCreateContext(_hdc);
            if (_hglrc == IntPtr.Zero)
                throw new InvalidOperationException("wglCreateContext failed");
            if (!wglMakeCurrent(_hdc, _hglrc))
                throw new InvalidOperationException("wglMakeCurrent failed");

            GL.LoadBindings(new WglBindingsContext());
        }

        private void MakeCurrent()
        {
            wglMakeCurrent(_hdc, _hglrc);
        }

        private void PreloadTextureAsync()
        {
            Task.Run(() =>
            {
                try
                {
                    string textureFile = TextureFile;
                    if (string.IsNullOrEmpty(textureFile))
                    {
                        textureFile = WalkerGlobe2.WalkerGlobe.DefaultDayTexturePath;
                    }

                    _preloadedTexture = new Bitmap(textureFile);
                    _textureReady = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Texture preload failed: {ex}");
                    _textureReady = true;
                }
            });
        }

        private void InitializeGlobe()
        {
            int w = _glPanel.ClientSize.Width;
            int h = _glPanel.ClientSize.Height;
            if (w <= 0) w = 800;
            if (h <= 0) h = 600;

            MakeCurrent();
            _context = Device.CreateContext(w, h, MakeCurrent);
            _globe = new WalkerGlobe2.WalkerGlobe(_preloadedTexture, _context, w, h);
            _preloadedTexture = null;
            _initialized = true;

            GlobeReady?.Invoke();
        }

        private void OnCompositionRender(object sender, EventArgs e)
        {
            if (_hglrc == IntPtr.Zero) return;

            if (!_initialized)
            {
                if (!_textureReady) return;

                try
                {
                    InitializeGlobe();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Globe init failed: {ex}");
                }
                return;
            }

            MakeCurrent();

            int w = _glPanel.ClientSize.Width;
            int h = _glPanel.ClientSize.Height;
            if (w > 0 && h > 0)
                _globe.HandleResize(w, h);

            _globe.RenderOneFrame();
            SwapBuffers(_hdc);
        }

        #region Mouse orbit camera (WinForms events on the GL panel)

        private void OnWfMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                _leftDrag = true;
                _lastMouse = new System.Windows.Point(e.X, e.Y);
                _glPanel.Capture = true;
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                _rightDrag = true;
                _lastMouse = new System.Windows.Point(e.X, e.Y);
                _glPanel.Capture = true;
            }
        }

        private void OnWfMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            _leftDrag = false;
            _rightDrag = false;
            _glPanel.Capture = false;
        }

        private void OnWfMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_initialized) return;
            double dx = e.X - _lastMouse.X;
            double dy = e.Y - _lastMouse.Y;
            _lastMouse = new System.Windows.Point(e.X, e.Y);

            var cam = _globe.SceneState.Camera;

            if (_leftDrag)
            {
                double sensitivity = 0.005;
                double azimuth = dx * sensitivity;
                double elevation = dy * sensitivity;

                var eye = cam.Eye;
                var target = cam.Target;
                var diff = eye - target;
                double dist = diff.Magnitude;

                double theta = Math.Atan2(diff.Y, diff.X) - azimuth;
                double phi = Math.Acos(diff.Z / dist);
                phi = Math.Max(0.01, Math.Min(Math.PI - 0.01, phi - elevation));

                cam.Eye = new Vector3D(
                    target.X + dist * Math.Sin(phi) * Math.Cos(theta),
                    target.Y + dist * Math.Sin(phi) * Math.Sin(theta),
                    target.Z + dist * Math.Cos(phi));
            }
            else if (_rightDrag)
            {
                double sensitivity = 0.01;
                double factor = 1.0 + dy * sensitivity;
                var eye = cam.Eye;
                var target = cam.Target;
                var diff = eye - target;
                cam.Eye = target + diff * factor;
            }
        }

        private void OnWfMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_initialized) return;
            var cam = _globe.SceneState.Camera;
            double factor = e.Delta > 0 ? 0.9 : 1.1;
            var diff = cam.Eye - cam.Target;
            cam.Eye = cam.Target + diff * factor;
        }

        #endregion

        #region Public API (delegating to WalkerGlobe)

        public void SetTime(double timeSeconds)
        {
            if (!_initialized) return;
            _globe.SetTime((int)timeSeconds);
        }

        public void UpdateSatellites(string key, Vector3D[] positionsKm, System.Drawing.Color color,
            float[] scales = null, bool[] highlighted = null)
        {
            if (!_initialized) return;
            var positionsM = new Vector3D[positionsKm.Length];
            for (int i = 0; i < positionsKm.Length; i++)
                positionsM[i] = positionsKm[i] * 1000.0;
            _globe.AddSatelliteMarkers(positionsM, key, color, scales, highlighted);
        }

        public void UpdateGroundStations(string key, Vector3D[] positionsKm, System.Drawing.Color color,
            float[] scales = null, float alpha = 0.5f, bool ecef = true)
        {
            if (!_initialized) return;
            var positionsM = new Vector3D[positionsKm.Length];
            for (int i = 0; i < positionsKm.Length; i++)
                positionsM[i] = positionsKm[i] * 1000.0;
            _globe.AddGroundStationMarkers(positionsM, key, color, scales, alpha, ecef);
        }

        public void SetSpacePolyline(string key, Vector3D[] positionsKm, System.Drawing.Color color, bool ecef = true)
        {
            if (!_initialized) return;
            var points = new System.Collections.Generic.List<Vector3D>();
            foreach (var p in positionsKm)
                points.Add(p * 1000.0);
            _globe.AddSpacePolyline(points, key, color, color, ecef);
        }

        public void SetGroundPolygons(string key, Vector2D[][] latLonRad,
            System.Drawing.Color lineColor, System.Drawing.Color fillColor)
        {
            if (!_initialized) return;
            _globe.AddPolygonShapeCollection(latLonRad, key, lineColor, fillColor);
        }

        public void SetLineSegments(string key, Vector3D[] endpointsKm, System.Drawing.Color color, bool ecef = false)
        {
            if (!_initialized) return;
            var points = new System.Collections.Generic.List<Vector3D>();
            foreach (var p in endpointsKm)
                points.Add(p * 1000.0);
            _globe.AddLineSegments(points, key, color, ecef);
        }

        public void SetSphereMarkers(string key, Vector3D[] positionsKm, System.Drawing.Color color,
            double radiusKm = 120.0, float alpha = 0.7f)
        {
            if (!_initialized) return;
            var positionsM = new Vector3D[positionsKm.Length];
            for (int i = 0; i < positionsKm.Length; i++)
                positionsM[i] = positionsKm[i] * 1000.0;
            _globe.AddSphereMarkers(positionsM, key, color, radiusKm * 1000.0, alpha);
        }

        public void RemoveShape(string key)
        {
            if (!_initialized) return;
            _globe.RemoveShape(key);
        }

        public bool ShowGrid
        {
            get => _globe?.ShowGrid ?? false;
            set { if (_initialized) _globe.ShowGrid = value; }
        }

        public bool ShowDayNight
        {
            get => _globe?.ShowDayNight ?? false;
            set { if (_initialized) _globe.ShowDayNight = value; }
        }

        public bool ShowAtmosphere
        {
            get => _globe?.ShowAtmosphere ?? false;
            set { if (_initialized) _globe.ShowAtmosphere = value; }
        }

        public bool ShowGsoArc
        {
            get => _globe?.ShowGsoArc ?? true;
            set { if (_initialized) _globe.ShowGsoArc = value; }
        }

        public CoverageDisplayMode CoverageRenderMode
        {
            get => _globe?.CoverageRenderMode ?? CoverageDisplayMode.FilledCone;
            set { if (_initialized) _globe.CoverageRenderMode = value; }
        }

        public WalkerGlobe2.WalkerGlobe Globe => _globe;

        public WalkerGlobe2.Renderer.SceneState SceneState => _globe?.SceneState;

        #endregion

        public void Dispose()
        {
            CompositionTarget.Rendering -= OnCompositionRender;
            _globe?.Dispose();

            if (_hglrc != IntPtr.Zero)
            {
                wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                wglDeleteContext(_hglrc);
                _hglrc = IntPtr.Zero;
            }
            _glPanel?.Dispose();
        }
    }
}
