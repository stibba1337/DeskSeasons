using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

namespace DeskSeasons
{
    // Прозрачное окно поверх всего экрана одного монитора: клики проходят насквозь,
    // рисование идёт прямо в DIB-секцию и выводится через UpdateLayeredWindow.
    class Overlay : Form
    {
        public Rectangle Area;
        public Bitmap Bmp;
        public Graphics G;
        IntPtr memDc, hBmp, oldBmp;
        bool excludeCapture;

        public Overlay(Rectangle area)
        {
            Area = area;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "DeskSeasons overlay";
            Allocate();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW |
                              Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyCapture();
        }

        public void SetExcludeFromCapture(bool on)
        {
            excludeCapture = on;
            if (IsHandleCreated) ApplyCapture();
        }

        void ApplyCapture()
        {
            try { Native.SetWindowDisplayAffinity(Handle, excludeCapture ? Native.WDA_EXCLUDEFROMCAPTURE : Native.WDA_NONE); }
            catch { }
        }

        void Allocate()
        {
            var bi = new Native.BITMAPINFOHEADER();
            bi.biSize = 40; bi.biWidth = Area.Width; bi.biHeight = -Area.Height;
            bi.biPlanes = 1; bi.biBitCount = 32;
            IntPtr bits;
            memDc = Native.CreateCompatibleDC(IntPtr.Zero);
            hBmp = Native.CreateDIBSection(memDc, ref bi, 0, out bits, IntPtr.Zero, 0);
            oldBmp = Native.SelectObject(memDc, hBmp);
            Bmp = new Bitmap(Area.Width, Area.Height, Area.Width * 4, PixelFormat.Format32bppPArgb, bits);
            G = Graphics.FromImage(Bmp);
        }

        public void BeginFrame()
        {
            G.ResetTransform();
            G.ResetClip();
            G.CompositingMode = CompositingMode.SourceCopy;
            G.Clear(Color.Transparent);
            G.CompositingMode = CompositingMode.SourceOver;
            G.SmoothingMode = SmoothingMode.AntiAlias;
            G.InterpolationMode = InterpolationMode.Bilinear;
            G.PixelOffsetMode = PixelOffsetMode.Half;
            G.CompositingQuality = CompositingQuality.HighSpeed;
            G.TranslateTransform(-Area.X, -Area.Y);
        }

        public void Present()
        {
            G.Flush();
            var dst = new Native.POINT(Area.X, Area.Y);
            var size = new Native.SIZE(Area.Width, Area.Height);
            var src = new Native.POINT(0, 0);
            var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, memDc, ref src, 0, ref blend, 2);
        }

        public void KeepOnTop()
        {
            if (IsHandleCreated)
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
        }

        protected override void Dispose(bool disposing)
        {
            if (G != null) { G.Dispose(); G = null; }
            if (Bmp != null) { Bmp.Dispose(); Bmp = null; }
            if (memDc != IntPtr.Zero)
            {
                Native.SelectObject(memDc, oldBmp);
                Native.DeleteObject(hBmp);
                Native.DeleteDC(memDc);
                memDc = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }
    }

    class WinInfo
    {
        public IntPtr H;
        public Rectangle R;
        public string Proc;
        public bool Excluded;
    }

    // Перечисляет видимые окна верхнего уровня в порядке Z (сверху вниз).
    static class WindowTracker
    {
        static readonly Dictionary<uint, string> procCache = new Dictionary<uint, string>();
        static DateTime cacheReset = DateTime.Now;
        static readonly StringBuilder sb = new StringBuilder(256);
        static readonly HashSet<string> skipClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
            "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "TaskListThumbnailWnd",
            "MultitaskingViewFrame", "ForegroundStaging", "TopLevelWindowForOverflowXamlIsland"
        };

        public static string ProcessName(IntPtr h)
        {
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            if ((DateTime.Now - cacheReset).TotalSeconds > 30) { procCache.Clear(); cacheReset = DateTime.Now; }
            string name;
            if (procCache.TryGetValue(pid, out name)) return name;
            name = "";
            IntPtr ph = Native.OpenProcess(0x1000, false, pid);
            if (ph != IntPtr.Zero)
            {
                var b = new StringBuilder(1024); int len = b.Capacity;
                if (Native.QueryFullProcessImageName(ph, 0, b, ref len))
                    name = System.IO.Path.GetFileName(b.ToString()).ToLowerInvariant();
                Native.CloseHandle(ph);
            }
            procCache[pid] = name;
            return name;
        }

        public static string ClassOf(IntPtr h)
        {
            sb.Length = 0;
            Native.GetClassName(h, sb, 256);
            return sb.ToString();
        }

        public static Rectangle RectOf(IntPtr h)
        {
            Native.RECT r;
            if (Native.DwmGetWindowAttribute(h, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out r, 16) != 0)
                Native.GetWindowRect(h, out r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        public static List<WinInfo> Enumerate(HashSet<IntPtr> own, Settings s)
        {
            var list = new List<WinInfo>();
            Native.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (own.Contains(h) || !Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                if ((ex & Native.WS_EX_TOOLWINDOW) != 0 && (ex & Native.WS_EX_APPWINDOW) == 0) return true;
                if ((ex & Native.WS_EX_TRANSPARENT) != 0 && (ex & Native.WS_EX_LAYERED) != 0) return true;
                if (Native.GetWindowTextLength(h) == 0) return true;
                int cloaked;
                if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;
                if (skipClasses.Contains(ClassOf(h))) return true;
                var r = RectOf(h);
                if (r.Width < 120 || r.Height < 60) return true;
                var w = new WinInfo { H = h, R = r, Proc = ProcessName(h) };
                w.Excluded = s.IsExcluded(w.Proc);
                list.Add(w);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        // Окно на переднем плане занимает весь монитор (игра, видео, презентация)
        public static bool IsFullscreen(IntPtr fg, Rectangle monitor)
        {
            if (fg == IntPtr.Zero) return false;
            string c = ClassOf(fg);
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
            Native.RECT r;
            if (!Native.GetWindowRect(fg, out r)) return false;
            return r.Left <= monitor.Left && r.Top <= monitor.Top && r.Right >= monitor.Right && r.Bottom >= monitor.Bottom;
        }
    }
}
