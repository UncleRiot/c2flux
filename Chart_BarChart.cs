using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class Chart_BarChart : Control
    {
        private static readonly Color[] ChartColors =
{
    Color.FromArgb(102, 192, 244),
    Color.FromArgb(244, 159, 67),
    Color.FromArgb(120, 220, 140),
    Color.FromArgb(190, 140, 255),
    Color.FromArgb(255, 120, 120),
    Color.FromArgb(120, 210, 210),
    Color.FromArgb(255, 210, 90),
    Color.FromArgb(170, 190, 255),
    Color.FromArgb(210, 160, 120),
    Color.FromArgb(150, 220, 180),
    Color.FromArgb(220, 150, 210)
};

        private const int SHGFI_ICON = 0x000000100;
        private const int SHGFI_SMALLICON = 0x000000001;
        private const int SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const int FILE_ATTRIBUTE_NORMAL = 0x00000080;

        private readonly ToolTip _toolTip;
        private readonly List<ChartHitArea> _hitAreas;
        private readonly Dictionary<string, Bitmap> _systemIconCache;
        private FileSystemEntry _entry;
        private string _currentToolTipText;
        private int _barHeight = 14;

        public int BarHeight
        {
            get => _barHeight;
            set
            {
                int normalizedValue = Math.Max(5, Math.Min(30, value));

                if (_barHeight == normalizedValue)
                    return;

                _barHeight = normalizedValue;
                Invalidate();
            }
        }

        public Chart_BarChart()
        {
            DoubleBuffered = true;
            _toolTip = new ToolTip();
            _hitAreas = new List<ChartHitArea>();
            _systemIconCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            UpdateStyles();
        }

        public void SetEntry(FileSystemEntry entry)
        {
            _entry = entry;
            _currentToolTipText = null;
            _toolTip.SetToolTip(this, string.Empty);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();

                foreach (Bitmap bitmap in _systemIconCache.Values)
                {
                    bitmap.Dispose();
                }

                _systemIconCache.Clear();
            }

            base.Dispose(disposing);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            string toolTipText = string.Empty;

            foreach (ChartHitArea hitArea in _hitAreas)
            {
                if (hitArea.Bounds.Contains(e.Location))
                {
                    toolTipText = FormatFileSystemDateToolTip(hitArea.Entry);
                    break;
                }
            }

            if (_currentToolTipText == toolTipText)
                return;

            _currentToolTipText = toolTipText;
            _toolTip.SetToolTip(this, toolTipText);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            _currentToolTipText = null;
            _toolTip.SetToolTip(this, string.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            _hitAreas.Clear();

            Rectangle visibleBounds = GetVisibleClientRectangle();

            if (visibleBounds.IsEmpty)
                return;

            int visibleWidth = visibleBounds.Width;
            int visibleHeight = visibleBounds.Height;

            if (visibleWidth <= 0 || visibleHeight <= 0)
                return;

            e.Graphics.SetClip(visibleBounds);
            e.Graphics.Clear(BackColor);

            if (_entry == null || _entry.Children.Count == 0)
            {
                DrawEmptyText(e.Graphics);
                return;
            }

            List<FileSystemEntry> items = _entry.Children
                .Where(child => child.SizeBytes > 0)
                .OrderByDescending(child => child.SizeBytes)
                .Take(18)
                .ToList();

            if (items.Count == 0)
            {
                DrawEmptyText(e.Graphics);
                return;
            }

            long maxSize = items.Max(item => item.SizeBytes);

            if (maxSize <= 0)
            {
                DrawEmptyText(e.Graphics);
                return;
            }

            int leftMargin = ScaleForDpi(20);
            int rightMargin = ScaleForDpi(20);
            int topMargin = ScaleForDpi(18);
            int labelToBarGap = ScaleForDpi(20);
            int barHeight = ScaleForDpi(BarHeight);
            int rowHeight = Math.Max(
                Font.Height + ScaleForDpi(6),
                barHeight + ScaleForDpi(7));
            int iconSize = ScaleForDpi(16);
            int iconToTextGap = ScaleForDpi(6);
            int textPaddingLeft = ScaleForDpi(10);
            int textGapRightOfBar = ScaleForDpi(8);

            int contentLeft = visibleBounds.Left + leftMargin;
            int contentRight = visibleBounds.Right - rightMargin;

            if (contentRight <= contentLeft)
                return;

            int longestLabelWidth = 0;

            foreach (FileSystemEntry item in items)
            {
                Size labelSize = TextRenderer.MeasureText(e.Graphics, item.Name, Font);
                longestLabelWidth = Math.Max(longestLabelWidth, iconSize + iconToTextGap + labelSize.Width);
            }

            int maximumLabelWidth = Math.Max(100, Math.Min(260, visibleWidth / 3));
            int labelWidth = Math.Min(longestLabelWidth, maximumLabelWidth);

            int barLeft = contentLeft + labelWidth + labelToBarGap;
            int maximumBarWidth = contentRight - barLeft;

            if (maximumBarWidth <= 0)
                return;

            for (int index = 0; index < items.Count; index++)
            {
                FileSystemEntry item = items[index];
                int y = visibleBounds.Top + topMargin + index * rowHeight;

                if (y + rowHeight > visibleBounds.Bottom)
                    break;

                string sizeText = SizeFormatter.Format(item.SizeBytes);

                Rectangle labelBounds = new Rectangle(contentLeft, y, labelWidth, rowHeight);
                _hitAreas.Add(new ChartHitArea(labelBounds, item));

                Rectangle iconBounds = new Rectangle(
                    labelBounds.Left,
                    y + Math.Max(0, (rowHeight - iconSize) / 2),
                    iconSize,
                    iconSize);

                Bitmap systemIcon = GetSystemIcon(item);

                if (systemIcon != null)
                {
                    e.Graphics.DrawImage(systemIcon, iconBounds);
                }

                Rectangle labelTextBounds = new Rectangle(
                    labelBounds.Left + iconSize + iconToTextGap,
                    labelBounds.Top,
                    Math.Max(0, labelBounds.Width - iconSize - iconToTextGap),
                    labelBounds.Height);

                TextRenderer.DrawText(
                    e.Graphics,
                    item.Name,
                    Font,
                    labelTextBounds,
                    ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                int barWidth = (int)Math.Round(maximumBarWidth * ((double)item.SizeBytes / maxSize));
                barWidth = Math.Max(1, Math.Min(maximumBarWidth, barWidth));

                Rectangle barBounds = new Rectangle(
                    barLeft,
                    y + (rowHeight - barHeight) / 2,
                    barWidth,
                    barHeight);

                _hitAreas.Add(new ChartHitArea(barBounds, item));

                using SolidBrush barBrush = new SolidBrush(ChartColors[index % ChartColors.Length]);
                e.Graphics.FillRectangle(barBrush, barBounds);

                Size sizeTextSize = TextRenderer.MeasureText(e.Graphics, sizeText, Font);
                bool drawTextInsideBar = barBounds.Width >= sizeTextSize.Width + textPaddingLeft;

                Rectangle textBounds;

                if (drawTextInsideBar)
                {
                    textBounds = new Rectangle(
                        barBounds.Left + textPaddingLeft,
                        y,
                        Math.Max(0, barBounds.Width - textPaddingLeft),
                        rowHeight);
                }
                else
                {
                    textBounds = new Rectangle(
                        barBounds.Right + textGapRightOfBar,
                        y,
                        Math.Max(0, contentRight - barBounds.Right - textGapRightOfBar),
                        rowHeight);
                }

                _hitAreas.Add(new ChartHitArea(textBounds, item));

                TextRenderer.DrawText(
                    e.Graphics,
                    sizeText,
                    Font,
                    textBounds,
                    ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            Invalidate();

            if (Parent != null)
            {
                Parent.Invalidate(true);
            }
        }

        private void Parent_SizeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private int ScaleForDpi(
            int logicalPixels)
        {
            int deviceDpi = DeviceDpi <= 0
                ? 96
                : DeviceDpi;

            return Math.Max(
                1,
                (int)Math.Round(
                    logicalPixels *
                    deviceDpi /
                    96D));
        }

        private Rectangle GetVisibleClientRectangle()
        {
            Rectangle visibleScreenRectangle = RectangleToScreen(ClientRectangle);

            Control parent = Parent;

            while (parent != null)
            {
                visibleScreenRectangle = Rectangle.Intersect(
                    visibleScreenRectangle,
                    parent.RectangleToScreen(parent.ClientRectangle));

                parent = parent.Parent;
            }

            if (visibleScreenRectangle.Width <= 0 || visibleScreenRectangle.Height <= 0)
            {
                return Rectangle.Empty;
            }

            Point localLocation = PointToClient(visibleScreenRectangle.Location);

            return new Rectangle(
                localLocation.X,
                localLocation.Y,
                visibleScreenRectangle.Width,
                visibleScreenRectangle.Height);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);

            if (Parent != null)
            {
                Parent.SizeChanged -= Parent_SizeChanged;
                Parent.SizeChanged += Parent_SizeChanged;
            }

            Invalidate();
        }

        private void DrawEmptyText(Graphics graphics)
        {
            TextRenderer.DrawText(
                graphics,
                LocalizationService.GetText("Chart.NoData"),
                Font,
                ClientRectangle,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private Bitmap GetSystemIcon(FileSystemEntry entry)
        {
            if (entry == null)
                return null;

            string cacheKey = entry.IsDirectory
                ? LocalizationService.GetText("Chart.Directory")
                : LocalizationService.GetText("Chart.FilePrefix") + Path.GetExtension(entry.Name);

            if (_systemIconCache.TryGetValue(cacheKey, out Bitmap cachedBitmap))
            {
                return cachedBitmap;
            }

            SHFILEINFO shellFileInfo = new SHFILEINFO();

            string iconPath = entry.IsDirectory
                ? entry.FullPath
                : entry.FullPath;

            int fileAttributes = entry.IsDirectory
                ? FILE_ATTRIBUTE_DIRECTORY
                : FILE_ATTRIBUTE_NORMAL;

            IntPtr result = SHGetFileInfo(
                iconPath,
                fileAttributes,
                ref shellFileInfo,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(SHFILEINFO)),
                SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);

            if (result == IntPtr.Zero || shellFileInfo.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                using Icon icon = (Icon)Icon.FromHandle(shellFileInfo.hIcon).Clone();
                Bitmap bitmap = icon.ToBitmap();
                _systemIconCache[cacheKey] = bitmap;
                return bitmap;
            }
            finally
            {
                DestroyIcon(shellFileInfo.hIcon);
            }
        }

        private string FormatFileSystemDateToolTip(FileSystemEntry entry)
        {
            if (entry == null)
                return string.Empty;

            if (string.IsNullOrWhiteSpace(entry.FullPath))
                return string.Empty;

            try
            {
                DateTime creationTime;
                DateTime lastWriteTime;
                DateTime lastAccessTime;

                if (entry.IsDirectory)
                {
                    if (!System.IO.Directory.Exists(entry.FullPath))
                        return string.Empty;

                    creationTime = System.IO.Directory.GetCreationTime(entry.FullPath);
                    lastWriteTime = System.IO.Directory.GetLastWriteTime(entry.FullPath);
                    lastAccessTime = System.IO.Directory.GetLastAccessTime(entry.FullPath);
                }
                else
                {
                    if (!System.IO.File.Exists(entry.FullPath))
                        return string.Empty;

                    creationTime = System.IO.File.GetCreationTime(entry.FullPath);
                    lastWriteTime = System.IO.File.GetLastWriteTime(entry.FullPath);
                    lastAccessTime = System.IO.File.GetLastAccessTime(entry.FullPath);
                }

                return string.Format(
                    LocalizationService.GetText("Chart.TooltipDates"),
                    creationTime,
                    Environment.NewLine,
                    lastWriteTime,
                    lastAccessTime);
            }
            catch
            {
                return string.Empty;
            }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            int dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            int uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private sealed class ChartHitArea
        {
            public ChartHitArea(Rectangle bounds, FileSystemEntry entry)
            {
                Bounds = bounds;
                Entry = entry;
            }

            public Rectangle Bounds { get; }
            public FileSystemEntry Entry { get; }
        }
    }
}