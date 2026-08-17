using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class Chart_PieChart : Control
    {
        private readonly ToolTip _toolTip;
        private readonly List<ChartHitArea> _hitAreas;
        private readonly ContextMenuStrip _contextMenu;
        private FileSystemEntry _entry;
        private FileSystemEntry _contextMenuEntry;
        private string _currentToolTipText;

        public Chart_PieChart()
        {
            DoubleBuffered = true;
            _toolTip = new ToolTip();
            _hitAreas = new List<ChartHitArea>();

            _contextMenu = new ContextMenuStrip();

            ToolStripMenuItem openInExplorerItem =
                new ToolStripMenuItem(
                    LocalizationService.GetText(
                        "Context.OpenInExplorer"));

            openInExplorerItem.Click +=
                OpenInExplorerItem_Click;

            _contextMenu.Items.Add(
                openInExplorerItem);

            AntdThemeService.ConfigureContextMenu(
                _contextMenu);
        }

        public void SetEntry(FileSystemEntry entry)
        {
            _entry = entry;
            _currentToolTipText = null;
            _toolTip.SetToolTip(this, string.Empty);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            string toolTipText = string.Empty;

            foreach (ChartHitArea hitArea in _hitAreas)
            {
                if (hitArea.Contains(e.Location))
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Right)
                return;

            _contextMenuEntry = null;

            foreach (ChartHitArea hitArea in _hitAreas)
            {
                if (!hitArea.Contains(e.Location))
                    continue;

                _contextMenuEntry = hitArea.Entry;
                break;
            }

            if (_contextMenuEntry == null)
                return;

            _contextMenu.Show(
                this,
                e.Location);
        }

        private void OpenInExplorerItem_Click(
            object sender,
            EventArgs e)
        {
            if (_contextMenuEntry == null ||
                string.IsNullOrWhiteSpace(
                    _contextMenuEntry.FullPath))
            {
                return;
            }

            string targetPath =
                _contextMenuEntry.FullPath;

            if (Directory.Exists(targetPath))
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "\"" + targetPath + "\"",
                        UseShellExecute = true
                    });

                return;
            }

            if (!File.Exists(targetPath))
                return;

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments =
                        "/select,\"" + targetPath + "\"",
                    UseShellExecute = true
                });
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

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            if (_entry == null || _entry.Children.Count == 0)
            {
                DrawEmptyText(e.Graphics);
                return;
            }

            List<ChartItem> chartItems = CreateChartItems(_entry);

            if (chartItems.Count == 0)
            {
                DrawEmptyText(e.Graphics);
                return;
            }

            long totalSize = chartItems.Sum(item => item.SizeBytes);

            if (totalSize <= 0)
            {
                DrawEmptyText(e.Graphics);
                return;
            }

            int chartLeft = ScaleForDpi(24);
            int chartTop = ScaleForDpi(24);
            int chartLegendGap = ScaleForDpi(24);
            int rightPadding = ScaleForDpi(8);

            int legendWidth = CalculateLegendWidth(
                chartItems,
                totalSize);

            int availableChartWidth =
                Width -
                chartLeft -
                chartLegendGap -
                legendWidth -
                rightPadding;

            int availableChartHeight =
                Height -
                (chartTop * 2);

            int chartSize = Math.Max(
                0,
                Math.Min(
                    availableChartWidth,
                    availableChartHeight));

            Rectangle chartBounds = new Rectangle(
                chartLeft,
                chartTop,
                chartSize,
                chartSize);

            float startAngle = -90F;

            for (int index = 0; index < chartItems.Count; index++)
            {
                ChartItem item = chartItems[index];
                float sweepAngle = (float)((double)item.SizeBytes * 360D / totalSize);

                Color segmentColor =
                    AntdThemeService.GetChartFamilyColor(
                        index);

                Color segmentGradientTopColor =
                    AntdThemeService.LightenChartColor(
                        segmentColor,
                        AntdThemeService.ChartFamilyGradientTopFactor);

                Color segmentGradientBottomColor =
                    AntdThemeService.DarkenChartColor(
                        segmentColor,
                        AntdThemeService.ChartFamilyGradientBottomFactor);

                using (GraphicsPath segmentPath =
                       new GraphicsPath())
                {
                    segmentPath.AddPie(
                        chartBounds,
                        startAngle,
                        sweepAngle);

                    RectangleF segmentBounds =
                        segmentPath.GetBounds();

                    using (LinearGradientBrush brush =
                           new LinearGradientBrush(
                               segmentBounds,
                               segmentGradientTopColor,
                               segmentGradientBottomColor,
                               LinearGradientMode.Vertical))
                    {
                        e.Graphics.FillPath(
                            brush,
                            segmentPath);
                    }
                }

                if (item.Entry != null)
                {
                    _hitAreas.Add(new ChartHitArea(chartBounds, startAngle, sweepAngle, item.Entry));
                }

                startAngle += sweepAngle;
            }

            using Pen borderPen = new Pen(ForeColor, 1);
            e.Graphics.DrawEllipse(borderPen, chartBounds);

            DrawLegend(
                e.Graphics,
                chartItems,
                totalSize,
                chartBounds.Right + chartLegendGap,
                chartTop);
        }

        private int CalculateLegendWidth(
            List<ChartItem> chartItems,
            long totalSize)
        {
            int requiredWidth = 0;

            foreach (ChartItem item in chartItems)
            {
                string text = string.Format(
                    LocalizationService.GetText("Chart.ItemLabel"),
                    item.Name,
                    SizeFormatter.Format(item.SizeBytes),
                    (double)item.SizeBytes * 100D / totalSize);

                Size textSize = TextRenderer.MeasureText(
                    text,
                    Font,
                    Size.Empty,
                    TextFormatFlags.SingleLine);

                requiredWidth = Math.Max(
                    requiredWidth,
                    textSize.Width +
                    ScaleForDpi(30));
            }

            int maximumWidth = Math.Max(
                0,
                Width / 2);

            return Math.Min(
                requiredWidth,
                maximumWidth);
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

        private List<ChartItem> CreateChartItems(FileSystemEntry entry)
        {
            List<ChartItem> items = entry.Children
                .Where(child => child.SizeBytes > 0)
                .OrderByDescending(child => child.SizeBytes)
                .Take(10)
                .Select(child => new ChartItem(child.Name, child.SizeBytes, child))
                .ToList();

            long topSize = items.Sum(item => item.SizeBytes);
            long totalSize = entry.Children.Sum(child => child.SizeBytes);
            long otherSize = totalSize - topSize;

            if (otherSize > 0)
            {
                items.Add(new ChartItem(LocalizationService.GetText("Chart.Other"), otherSize, null));
            }

            return items;
        }

        private void DrawLegend(Graphics graphics, List<ChartItem> chartItems, long totalSize, int left, int top)
        {
            int y = top;

            for (int index = 0; index < chartItems.Count; index++)
            {
                ChartItem item = chartItems[index];

                using SolidBrush colorBrush =
                    new SolidBrush(
                        AntdThemeService.GetChartFamilyColor(
                            index));
                int legendMarkerSize =
                    ScaleForDpi(14);
                int legendMarkerTop =
                    y + ScaleForDpi(3);

                graphics.FillRectangle(
                    colorBrush,
                    left,
                    legendMarkerTop,
                    legendMarkerSize,
                    legendMarkerSize);

                string text = string.Format(
                    LocalizationService.GetText("Chart.ItemLabel"),
                    item.Name,
                    SizeFormatter.Format(item.SizeBytes),
                    (double)item.SizeBytes * 100D / totalSize);

                int legendRowHeight = Math.Max(
                    Font.Height + ScaleForDpi(4),
                    ScaleForDpi(22));

                Rectangle legendBounds = new Rectangle(
                    left,
                    y,
                    Math.Max(
                        0,
                        Width -
                        left -
                        ScaleForDpi(8)),
                    legendRowHeight);

                if (item.Entry != null)
                {
                    _hitAreas.Add(new ChartHitArea(legendBounds, item.Entry));
                }

                TextRenderer.DrawText(
                    graphics,
                    text,
                    Font,
                    new Rectangle(
                        left + ScaleForDpi(22),
                        y,
                        Math.Max(
                            0,
                            Width -
                            left -
                            ScaleForDpi(30)),
                        legendRowHeight),
                    ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                y += Math.Max(
                    legendRowHeight + ScaleForDpi(2),
                    ScaleForDpi(24));
            }
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
                    if (!Directory.Exists(entry.FullPath))
                        return string.Empty;

                    creationTime = Directory.GetCreationTime(entry.FullPath);
                    lastWriteTime = Directory.GetLastWriteTime(entry.FullPath);
                    lastAccessTime = Directory.GetLastAccessTime(entry.FullPath);
                }
                else
                {
                    if (!File.Exists(entry.FullPath))
                        return string.Empty;

                    creationTime = File.GetCreationTime(entry.FullPath);
                    lastWriteTime = File.GetLastWriteTime(entry.FullPath);
                    lastAccessTime = File.GetLastAccessTime(entry.FullPath);
                }

                return string.Format(
                    LocalizationService.GetText("Chart.PieTooltip"),
                    entry.FullPath,
                    Environment.NewLine,
                    creationTime,
                    lastWriteTime,
                    lastAccessTime);
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class ChartItem
        {
            public ChartItem(string name, long sizeBytes, FileSystemEntry entry)
            {
                Name = name;
                SizeBytes = sizeBytes;
                Entry = entry;
            }

            public string Name { get; }
            public long SizeBytes { get; }
            public FileSystemEntry Entry { get; }
        }

        private sealed class ChartHitArea
        {
            private readonly Rectangle _bounds;
            private readonly float _startAngle;
            private readonly float _sweepAngle;
            private readonly bool _isPieSlice;

            public ChartHitArea(Rectangle bounds, FileSystemEntry entry)
            {
                _bounds = bounds;
                Entry = entry;
            }

            public ChartHitArea(Rectangle bounds, float startAngle, float sweepAngle, FileSystemEntry entry)
            {
                _bounds = bounds;
                _startAngle = NormalizeAngle(startAngle);
                _sweepAngle = sweepAngle;
                _isPieSlice = true;
                Entry = entry;
            }

            public FileSystemEntry Entry { get; }

            public bool Contains(Point point)
            {
                if (!_bounds.Contains(point))
                    return false;

                if (!_isPieSlice)
                    return true;

                double radiusX = _bounds.Width / 2D;
                double radiusY = _bounds.Height / 2D;

                if (radiusX <= 0D || radiusY <= 0D)
                    return false;

                double centerX = _bounds.Left + radiusX;
                double centerY = _bounds.Top + radiusY;
                double normalizedX = (point.X - centerX) / radiusX;
                double normalizedY = (point.Y - centerY) / radiusY;

                if ((normalizedX * normalizedX) + (normalizedY * normalizedY) > 1D)
                    return false;

                double angle = Math.Atan2(point.Y - centerY, point.X - centerX) * 180D / Math.PI;
                angle = NormalizeAngle((float)angle);

                double endAngle = NormalizeAngle(_startAngle + _sweepAngle);

                if (_sweepAngle >= 360F)
                    return true;

                if (_startAngle <= endAngle)
                    return angle >= _startAngle && angle <= endAngle;

                return angle >= _startAngle || angle <= endAngle;
            }

            private static float NormalizeAngle(float angle)
            {
                while (angle < 0F)
                {
                    angle += 360F;
                }

                while (angle >= 360F)
                {
                    angle -= 360F;
                }

                return angle;
            }
        }
    }
}