using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class Chart_Sunburst : Control
    {
        private readonly ToolTip _toolTip;
        private readonly List<SunburstHitArea> _hitAreas;
        private readonly ContextMenuStrip _contextMenu;
        private FileSystemEntry _entry;
        private FileSystemEntry _contextMenuEntry;
        private string _currentToolTipText;
        private int _depth;
        private int _maxItems;

        private const double FamilySplitMinimumShare = 0.10D;
        private const double FamilyPromotionMinimumShare = 0.50D;
        private const double DominantChildMinimumShare = 0.60D;
        private const int FamilyPromotionMaximumDepth = 8;

        public Chart_Sunburst()
        {
            DoubleBuffered = true;
            BackColor = AntdThemeService.BackgroundPrimary;
            ForeColor = AntdThemeService.TextPrimary;
            Font = AntdThemeService.DefaultFont;
            _toolTip = new ToolTip();
            _hitAreas = new List<SunburstHitArea>();

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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Right)
                return;

            _contextMenuEntry = null;

            for (int index = _hitAreas.Count - 1; index >= 0; index--)
            {
                SunburstHitArea hitArea = _hitAreas[index];

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
                ref remainingItems,
                AntdThemeService.GetChartFamilyColor(0),
                false);

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
            ref int remainingItems,
            Color inheritedFamilyColor,
            bool familyLocked)
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

            bool createFamilies =
                !familyLocked &&
                HasMeaningfulFamilySplit(
                    parent,
                    children);

            int familyIndex =
                AntdThemeService.GetChartFamilyStartIndex(
                    parent.Name);

            float currentAngle = startAngle;

            foreach (FileSystemEntry child in children)
            {
                if (remainingItems <= 0)
                    break;

                float childSweep =
                    sweepAngle *
                    child.SizeBytes /
                    totalSize;

                if (childSweep < 0.15F)
                {
                    currentAngle += childSweep;
                    continue;
                }

                Color childFamilyColor =
                    inheritedFamilyColor;

                bool childFamilyLocked =
                    familyLocked;

                if (createFamilies)
                {
                    bool promoteDescendantFamilies =
                        ShouldPromoteDescendantFamilies(
                            parent,
                            child);

                    if (!promoteDescendantFamilies)
                    {
                        childFamilyColor =
                            AntdThemeService.GetChartFamilyColor(
                                familyIndex);

                        familyIndex++;
                        childFamilyLocked = true;
                    }
                }

                float innerRadius =
                    centerRadius +
                    level * ringWidth;
                float outerRadius =
                    innerRadius +
                    ringWidth;

                Color fillColor =
                    AntdThemeService.GetChartFamilyShade(
                        childFamilyColor,
                        child.Name,
                        level);

                Color gradientTopColor =
                    AntdThemeService.LightenChartColor(
                        fillColor,
                        AntdThemeService.ChartFamilyGradientTopFactor);

                Color gradientBottomColor =
                    AntdThemeService.DarkenChartColor(
                        fillColor,
                        AntdThemeService.ChartFamilyGradientBottomFactor);

                using GraphicsPath path = CreateRingSegmentPath(
                    outerBounds,
                    innerRadius,
                    outerRadius,
                    currentAngle,
                    childSweep);

                using (PathGradientBrush fillBrush =
                       new PathGradientBrush(path))
                {
                    fillBrush.CenterColor =
                        gradientTopColor;
                    fillBrush.SurroundColors =
                        new[]
                        {
                            gradientBottomColor
                        };

                    graphics.FillPath(
                        fillBrush,
                        path);
                }

                using (Pen borderPen =
                       new Pen(
                           AntdThemeService.BackgroundPrimary,
                           1F))
                {
                    graphics.DrawPath(
                        borderPen,
                        path);
                }

                DrawSegmentLabel(
                    graphics,
                    path,
                    child,
                    outerBounds,
                    innerRadius,
                    outerRadius,
                    currentAngle,
                    childSweep);

                _hitAreas.Add(
                    new SunburstHitArea(
                        path,
                        child));

                remainingItems--;

                if (child.IsDirectory &&
                    child.Children.Count > 0)
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
                        ref remainingItems,
                        childFamilyColor,
                        childFamilyLocked);
                }

                currentAngle += childSweep;
            }
        }

        private static bool HasMeaningfulFamilySplit(
            FileSystemEntry parent,
            List<FileSystemEntry> children)
        {
            if (parent == null ||
                children == null ||
                parent.SizeBytes <= 0)
            {
                return false;
            }

            int significantCount = 0;

            foreach (FileSystemEntry child in children)
            {
                if (child == null ||
                    child.SizeBytes <= 0)
                {
                    continue;
                }

                double share =
                    child.SizeBytes /
                    (double)parent.SizeBytes;

                if (share >= FamilySplitMinimumShare)
                {
                    significantCount++;
                }

                if (significantCount >= 2)
                    return true;
            }

            return false;
        }

        private static bool ShouldPromoteDescendantFamilies(
            FileSystemEntry parent,
            FileSystemEntry child)
        {
            if (parent == null ||
                child == null ||
                !child.IsDirectory ||
                parent.SizeBytes <= 0 ||
                child.SizeBytes <= 0)
            {
                return false;
            }

            double share =
                child.SizeBytes /
                (double)parent.SizeBytes;

            if (share < FamilyPromotionMinimumShare)
                return false;

            return HasDescendantMeaningfulFamilySplit(
                child,
                0);
        }

        private static bool HasDescendantMeaningfulFamilySplit(
            FileSystemEntry entry,
            int depth)
        {
            if (entry == null ||
                entry.Children.Count == 0 ||
                entry.SizeBytes <= 0 ||
                depth >= FamilyPromotionMaximumDepth)
            {
                return false;
            }

            List<FileSystemEntry> children =
                entry.Children
                    .Where(
                        child =>
                            child != null &&
                            child.SizeBytes > 0)
                    .OrderByDescending(
                        child =>
                            child.SizeBytes)
                    .ToList();

            int significantCount = 0;

            foreach (FileSystemEntry child in children)
            {
                double share =
                    child.SizeBytes /
                    (double)entry.SizeBytes;

                if (share >= FamilySplitMinimumShare)
                {
                    significantCount++;
                }

                if (significantCount >= 2)
                    return true;
            }

            FileSystemEntry dominantChild =
                children
                    .Where(
                        child =>
                            child.IsDirectory)
                    .FirstOrDefault();

            if (dominantChild == null ||
                dominantChild.SizeBytes /
                    (double)entry.SizeBytes <
                    DominantChildMinimumShare)
            {
                return false;
            }

            return HasDescendantMeaningfulFamilySplit(
                dominantChild,
                depth + 1);
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

            using SolidBrush centerBrush =
                new SolidBrush(
                    AntdThemeService.BackgroundPrimary);
            using Pen centerPen =
                new Pen(
                    AntdThemeService.Border,
                    1F);
            graphics.FillEllipse(centerBrush, centerBounds);
            graphics.DrawEllipse(centerPen, centerBounds);

            string centerText = _entry?.Name ?? string.Empty;
            using StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };

            using SolidBrush textBrush =
                new SolidBrush(
                    AntdThemeService.TextPrimary);
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

        private void DrawSegmentLabel(
            Graphics graphics,
            GraphicsPath segmentPath,
            FileSystemEntry entry,
            RectangleF outerBounds,
            float innerRadius,
            float outerRadius,
            float startAngle,
            float sweepAngle)
        {
            if (entry == null ||
                segmentPath == null)
            {
                return;
            }

            float middleRadius =
                (innerRadius + outerRadius) / 2F;

            float arcLength =
                Math.Abs(sweepAngle) *
                (float)Math.PI /
                180F *
                middleRadius;

            float radialHeight =
                outerRadius -
                innerRadius;

            if (arcLength < 58F ||
                radialHeight < 18F)
            {
                return;
            }

            string name =
                entry.Name ??
                string.Empty;

            if (string.IsNullOrWhiteSpace(name))
                return;

            string sizeText =
                SizeFormatter.Format(
                    entry.SizeBytes);

            bool drawTwoLines =
                radialHeight >= 36F &&
                arcLength >= 84F;

            float labelWidth =
                Math.Min(
                    Math.Max(
                        0F,
                        arcLength - 12F),
                    180F);

            float labelHeight =
                drawTwoLines
                    ? 34F
                    : 18F;

            if (labelWidth < 48F)
                return;

            float middleAngle =
                startAngle +
                sweepAngle / 2F;

            double radians =
                middleAngle *
                Math.PI /
                180D;

            PointF center =
                new PointF(
                    outerBounds.Left +
                    outerBounds.Width / 2F,
                    outerBounds.Top +
                    outerBounds.Height / 2F);

            PointF labelCenter =
                new PointF(
                    center.X +
                    middleRadius *
                    (float)Math.Cos(radians),
                    center.Y +
                    middleRadius *
                    (float)Math.Sin(radians));

            RectangleF labelBounds =
                new RectangleF(
                    labelCenter.X -
                    labelWidth / 2F,
                    labelCenter.Y -
                    labelHeight / 2F,
                    labelWidth,
                    labelHeight);

            GraphicsState state =
                graphics.Save();

            try
            {
                graphics.SetClip(
                    segmentPath,
                    CombineMode.Intersect);

                using SolidBrush textBrush =
                    new SolidBrush(
                        AntdThemeService.ChartSegmentTextColor);

                using StringFormat format =
                    new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };

                RectangleF nameBounds =
                    new RectangleF(
                        labelBounds.X + 4F,
                        labelBounds.Y,
                        Math.Max(
                            0F,
                            labelBounds.Width - 8F),
                        drawTwoLines
                            ? 17F
                            : labelBounds.Height);

                graphics.DrawString(
                    name,
                    Font,
                    textBrush,
                    nameBounds,
                    format);

                if (!drawTwoLines)
                    return;

                RectangleF sizeBounds =
                    new RectangleF(
                        labelBounds.X + 4F,
                        labelBounds.Y + 17F,
                        Math.Max(
                            0F,
                            labelBounds.Width - 8F),
                        17F);

                graphics.DrawString(
                    sizeText,
                    Font,
                    textBrush,
                    sizeBounds,
                    format);
            }
            finally
            {
                graphics.Restore(
                    state);
            }
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
                _contextMenu.Dispose();

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
