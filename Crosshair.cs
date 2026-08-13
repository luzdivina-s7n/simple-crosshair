using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Crosshair
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var m = new System.Threading.Mutex(true, "Local\\Crosshair_App", out createdNew))
            {
                if (!createdNew) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { SetProcessDPIAware(); } catch { }
                Application.Run(new OverlayForm());
            }
        }

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();
    }

    class OverlayForm : Form
    {
        private const int WinSize = 64;

        private NotifyIcon _tray;
        private ToolStripMenuItem _small;
        private ToolStripMenuItem _medium;
        private int _dotSize = 4;

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Width = WinSize;
            Height = WinSize;
            CenterOverScreen();

            _small = new ToolStripMenuItem("Small", null, (s, e) => SetSize(4));
            _medium = new ToolStripMenuItem("Medium", null, (s, e) => SetSize(6));

            var menu = new ContextMenuStrip();
            menu.Items.Add(_small);
            menu.Items.Add(_medium);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, OnExit);

            _tray = new NotifyIcon();
            _tray.Icon = LoadAppIcon();
            _tray.Text = "Crosshair";
            _tray.Visible = true;
            _tray.ContextMenuStrip = menu;

            Icon = LoadAppIcon();
            SetSize(4);
        }

        private void CenterOverScreen()
        {
            Rectangle b = Screen.PrimaryScreen.Bounds;
            Left = b.Left + (b.Width - Width) / 2;
            Top = b.Top + (b.Height - Height) / 2;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20;
                cp.ExStyle |= 0x80000;
                cp.ExStyle |= 0x80;
                return cp;
            }
        }

        private void SetSize(int size)
        {
            _dotSize = size;
            _small.Checked = size == 4;
            _medium.Checked = size == 6;
            RedrawLayered();
        }

        private void RedrawLayered()
        {
            if (!IsHandleCreated) return;

            using (Bitmap bmp = new Bitmap(WinSize, WinSize, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Transparent);
                float d = _dotSize;
                RectangleF r = new RectangleF((WinSize - d) / 2f, (WinSize - d) / 2f, d, d);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(r);
                    using (PathGradientBrush pgb = new PathGradientBrush(path))
                    {
                        pgb.CenterColor = Color.White;
                        pgb.SurroundColors = new Color[] { Color.FromArgb(255, 190, 190, 190) };
                        g.FillPath(pgb, path);
                    }
                }

                IntPtr screenDc = GetDC(IntPtr.Zero);
                IntPtr memDc = CreateCompatibleDC(screenDc);
                IntPtr hBitmap = IntPtr.Zero;
                IntPtr oldBitmap = IntPtr.Zero;
                try
                {
                    hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                    oldBitmap = SelectObject(memDc, hBitmap);

                    Point ptDst = new Point { X = Left, Y = Top };
                    SIZE sz = new SIZE { X = WinSize, Y = WinSize };
                    Point ptSrc = new Point { X = 0, Y = 0 };
                    BLENDFUNCTION blend = new BLENDFUNCTION
                    {
                        BlendOp = AC_SRC_OVER,
                        SourceConstantAlpha = 255,
                        AlphaFormat = AC_SRC_ALPHA
                    };

                    UpdateLayeredWindow(Handle, screenDc, ref ptDst, ref sz, memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);
                }
                finally
                {
                    if (hBitmap != IntPtr.Zero) { SelectObject(memDc, oldBitmap); DeleteObject(hBitmap); }
                    if (memDc != IntPtr.Zero) DeleteDC(memDc);
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RedrawLayered();
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            if (IsHandleCreated) RedrawLayered();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x007E)
            {
                CenterOverScreen();
                RedrawLayered();
            }
            base.WndProc(ref m);
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                using (System.IO.Stream s = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("Crosshair.App.ico"))
                    if (s != null) return new Icon(s);
            }
            catch { }
            return SystemIcons.Application;
        }

        private void OnExit(object sender, EventArgs e)
        {
            _tray.Visible = false;
            _tray.Dispose();
            Close();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            base.Dispose(disposing);
        }

        private const byte AC_SRC_OVER = 0;
        private const byte AC_SRC_ALPHA = 1;
        private const uint ULW_ALPHA = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref SIZE psize, IntPtr hdcSrc, ref Point pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}