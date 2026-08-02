using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using UptimeWidget.Items;
using UptimeWidget.Models;

namespace UptimeWidget
{
    /// <summary>
    /// Borderless, always-on-top floating window that renders a vertical list of
    /// widget items. Uses a layered window (UpdateLayeredWindow) with an owner-drawn
    /// ARGB bitmap so the background can be translucent independently of the text:
    /// the background is filled with <see cref="AppSettings.BackgroundOpacity"/> alpha
    /// while text is drawn fully opaque, and the whole-window
    /// <see cref="AppSettings.Opacity"/> is applied as the layered constant alpha.
    /// </summary>
    public sealed class WidgetForm : Form
    {
        private readonly Dictionary<string, string> _texts = [];
        private readonly Dictionary<string, TimeSpan> _elapsedSinceRefresh = [];
        private IReadOnlyList<IWidgetItem> _items = Array.Empty<IWidgetItem>();
        private readonly System.Windows.Forms.Timer _timer;
        private TimeSpan _tickInterval = TimeSpan.FromMilliseconds(1000);

        private Color _backColor = Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E);
        private Color _foreColor = Color.White;
        private string _fontFamily = "Segoe UI";
        private float _fontSize = 10f;
        private double _opacity = 0.85;
        private double _backgroundOpacity = 0.85;
        private bool _alwaysOnTop = true;

        private static readonly Padding ContentPadding = new(8, 6, 8, 6);
        private const int LineSpacing = 2;
        private const int ColumnGap = 16;
        private const string EmptyPlaceholderText = "No items selected — see Settings…";

        private bool _dragging;
        private Point _dragStartCursor;
        private Point _dragStartLocation;
        private bool _hovered;

        // Hover affordance: a dashed, semi-transparent white/gray outline shown when the
        // mouse is over an unlocked widget so its bounds are visible even at 0 opacity.
        private static readonly Color HoverBorderColor = Color.FromArgb(160, 200, 200, 200);
        private const float HoverBorderWidth = 1f;
        private const int HoverBorderRadius = 10;

        /// <summary>Raised after a drag completes, with the final persisted location.</summary>
        public event Action<Point>? LocationPersisted;

        /// <summary>When true, the widget cannot be moved by dragging and is click-through.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool PositionLocked
        {
            get;
            set
            {
                field = value;
                ApplyClickThrough();
                if (IsHandleCreated)
                {
                    Render();
                }
            }
        }

        public WidgetForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;

            HookDrag(this);

            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += OnTimerTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED;
                if (PositionLocked)
                {
                    cp.ExStyle |= WS_EX_TRANSPARENT;
                }
                return cp;
            }
        }

        /// <summary>Toggles the WS_EX_TRANSPARENT extended style so a locked widget is click-through.</summary>
        private void ApplyClickThrough()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            exStyle = PositionLocked
                ? exStyle | WS_EX_TRANSPARENT
                : exStyle & ~WS_EX_TRANSPARENT;
            _ = SetWindowLong(Handle, GWL_EXSTYLE, exStyle);
        }

        /// <summary>Starts the refresh timer using the settings' tick interval.</summary>
        public void StartRefresh(AppSettings settings)
        {
            _tickInterval = TimeSpan.FromMilliseconds(Math.Max(100, settings.UpdateIntervalMs));
            _timer.Interval = (int)_tickInterval.TotalMilliseconds;
            _timer.Start();
        }

        /// <summary>Stops the refresh timer.</summary>
        public void StopRefresh()
        {
            _timer.Stop();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            bool changed = false;
            foreach (IWidgetItem item in _items)
            {
                TimeSpan elapsed = _elapsedSinceRefresh.TryGetValue(item.Id, out TimeSpan prev)
                    ? prev + _tickInterval
                    : _tickInterval;

                if (elapsed >= item.RefreshInterval)
                {
                    _texts[item.Id] = item.GetDisplayText();
                    _elapsedSinceRefresh[item.Id] = TimeSpan.Zero;
                    changed = true;
                }
                else
                {
                    _elapsedSinceRefresh[item.Id] = elapsed;
                }
            }

            if (changed)
            {
                Render();
            }
        }

        /// <summary>Wires mouse events so dragging anywhere on the widget moves it.</summary>
        private void HookDrag(Control control)
        {
            control.MouseDown += OnDragMouseDown;
            control.MouseMove += OnDragMouseMove;
            control.MouseUp += OnDragMouseUp;
            control.MouseEnter += OnHoverEnter;
            control.MouseLeave += OnHoverLeave;
        }

        private void OnHoverEnter(object? sender, EventArgs e)
        {
            if (_hovered)
            {
                return;
            }

            _hovered = true;
            Render();
        }

        private void OnHoverLeave(object? sender, EventArgs e)
        {
            if (!_hovered)
            {
                return;
            }

            _hovered = false;
            Render();
        }

        private void OnDragMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || PositionLocked)
            {
                return;
            }

            _dragging = true;
            _dragStartCursor = Cursor.Position;
            _dragStartLocation = Location;
        }

        private void OnDragMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }

            Point cursor = Cursor.Position;
            int dx = cursor.X - _dragStartCursor.X;
            int dy = cursor.Y - _dragStartCursor.Y;
            Point desired = new(_dragStartLocation.X + dx, _dragStartLocation.Y + dy);
            Location = ClampToWorkingArea(desired);
        }

        private void OnDragMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_dragging)
            {
                return;
            }

            _dragging = false;
            LocationPersisted?.Invoke(Location);
        }

        /// <summary>
        /// Rebuilds the item list from the given enabled items (in order) and applies
        /// the visual settings.
        /// </summary>
        public void BuildItems(IReadOnlyList<IWidgetItem> items, AppSettings settings)
        {
            _items = items;
            _texts.Clear();
            _elapsedSinceRefresh.Clear();
            foreach (IWidgetItem item in items)
            {
                _texts[item.Id] = item.GetDisplayText();
            }

            ApplyAppearance(settings);
        }

        /// <summary>Updates the visible text for a single item.</summary>
        public void UpdateItemText(string id, string text)
        {
            if (_texts.ContainsKey(id))
            {
                _texts[id] = text;
                Render();
            }
        }

        /// <summary>
        /// Positions the widget. Uses the saved location if present and still on the
        /// primary screen; otherwise anchors to the bottom-right of the primary screen's
        /// working area with an 8px inset.
        /// </summary>
        public void ApplyPosition(AppSettings settings)
        {
            Rectangle wa = Screen.PrimaryScreen?.WorkingArea
                ?? Screen.GetWorkingArea(this);

            if (settings.PositionX is int sx && settings.PositionY is int sy)
            {
                Location = ClampToWorkingArea(new Point(sx, sy), wa);
            }
            else
            {
                const int inset = 8;
                int x = wa.Right - Width - inset;
                int y = wa.Bottom - Height - inset;
                Location = ClampToWorkingArea(new Point(x, y), wa);
            }
        }

        /// <summary>Keeps the widget fully within the primary screen working area.</summary>
        public Point ClampToWorkingArea(Point desired, Rectangle? workingArea = null)
        {
            Rectangle wa = workingArea
                ?? Screen.PrimaryScreen?.WorkingArea
                ?? Screen.GetWorkingArea(this);

            int maxX = Math.Max(wa.Left, wa.Right - Width);
            int maxY = Math.Max(wa.Top, wa.Bottom - Height);
            int x = Math.Clamp(desired.X, wa.Left, maxX);
            int y = Math.Clamp(desired.Y, wa.Top, maxY);
            return new Point(x, y);
        }

        /// <summary>Applies opacities, colors, font, and always-on-top from settings, then renders.</summary>
        public void ApplyAppearance(AppSettings settings)
        {
            _opacity = Math.Clamp(settings.Opacity, 0.1, 1.0);
            _backgroundOpacity = Math.Clamp(settings.BackgroundOpacity, 0.0, 1.0);
            _backColor = Color.FromArgb(settings.BackColorArgb);
            _foreColor = Color.FromArgb(settings.ForeColorArgb);
            _fontFamily = settings.FontFamily;
            _fontSize = settings.FontSize;
            TopMost = settings.AlwaysOnTop;
            _alwaysOnTop = settings.AlwaysOnTop;

            // Setting TopMost = false does not immediately drop the window out of the
            // topmost z-order band; it lingers above other windows until something else
            // activates. Explicitly demote it so unchecking "always on top" takes effect now.
            DemoteIfNotOnTop();

            Render();
        }

        /// <summary>
        /// Sends the widget to the bottom of the z-order when "always on top" is disabled,
        /// so it does not cover the currently active window. Safe to call before or after the
        /// handle exists; it re-applies on <see cref="OnShown"/> for the startup case where the
        /// window is not yet created/activated when settings are first applied.
        /// </summary>
        private void DemoteIfNotOnTop()
        {
            if (IsHandleCreated && !_alwaysOnTop)
            {
                _ = SetWindowPos(
                    Handle, HWND_BOTTOM, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            DemoteIfNotOnTop();
        }

        private Font BuildFont()
        {
            return BuildFont(FontStyle.Regular);
        }

        private Font BuildFont(FontStyle style)
        {
            try
            {
                return new Font(_fontFamily, _fontSize, style);
            }
            catch
            {
                return new Font(FontFamily.GenericSansSerif, _fontSize, style);
            }
        }

        /// <summary>
        /// Composes the widget content (translucent background + opaque text) into a
        /// 32-bpp ARGB bitmap and pushes it to the layered window.
        /// </summary>
        private void Render()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            using Font font = BuildFont();
            using Font valueFont = BuildFont(FontStyle.Bold);

            // Measure lines to size the window. Each line is split into a left "name"
            // column and a right "value" column (separated by the first ": ").
            List<(string name, string value, Size nameSize, Size valueSize)> lines = [];
            int nameColWidth = 0;
            int valueColWidth = 0;
            int contentHeight = 0;
            using (Bitmap measureBmp = new(1, 1))
            using (Graphics mg = Graphics.FromImage(measureBmp))
            {
                mg.TextRenderingHint = TextRenderingHint.AntiAlias;
                foreach (IWidgetItem item in _items)
                {
                    string text = _texts.TryGetValue(item.Id, out string? t) ? t : string.Empty;
                    (string name, string value) = SplitColumns(text);

                    SizeF nameSz = mg.MeasureString(name, font);
                    SizeF valueSz = mg.MeasureString(value, valueFont);
                    Size nameSize = new((int)Math.Ceiling(nameSz.Width), (int)Math.Ceiling(nameSz.Height));
                    Size valueSize = new((int)Math.Ceiling(valueSz.Width), (int)Math.Ceiling(valueSz.Height));

                    lines.Add((name, value, nameSize, valueSize));
                    nameColWidth = Math.Max(nameColWidth, nameSize.Width);
                    valueColWidth = Math.Max(valueColWidth, valueSize.Width);
                    contentHeight += Math.Max(nameSize.Height, valueSize.Height);
                }

                if (lines.Count == 0)
                {
                    SizeF sz = mg.MeasureString(EmptyPlaceholderText, font);
                    Size size = new((int)Math.Ceiling(sz.Width), (int)Math.Ceiling(sz.Height));
                    lines.Add((EmptyPlaceholderText, string.Empty, size, Size.Empty));
                    nameColWidth = Math.Max(nameColWidth, size.Width);
                    contentHeight += size.Height;
                }
            }

            int gap = valueColWidth > 0 ? ColumnGap : 0;
            int contentWidth = nameColWidth + gap + valueColWidth;

            if (lines.Count > 1)
            {
                contentHeight += LineSpacing * (lines.Count - 1);
            }

            int width = Math.Max(1, contentWidth + ContentPadding.Horizontal);
            int height = Math.Max(1, contentHeight + ContentPadding.Vertical);

            using Bitmap bmp = new(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = TextRenderingHint.AntiAlias;

                // Clamp to a minimum alpha of 1 so the background never becomes fully
                // transparent. Layered windows hit-test on the alpha channel, so alpha 0
                // pixels are click-through (not draggable). Alpha 1 is visually
                // indistinguishable from 0 but keeps the whole widget interactive.
                int bgAlpha = Math.Max(1, (int)Math.Round(_backgroundOpacity * 255));
                using (SolidBrush bgBrush = new(Color.FromArgb(bgAlpha, _backColor)))
                {
                    g.FillRectangle(bgBrush, 0, 0, width, height);
                }

                int y = ContentPadding.Top;
                int valueRight = ContentPadding.Left + contentWidth;
                using SolidBrush textBrush = new(Color.FromArgb(255, _foreColor));
                foreach ((string name, string value, Size nameSize, Size valueSize) in lines)
                {
                    // Name column: left-aligned.
                    g.DrawString(name, font, textBrush, ContentPadding.Left, y);

                    // Value column: right-aligned to the content's right edge.
                    if (value.Length > 0)
                    {
                        g.DrawString(value, valueFont, textBrush, valueRight - valueSize.Width, y);
                    }

                    y += Math.Max(nameSize.Height, valueSize.Height) + LineSpacing;
                }

                // Hover affordance: outline the widget bounds when hovered and unlocked so
                // the user can see its area/size even if the background/elements are at 0 opacity.
                if (_hovered && !PositionLocked)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using Pen borderPen = new(HoverBorderColor, HoverBorderWidth)
                    {
                        DashStyle = DashStyle.Dash,
                        Alignment = PenAlignment.Inset,
                    };
                    using GraphicsPath path = BuildRoundedRect(
                        new Rectangle(0, 0, width - 1, height - 1), HoverBorderRadius);
                    g.DrawPath(borderPen, path);
                }
            }

            Size = new Size(width, height);
            PushBitmap(bmp);
        }

        /// <summary>
        /// Splits an item's display text into a left "name" column and a right "value"
        /// column at the last ": " separator. Using the last separator keeps display
        /// names that themselves contain ": " intact on the name side, since the value
        /// is always the trailing metric text. When no separator is present the whole
        /// text is treated as the name and the value column is empty.
        /// </summary>
        private static (string name, string value) SplitColumns(string text)
        {
            int idx = text.LastIndexOf(": ", StringComparison.Ordinal);
            if (idx < 0)
            {
                return (text, string.Empty);
            }

            string name = text[..idx];
            string value = text[(idx + 2)..];
            return (name, value);
        }

        /// <summary>
        /// Builds a rounded-rectangle path. The corner radius is clamped so it never
        /// exceeds half of the smaller side (degrading to a full pill/ellipse when large).
        /// </summary>
        private static GraphicsPath BuildRoundedRect(Rectangle bounds, int radius)
        {
            int r = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            GraphicsPath path = new();
            if (r <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int d = r * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Pushes the composed bitmap to the layered window with the whole-window opacity.</summary>
        private void PushBitmap(Bitmap bmp)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                // GetHbitmap with a transparent background preserves premultiplied alpha.
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                SIZE size = new() { cx = bmp.Width, cy = bmp.Height };
                POINT pointSource = new() { x = 0, y = 0 };
                POINT topPos = new() { x = Left, y = Top };

                BLENDFUNCTION blend = new()
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = (byte)Math.Round(_opacity * 255),
                    AlphaFormat = AC_SRC_ALPHA,
                };

                _ = UpdateLayeredWindow(
                    Handle, screenDc, ref topPos, ref size, memDc,
                    ref pointSource, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                _ = ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    _ = SelectObject(memDc, oldBitmap);
                    _ = DeleteObject(hBitmap);
                }
                _ = DeleteDC(memDc);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Render();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_QUERYENDSESSION = 0x0011;
            if (m.Msg == WM_QUERYENDSESSION)
            {
                // Windows / the installer's Restart Manager is asking us to close.
                // Agree and let the application exit so the process terminates fully.
                Application.Exit();
            }

            base.WndProc(ref m);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            // Layered windows must be repositioned via UpdateLayeredWindow to move the surface.
            if (IsHandleCreated)
            {
                Render();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        // --- Native interop for layered window compositing ---

        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int GWL_EXSTYLE = -20;
        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        private static readonly IntPtr HWND_NOTOPMOST = new(-2);
        private static readonly IntPtr HWND_BOTTOM = new(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
