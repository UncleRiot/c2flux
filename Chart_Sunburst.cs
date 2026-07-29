using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class Chart_Sunburst : Control
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

        private readonly ToolTip _toolTip;
        private readonly List<SunburstHitArea> _hitAreas;
        private FileSystemEntry _entry;
        private string _currentToolTipText;
        private int _depth;
        private int _maxItems;

        public Chart_Sunburst()
        {
            DoubleBuffered = true;
            _toolTip = new ToolTip();
            _hitAreas = new List<SunburstHitArea>();
            _depth = 3;
            _maxItems = 1000;
        }

        public void SetEntry(FileSystemEntry entry)
        {
            _entry = entry;
            _currentToolTipText = null;
            _toolTip.SetToolTip(this, string.Empty);
            Invalidate();
        }

        public void SetDisplayOptions(int depth, int maxItems)
        {
            _depth = Math.Max(0, depth);
            _maxItems = Math.Max(100, maxItems);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            string toolTipText = string.Empty;

            for (int index = _hitAreas.Count - 1; index >= 0; index--)
            {
                SunburstHitArea hitArea = _hitAreas[index];

                if (hitArea.Contains(e.Location))
                {
                    toolTipText =
                        hitArea.Entry.Name +
                        Environment.NewLine +
                        SizeFormatter.Format(hitArea.Entry.SizeBytes) +
                        Environment.NewLine +
                        hitArea.Entry.FullPath;
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

            foreach (SunburstHitArea hitArea in _hitAreas)
            {
                hitArea.Dispose();
            }

            _hitAreas.Clear();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            if (_entry == null || _entry.Children.Count == 0)
            {
                DrawEmptyText(e.Graphics);
                return;
            }

            int availableDepth = GetAvailableDepth(_entry);
            int visibleDepth = _depth == 0
                ? availableDepth
                : Math.Min(_depth, availableDepth);

            if (visibleDepth <= 0)
            {
                DrawEmptyText(e.Graphics);
                return;
            }

            int diameter = Math.Max(0, Math.Min(Width, Height) - 40);

            if (diameter < 80)
                return;

            RectangleF outerBounds = new RectangleF(
                (Width - diameter) / 2F,
                (Height - diameter) / 2F,
                diameter,
                diameter);

            float centerRadius = Math.Max(24F, diameter * 0.10F);
            float ringWidth = (diameter / 2F - centerRadius) / visibleDepth;

            if (ringWidth < 3F)
                ringWidth = 3F;

            int remainingItems = _maxItems;
            DrawChildren(
                e.Graphics,
                _entry,
                outerBounds,
                centerRadius,
                ringWidth,
                0,
                visibleDepth,
                -90F,
                360F,
                ref remainingItems);

            DrawCenter(e.Graphics, outerBounds, centerRadius);
        }

        private void DrawChildren(
            Graphics graphics,
            FileSystemEntry parent,
            RectangleF outerBounds,
            float centerRadius,
            float ringWidth,
            int level,
            int visibleDepth,
            float startAngle,
            float sweepAngle,
            ref int remainingItems)
        {
            if (level >= visibleDepth || remainingItems <= 0)
                return;

            List<FileSystemEntry> children = parent.Children
                .Where(child => child != null && child.SizeBytes > 0)
                .OrderByDescending(child => child.SizeBytes)
                .ToList();

            long totalSize = children.Sum(child => child.SizeBytes);

            if (totalSize <= 0)
                return;

            float currentAngle = startAngle;
            int colorIndex = 0;

            foreach (FileSystemEntry child in children)
            {
                if (remainingItems <= 0)
                    break;

                float childSweep = sweepAngle * child.SizeBytes / totalSize;

                if (childSweep < 0.15F)
                {
                    currentAngle += childSweep;
                    continue;
                }

                float innerRadius = centerRadius + level * ringWidth;
                float outerRadius = innerRadius + ringWidth;
                Color baseColor = ChartColors[colorIndex % ChartColors.Length];
                Color fillColor = AdjustColor(baseColor, level);

                using GraphicsPath path = CreateRingSegmentPath(
                    outerBounds,
                    innerRadius,
                    outerRadius,
                    currentAngle,
                    childSweep);

                using SolidBrush brush = new SolidBrush(fillColor);
                using Pen borderPen = new Pen(BackColor, 1F);

                graphics.FillPath(brush, path);
                graphics.DrawPath(borderPen, path);

                _hitAreas.Add(new SunburstHitArea(path, child));
                remainingItems--;

                if (child.IsDirectory && child.Children.Count > 0)
                {
                    DrawChildren(
                        graphics,
                        child,
                        outerBounds,
                        centerRadius,
                        ringWidth,
                        level + 1,
                        visibleDepth,
                        currentAngle,
                        childSweep,
                        ref remainingItems);
                }

                currentAngle += childSweep;
                colorIndex++;
            }
        }

        private void DrawCenter(
            Graphics graphics,
            RectangleF outerBounds,
            float centerRadius)
        {
            PointF center = new PointF(
                outerBounds.Left + outerBounds.Width / 2F,
                outerBounds.Top + outerBounds.Height / 2F);

            RectangleF centerBounds = new RectangleF(
                center.X - centerRadius,
                center.Y - centerRadius,
                centerRadius * 2F,
                centerRadius * 2F);

            using SolidBrush centerBrush = new SolidBrush(BackColor);
            using Pen centerPen = new Pen(AntdThemeService.TextPrimary, 1F);
            graphics.FillEllipse(centerBrush, centerBounds);
            graphics.DrawEllipse(centerPen, centerBounds);

            string centerText = _entry?.Name ?? string.Empty;
            using StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };

            using SolidBrush textBrush = new SolidBrush(AntdThemeService.TextPrimary);
            graphics.DrawString(
                centerText,
                Font,
                textBrush,
                centerBounds,
                format);
        }

        private void DrawEmptyText(Graphics graphics)
        {
            using SolidBrush brush = new SolidBrush(AntdThemeService.TextPrimary);
            using StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            graphics.DrawString(
                LocalizationService.GetText("Chart.NoData"),
                Font,
                brush,
                ClientRectangle,
                format);
        }

        private static GraphicsPath CreateRingSegmentPath(
            RectangleF outerBounds,
            float innerRadius,
            float outerRadius,
            float startAngle,
            float sweepAngle)
        {
            PointF center = new PointF(
                outerBounds.Left + outerBounds.Width / 2F,
                outerBounds.Top + outerBounds.Height / 2F);

            RectangleF outerCircle = new RectangleF(
                center.X - outerRadius,
                center.Y - outerRadius,
                outerRadius * 2F,
                outerRadius * 2F);

            RectangleF innerCircle = new RectangleF(
                center.X - innerRadius,
                center.Y - innerRadius,
                innerRadius * 2F,
                innerRadius * 2F);

            GraphicsPath path = new GraphicsPath();
            path.AddArc(outerCircle, startAngle, sweepAngle);
            path.AddArc(innerCircle, startAngle + sweepAngle, -sweepAngle);
            path.CloseFigure();
            return path;
        }

        private static Color AdjustColor(Color color, int level)
        {
            float factor = Math.Max(0.45F, 1F - level * 0.10F);

            return Color.FromArgb(
                color.A,
                Math.Max(0, Math.Min(255, (int)(color.R * factor))),
                Math.Max(0, Math.Min(255, (int)(color.G * factor))),
                Math.Max(0, Math.Min(255, (int)(color.B * factor))));
        }

        private static int GetAvailableDepth(FileSystemEntry entry)
        {
            if (entry == null || entry.Children.Count == 0)
                return 0;

            int maxChildDepth = 0;

            foreach (FileSystemEntry child in entry.Children)
            {
                if (child == null || child.SizeBytes <= 0)
                    continue;

                maxChildDepth = Math.Max(
                    maxChildDepth,
                    GetAvailableDepth(child));
            }

            return 1 + maxChildDepth;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();

                foreach (SunburstHitArea hitArea in _hitAreas)
                {
                    hitArea.Dispose();
                }

                _hitAreas.Clear();
            }

            base.Dispose(disposing);
        }

        private sealed class SunburstHitArea : IDisposable
        {
            private readonly GraphicsPath _path;

            public SunburstHitArea(
                GraphicsPath path,
                FileSystemEntry entry)
            {
                _path = (GraphicsPath)path.Clone();
                Entry = entry;
            }

            public FileSystemEntry Entry { get; }

            public bool Contains(Point point)
            {
                return _path.IsVisible(point);
            }

            public void Dispose()
            {
                _path.Dispose();
            }
        }
    }
}
