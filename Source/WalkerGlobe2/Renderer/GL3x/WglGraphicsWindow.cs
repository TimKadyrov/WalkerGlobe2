using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;

namespace WalkerGlobe2.Renderer.GL3x
{
    /// <summary>
    /// IBindingsContext that resolves GL function pointers via WGL + opengl32.dll,
    /// bypassing GLFW entirely.
    /// </summary>
    internal class WglBindingsContext : OpenTK.IBindingsContext
    {
        [DllImport("opengl32.dll", SetLastError = true)]
        private static extern IntPtr wglGetProcAddress(string lpszProc);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        private static readonly IntPtr _opengl32 = LoadLibrary("opengl32.dll");

        public IntPtr GetProcAddress(string procName)
        {
            var addr = wglGetProcAddress(procName);
            if (addr == IntPtr.Zero || addr == (IntPtr)1 || addr == (IntPtr)2)
                addr = GetProcAddress(_opengl32, procName);
            return addr;
        }
    }

    /// <summary>
    /// A standalone OpenGL window using WinForms + raw WGL, bypassing GLFW entirely.
    /// This avoids the ~20s startup penalty of GLFW's GameWindow on some systems.
    /// </summary>
    internal class WglGraphicsWindow : GraphicsWindow
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
        private const byte PFD_TYPE_RGBA      = 0;
        private const byte PFD_MAIN_PLANE     = 0;

        public WglGraphicsWindow(int width, int height, string title, WindowType windowType)
        {
            if (width < 0) throw new ArgumentOutOfRangeException("width");
            if (height < 0) throw new ArgumentOutOfRangeException("height");

            // Create WinForms form with a Panel for GL rendering
            _form = new Form
            {
                Text = title ?? "",
                ClientSize = new Size(Math.Max(width, 1), Math.Max(height, 1)),
                StartPosition = FormStartPosition.CenterScreen,
            };

            if (windowType == WindowType.FullScreen)
            {
                _form.FormBorderStyle = FormBorderStyle.None;
                _form.WindowState = FormWindowState.Maximized;
            }

            _panel = new Panel { Dock = DockStyle.Fill };
            _form.Controls.Add(_panel);

            // Force handle creation so we can get HDC
            _form.Show();
            if (string.IsNullOrEmpty(title))
                _form.Visible = false;

            _hdc = GetDC(_panel.Handle);

            // Set pixel format
            var pfd = new PIXELFORMATDESCRIPTOR
            {
                nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>(),
                nVersion = 1,
                dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
                iPixelType = PFD_TYPE_RGBA,
                cColorBits = 32,
                cDepthBits = 24,
                cStencilBits = 8,
                iLayerType = PFD_MAIN_PLANE,
            };

            int pixelFormat = ChoosePixelFormat(_hdc, ref pfd);
            if (pixelFormat == 0)
                throw new InvalidOperationException("ChoosePixelFormat failed");
            if (!SetPixelFormat(_hdc, pixelFormat, ref pfd))
                throw new InvalidOperationException("SetPixelFormat failed");

            // Create and activate WGL context
            _hglrc = wglCreateContext(_hdc);
            if (_hglrc == IntPtr.Zero)
                throw new InvalidOperationException("wglCreateContext failed");
            if (!wglMakeCurrent(_hdc, _hglrc))
                throw new InvalidOperationException("wglMakeCurrent failed");

            // Load OpenTK GL bindings via WGL (no GLFW needed)
            GL.LoadBindings(new WglBindingsContext());

            FinalizerThreadContextGL3x.Initialize();

            _form.Resize += (s, e) => OnResize();

            _context = new ContextGL3x(_panel.ClientSize.Width, _panel.ClientSize.Height, MakeCurrent);
            _mouse = new MouseWgl(_panel);
            _keyboard = new KeyboardWgl(_form);
        }

        public void MakeCurrent()
        {
            wglMakeCurrent(_hdc, _hglrc);
        }

        public override void Run(double updateRate)
        {
            double targetFrameTime = 1.0 / updateRate;
            var timer = Stopwatch.StartNew();
            double accumulator = 0;

            _running = true;
            OnResize();
            while (_running && !_form.IsDisposed)
            {
                Application.DoEvents();
                if (_form.IsDisposed) break;

                double elapsed = timer.Elapsed.TotalSeconds;
                timer.Restart();
                accumulator += elapsed;

                // Update at fixed rate
                while (accumulator >= targetFrameTime)
                {
                    OnUpdateFrame();
                    accumulator -= targetFrameTime;
                }

                // Render every iteration
                OnPreRenderFrame();
                OnRenderFrame();
                OnPostRenderFrame();
                SwapBuffers(_hdc);
            }
        }

        public override void Close()
        {
            _running = false;
        }

        public override Context Context => _context;
        public override int Width => _panel.ClientSize.Width;
        public override int Height => _panel.ClientSize.Height;
        public override Mouse Mouse => _mouse;
        public override Keyboard Keyboard => _keyboard;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                if (_hglrc != IntPtr.Zero)
                {
                    wglDeleteContext(_hglrc);
                    _hglrc = IntPtr.Zero;
                }
                _panel?.Dispose();
                _form?.Dispose();
            }
            base.Dispose(disposing);
        }

        private Form _form;
        private Panel _panel;
        private IntPtr _hdc;
        private IntPtr _hglrc;
        private ContextGL3x _context;
        private MouseWgl _mouse;
        private KeyboardWgl _keyboard;
        private bool _running;
    }

    /// <summary>
    /// Mouse input adapter for WinForms Panel.
    /// </summary>
    internal class MouseWgl : Mouse
    {
        public MouseWgl(Control control)
        {
            control.MouseDown += (s, e) =>
            {
                MouseButton button;
                if (e.Button == MouseButtons.Left) button = MouseButton.Left;
                else if (e.Button == MouseButtons.Middle) button = MouseButton.Middle;
                else if (e.Button == MouseButtons.Right) button = MouseButton.Right;
                else return;
                OnButtonDown(new Point(e.X, e.Y), button);
            };
            control.MouseUp += (s, e) =>
            {
                MouseButton button;
                if (e.Button == MouseButtons.Left) button = MouseButton.Left;
                else if (e.Button == MouseButtons.Middle) button = MouseButton.Middle;
                else if (e.Button == MouseButtons.Right) button = MouseButton.Right;
                else return;
                OnButtonUp(new Point(e.X, e.Y), button);
            };
            control.MouseMove += (s, e) =>
            {
                OnMove(new Point(e.X, e.Y));
            };
        }
    }

    /// <summary>
    /// Keyboard input adapter for WinForms Form.
    /// </summary>
    internal class KeyboardWgl : Keyboard
    {
        public KeyboardWgl(Form form)
        {
            form.KeyPreview = true;
            form.KeyDown += (s, e) => OnKeyDown(WinFormsToWalkerGlobe(e.KeyCode));
            form.KeyUp += (s, e) => OnKeyUp(WinFormsToWalkerGlobe(e.KeyCode));
        }

        private static KeyboardKey WinFormsToWalkerGlobe(System.Windows.Forms.Keys key)
        {
            return key switch
            {
                System.Windows.Forms.Keys.LShiftKey => KeyboardKey.ShiftLeft,
                System.Windows.Forms.Keys.RShiftKey => KeyboardKey.ShiftRight,
                System.Windows.Forms.Keys.LControlKey => KeyboardKey.ControlLeft,
                System.Windows.Forms.Keys.RControlKey => KeyboardKey.ControlRight,
                System.Windows.Forms.Keys.LMenu => KeyboardKey.AltLeft,
                System.Windows.Forms.Keys.RMenu => KeyboardKey.AltRight,
                System.Windows.Forms.Keys.LWin => KeyboardKey.WinLeft,
                System.Windows.Forms.Keys.RWin => KeyboardKey.WinRight,
                System.Windows.Forms.Keys.Apps => KeyboardKey.Menu,
                System.Windows.Forms.Keys.F1 => KeyboardKey.F1,
                System.Windows.Forms.Keys.F2 => KeyboardKey.F2,
                System.Windows.Forms.Keys.F3 => KeyboardKey.F3,
                System.Windows.Forms.Keys.F4 => KeyboardKey.F4,
                System.Windows.Forms.Keys.F5 => KeyboardKey.F5,
                System.Windows.Forms.Keys.F6 => KeyboardKey.F6,
                System.Windows.Forms.Keys.F7 => KeyboardKey.F7,
                System.Windows.Forms.Keys.F8 => KeyboardKey.F8,
                System.Windows.Forms.Keys.F9 => KeyboardKey.F9,
                System.Windows.Forms.Keys.F10 => KeyboardKey.F10,
                System.Windows.Forms.Keys.F11 => KeyboardKey.F11,
                System.Windows.Forms.Keys.F12 => KeyboardKey.F12,
                System.Windows.Forms.Keys.Up => KeyboardKey.Up,
                System.Windows.Forms.Keys.Down => KeyboardKey.Down,
                System.Windows.Forms.Keys.Left => KeyboardKey.Left,
                System.Windows.Forms.Keys.Right => KeyboardKey.Right,
                System.Windows.Forms.Keys.Enter => KeyboardKey.Enter,
                System.Windows.Forms.Keys.Escape => KeyboardKey.Escape,
                System.Windows.Forms.Keys.Space => KeyboardKey.Space,
                System.Windows.Forms.Keys.Tab => KeyboardKey.Tab,
                System.Windows.Forms.Keys.Back => KeyboardKey.Backspace,
                System.Windows.Forms.Keys.Insert => KeyboardKey.Insert,
                System.Windows.Forms.Keys.Delete => KeyboardKey.Delete,
                System.Windows.Forms.Keys.PageUp => KeyboardKey.PageUp,
                System.Windows.Forms.Keys.PageDown => KeyboardKey.PageDown,
                System.Windows.Forms.Keys.Home => KeyboardKey.Home,
                System.Windows.Forms.Keys.End => KeyboardKey.End,
                System.Windows.Forms.Keys.CapsLock => KeyboardKey.CapsLock,
                System.Windows.Forms.Keys.Scroll => KeyboardKey.ScrollLock,
                System.Windows.Forms.Keys.PrintScreen => KeyboardKey.PrintScreen,
                System.Windows.Forms.Keys.Pause => KeyboardKey.Pause,
                System.Windows.Forms.Keys.NumLock => KeyboardKey.NumLock,
                System.Windows.Forms.Keys.NumPad0 => KeyboardKey.Keypad0,
                System.Windows.Forms.Keys.NumPad1 => KeyboardKey.Keypad1,
                System.Windows.Forms.Keys.NumPad2 => KeyboardKey.Keypad2,
                System.Windows.Forms.Keys.NumPad3 => KeyboardKey.Keypad3,
                System.Windows.Forms.Keys.NumPad4 => KeyboardKey.Keypad4,
                System.Windows.Forms.Keys.NumPad5 => KeyboardKey.Keypad5,
                System.Windows.Forms.Keys.NumPad6 => KeyboardKey.Keypad6,
                System.Windows.Forms.Keys.NumPad7 => KeyboardKey.Keypad7,
                System.Windows.Forms.Keys.NumPad8 => KeyboardKey.Keypad8,
                System.Windows.Forms.Keys.NumPad9 => KeyboardKey.Keypad9,
                System.Windows.Forms.Keys.Divide => KeyboardKey.KeypadDivide,
                System.Windows.Forms.Keys.Multiply => KeyboardKey.KeypadMultiply,
                System.Windows.Forms.Keys.Subtract => KeyboardKey.KeypadMinus,
                System.Windows.Forms.Keys.Add => KeyboardKey.KeypadPlus,
                System.Windows.Forms.Keys.Decimal => KeyboardKey.KeypadDecimal,
                System.Windows.Forms.Keys.A => KeyboardKey.A,
                System.Windows.Forms.Keys.B => KeyboardKey.B,
                System.Windows.Forms.Keys.C => KeyboardKey.C,
                System.Windows.Forms.Keys.D => KeyboardKey.D,
                System.Windows.Forms.Keys.E => KeyboardKey.E,
                System.Windows.Forms.Keys.F => KeyboardKey.F,
                System.Windows.Forms.Keys.G => KeyboardKey.G,
                System.Windows.Forms.Keys.H => KeyboardKey.H,
                System.Windows.Forms.Keys.I => KeyboardKey.I,
                System.Windows.Forms.Keys.J => KeyboardKey.J,
                System.Windows.Forms.Keys.K => KeyboardKey.K,
                System.Windows.Forms.Keys.L => KeyboardKey.L,
                System.Windows.Forms.Keys.M => KeyboardKey.M,
                System.Windows.Forms.Keys.N => KeyboardKey.N,
                System.Windows.Forms.Keys.O => KeyboardKey.O,
                System.Windows.Forms.Keys.P => KeyboardKey.P,
                System.Windows.Forms.Keys.Q => KeyboardKey.Q,
                System.Windows.Forms.Keys.R => KeyboardKey.R,
                System.Windows.Forms.Keys.S => KeyboardKey.S,
                System.Windows.Forms.Keys.T => KeyboardKey.T,
                System.Windows.Forms.Keys.U => KeyboardKey.U,
                System.Windows.Forms.Keys.V => KeyboardKey.V,
                System.Windows.Forms.Keys.W => KeyboardKey.W,
                System.Windows.Forms.Keys.X => KeyboardKey.X,
                System.Windows.Forms.Keys.Y => KeyboardKey.Y,
                System.Windows.Forms.Keys.Z => KeyboardKey.Z,
                System.Windows.Forms.Keys.D0 => KeyboardKey.Number0,
                System.Windows.Forms.Keys.D1 => KeyboardKey.Number1,
                System.Windows.Forms.Keys.D2 => KeyboardKey.Number2,
                System.Windows.Forms.Keys.D3 => KeyboardKey.Number3,
                System.Windows.Forms.Keys.D4 => KeyboardKey.Number4,
                System.Windows.Forms.Keys.D5 => KeyboardKey.Number5,
                System.Windows.Forms.Keys.D6 => KeyboardKey.Number6,
                System.Windows.Forms.Keys.D7 => KeyboardKey.Number7,
                System.Windows.Forms.Keys.D8 => KeyboardKey.Number8,
                System.Windows.Forms.Keys.D9 => KeyboardKey.Number9,
                System.Windows.Forms.Keys.Oemtilde => KeyboardKey.Tilde,
                System.Windows.Forms.Keys.OemMinus => KeyboardKey.Minus,
                System.Windows.Forms.Keys.Oemplus => KeyboardKey.Plus,
                System.Windows.Forms.Keys.OemOpenBrackets => KeyboardKey.BracketLeft,
                System.Windows.Forms.Keys.OemCloseBrackets => KeyboardKey.BracketRight,
                System.Windows.Forms.Keys.OemSemicolon => KeyboardKey.Semicolon,
                System.Windows.Forms.Keys.OemQuotes => KeyboardKey.Quote,
                System.Windows.Forms.Keys.Oemcomma => KeyboardKey.Comma,
                System.Windows.Forms.Keys.OemPeriod => KeyboardKey.Period,
                System.Windows.Forms.Keys.OemQuestion => KeyboardKey.Slash,
                System.Windows.Forms.Keys.OemBackslash => KeyboardKey.Backslash,
                _ => KeyboardKey.Unknown
            };
        }
    }
}
