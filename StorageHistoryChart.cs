using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public enum StorageHistoryDisplayMode
    {
        UsedSpace,
        FreeSpace
    }

    public sealed class StorageHistoryChart : Control
    {
        private const int TargetYAxisIntervalCount = 17;
        private const int CriticalFreeSpaceGigabytes = 10;
        private const long BytesPerGigabyte = 1024L * 1024L * 1024L;
        private readonly ToolTip toolTip = new ToolTip();
        private IReadOnlyList<StorageHistoryRecord> _records = Array.Empty<StorageHistoryRecord>();
        private StorageHistoryDisplayMode _displayMode = StorageHistoryDisplayMode.FreeSpace;
        private PointF[] _points = Array.Empty<PointF>();
        private int _hoveredPointIndex = -1;
        private int _gradientIntensityPercent = 55;
        private bool _useDarkMode;

        public StorageHistoryChart()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = SystemColors.Window;
        }

        public void SetRecords(IReadOnlyList<StorageHistoryRecord> records, StorageHistoryDisplayMode displayMode)
        {
            _records = (records ?? Array.Empty<StorageHistoryRecord>())
                .OrderBy(record => record.RecordedAtUtc)
                .ToList();
            _displayMode = displayMode;
            _hoveredPointIndex = -1;
            toolTip.Hide(this);
            Invalidate();
        }

        public void SetGradientIntensity(int gradientIntensityPercent)
        {
            int clampedValue = Clamp(gradientIntensityPercent, 0, 100);

            if (_gradientIntensityPercent == clampedValue)
                return;

            _gradientIntensityPercent = clampedValue;
            Invalidate();
        }

        public void ApplyTheme(bool useDarkMode)
        {
            _useDarkMode = useDarkMode;
            BackColor = useDarkMode
                ? Color.FromArgb(32, 32, 32)
                : Color.White;
            ForeColor = useDarkMode
                ? Color.White
                : Color.Black;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoveredPointIndex >= 0)
            {
                _hoveredPointIndex = -1;
                toolTip.Hide(this);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_points.Length == 0)
                return;

            int nearestIndex = -1;
            double nearestDistance = double.MaxValue;

            for (int index = 0; index < _points.Length; index++)
            {
                double deltaX = _points[index].X - e.X;
                double deltaY = _points[index].Y - e.Y;
                double distance = deltaX * deltaX + deltaY * deltaY;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = index;
                }
            }

            if (nearestIndex < 0 || nearestDistance > 144D)
            {
                if (_hoveredPointIndex >= 0)
                {
                    _hoveredPointIndex = -1;
                    toolTip.Hide(this);
                }

                return;
            }

            if (_hoveredPointIndex == nearestIndex)
                return;

            _hoveredPointIndex = nearestIndex;
            StorageHistoryRecord record = _records[nearestIndex];
            long value = GetDisplayValue(record);
            string hint = string.Format(
                CultureInfo.CurrentCulture,
                "{0}\r\n{1}: {2}",
                record.RecordedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                LocalizationService.GetText(
                    _displayMode == StorageHistoryDisplayMode.FreeSpace
                        ? "StorageHistory.Free"
                        : "StorageHistory.Used"),
                SizeFormatter.Format(value));

            if (nearestIndex > 0)
            {
                long previousValue = GetDisplayValue(_records[nearestIndex - 1]);
                long change = value - previousValue;
                string changeDescription;

                if (string.Equals(
                    LocalizationService.CurrentLanguageCode,
                    LocalizationService.GermanLanguageCode,
                    StringComparison.OrdinalIgnoreCase))
                {
                    changeDescription = change > 0L
                        ? "Seit letzter Messung gestiegen"
                        : change < 0L
                            ? "Seit letzter Messung gesunken"
                            : "Seit letzter Messung unverändert";
                }
                else
                {
                    changeDescription = change > 0L
                        ? "Increased since last measurement"
                        : change < 0L
                            ? "Decreased since last measurement"
                            : "Unchanged since last measurement";
                }

                hint += string.Format(
                    CultureInfo.CurrentCulture,
                    "\r\n- {0}: {1}",
                    changeDescription,
                    SizeFormatter.Format(Math.Abs(change)));
            }

            toolTip.Show(hint, this, e.X + 14, e.Y + 14, 10000);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int leftMargin = ScaleForDpi(80);
            int topMargin = ScaleForDpi(45);
            int rightMargin = ScaleForDpi(25);
            int bottomMargin = ScaleForDpi(60);

            Rectangle plotArea = new Rectangle(
                leftMargin,
                topMargin,
                Math.Max(
                    1,
                    ClientSize.Width -
                    leftMargin -
                    rightMargin),
                Math.Max(
                    1,
                    ClientSize.Height -
                    topMargin -
                    bottomMargin));

            using (Font titleFont = new Font(Font, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(ForeColor))
            {
                e.Graphics.DrawString(
                    LocalizationService.GetText("StorageHistory.Graph"),
                    titleFont,
                    titleBrush,
                    ScaleForDpi(12),
                    ScaleForDpi(12));
            }

            if (_records.Count == 0)
            {
                _points = Array.Empty<PointF>();
                TextRenderer.DrawText(
                    e.Graphics,
                    LocalizationService.GetText("StorageHistory.NoData"),
                    Font,
                    ClientRectangle,
                    ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            long maximumCapacity = _records.Max(record => record.TotalCapacityBytes);
            long maximumValue = _records.Max(GetDisplayValue);
            long sourceMaximum = Math.Max(1L, Math.Max(maximumCapacity, maximumValue));
            double sourceMaximumGigabytes = sourceMaximum / (double)BytesPerGigabyte;
            long axisStepGigabytes = GetNiceAxisStepInGigabytes(sourceMaximumGigabytes, TargetYAxisIntervalCount);
            long axisMaximumGigabytes = Math.Max(
                axisStepGigabytes,
                (long)Math.Ceiling(sourceMaximumGigabytes / axisStepGigabytes) * axisStepGigabytes);
            long axisMaximum = Math.Max(1L, axisMaximumGigabytes * BytesPerGigabyte);
            int yAxisLabelCount = (int)(axisMaximumGigabytes / axisStepGigabytes) + 1;

            using (LinearGradientBrush backgroundBrush = CreateBackgroundBrush(plotArea, axisMaximum))
            {
                e.Graphics.FillRectangle(backgroundBrush, plotArea);
            }

            DateTime minimumTime = _records.Min(record => record.RecordedAtUtc);
            DateTime maximumTime = _records.Max(record => record.RecordedAtUtc);
            double timeRangeTicks = Math.Max(1D, (maximumTime - minimumTime).Ticks);

            using (Pen gridPen = new Pen(GetChartLineColor(110), 1F))
            using (Pen axisPen = new Pen(GetChartLineColor(180), 1F))
            {
                gridPen.DashStyle = DashStyle.Dot;

                for (int index = 0; index < yAxisLabelCount; index++)
                {
                    float ratio = yAxisLabelCount == 1
                        ? 0F
                        : index / (float)(yAxisLabelCount - 1);
                    float y = plotArea.Bottom - ratio * plotArea.Height;
                    e.Graphics.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);

                    long labelValueGigabytes = axisStepGigabytes * index;
                    string labelText = FormatGigabytesAxisLabel(labelValueGigabytes);
                    Size labelSize = TextRenderer.MeasureText(labelText, Font);
                    TextRenderer.DrawText(
                        e.Graphics,
                        labelText,
                        Font,
                        new Point(
                            plotArea.Left -
                            labelSize.Width -
                            ScaleForDpi(6),
                            (int)y -
                            labelSize.Height / 2),
                        ForeColor);
                }

                int verticalGridLineCount = Math.Max(
                    2,
                    Math.Min(
                        6,
                        plotArea.Width /
                        Math.Max(
                            1,
                            ScaleForDpi(140))));

                for (int index = 0; index <= verticalGridLineCount; index++)
                {
                    float ratio = index / (float)verticalGridLineCount;
                    float x = plotArea.Left + ratio * plotArea.Width;
                    e.Graphics.DrawLine(gridPen, x, plotArea.Top, x, plotArea.Bottom);

                    DateTime labelTime = minimumTime.AddTicks(
                        (long)Math.Round((maximumTime - minimumTime).Ticks * ratio));
                    string labelText = FormatAxisTime(labelTime, minimumTime, maximumTime);
                    Size labelSize = TextRenderer.MeasureText(labelText, Font);
                    int labelX = (int)x - labelSize.Width / 2;
                    labelX = Math.Max(plotArea.Left, Math.Min(plotArea.Right - labelSize.Width, labelX));

                    TextRenderer.DrawText(
                        e.Graphics,
                        labelText,
                        Font,
                        new Point(
                            labelX,
                            plotArea.Bottom +
                            ScaleForDpi(8)),
                        ForeColor);
                }

                e.Graphics.DrawLine(axisPen, plotArea.Left, plotArea.Top, plotArea.Left, plotArea.Bottom);
                e.Graphics.DrawLine(axisPen, plotArea.Left, plotArea.Bottom, plotArea.Right, plotArea.Bottom);
            }

            _points = new PointF[_records.Count];

            for (int index = 0; index < _records.Count; index++)
            {
                double elapsedTicks = (_records[index].RecordedAtUtc - minimumTime).Ticks;
                float normalizedTime = _records.Count == 1
                    ? 0.5F
                    : (float)(elapsedTicks / timeRangeTicks);
                float x = plotArea.Left + normalizedTime * plotArea.Width;

                long value = GetDisplayValue(_records[index]);
                float normalizedValue = value / (float)axisMaximum;
                float y = plotArea.Bottom - normalizedValue * plotArea.Height;
                _points[index] = new PointF(x, y);
            }

            using (Pen graphPen = new Pen(
                SystemColors.Highlight,
                Math.Max(
                    1F,
                    ScaleForDpi(2))))
            {
                if (_points.Length > 1)
                {
                    e.Graphics.DrawLines(graphPen, _points);
                }
            }

            using (Brush pointBrush = new SolidBrush(SystemColors.Highlight))
            {
                foreach (PointF point in _points)
                {
                    float pointRadius =
                        ScaleForDpi(3);
                    float pointDiameter =
                        pointRadius * 2F;

                    e.Graphics.FillEllipse(
                        pointBrush,
                        point.X - pointRadius,
                        point.Y - pointRadius,
                        pointDiameter,
                        pointDiameter);
                }
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

        private LinearGradientBrush CreateBackgroundBrush(Rectangle plotArea, long axisMaximum)
        {
            LinearGradientBrush backgroundBrush = new LinearGradientBrush(
                plotArea,
                Color.White,
                Color.White,
                LinearGradientMode.Vertical);

            Color pastelGreen = BlendWithWhite(Color.FromArgb(120, 190, 120), _gradientIntensityPercent);
            Color pastelRed = BlendWithWhite(Color.FromArgb(225, 140, 140), _gradientIntensityPercent);
            Color strongRed = BlendWithWhite(
                Color.FromArgb(210, 70, 70),
                Math.Min(100, _gradientIntensityPercent + 35));

            if (_displayMode == StorageHistoryDisplayMode.FreeSpace)
            {
                double criticalFreeSpaceBytes = CriticalFreeSpaceGigabytes * 1024D * 1024D * 1024D;
                float criticalPosition = 1F - (float)Math.Min(1D, criticalFreeSpaceBytes / Math.Max(1D, axisMaximum));
                criticalPosition = Math.Max(0F, Math.Min(1F, criticalPosition));

                if (criticalPosition <= 0F || criticalPosition >= 1F)
                {
                    backgroundBrush.InterpolationColors = new ColorBlend
                    {
                        Positions = new[] { 0F, 1F },
                        Colors = new[] { pastelGreen, strongRed }
                    };
                }
                else
                {
                    backgroundBrush.InterpolationColors = new ColorBlend
                    {
                        Positions = new[] { 0F, criticalPosition, 1F },
                        Colors = new[] { pastelGreen, pastelRed, strongRed }
                    };
                }
            }
            else
            {
                backgroundBrush.InterpolationColors = new ColorBlend
                {
                    Positions = new[] { 0F, 1F },
                    Colors = new[] { pastelRed, pastelGreen }
                };
            }

            return backgroundBrush;
        }

        private static string FormatAxisTime(DateTime valueUtc, DateTime minimumTime, DateTime maximumTime)
        {
            TimeSpan range = maximumTime - minimumTime;
            DateTime localValue = valueUtc.ToLocalTime();

            if (range.TotalDays < 1D)
                return localValue.ToString("t", CultureInfo.CurrentCulture);

            if (range.TotalDays < 7D)
                return localValue.ToString("g", CultureInfo.CurrentCulture);

            return localValue.ToString("d", CultureInfo.CurrentCulture);
        }

        private long GetDisplayValue(StorageHistoryRecord record)
        {
            if (_displayMode == StorageHistoryDisplayMode.FreeSpace)
            {
                if (record.TotalCapacityBytes > 0L)
                {
                    return Math.Max(
                        0L,
                        Math.Min(record.TotalCapacityBytes, record.FreeSpaceBytes));
                }

                return 0L;
            }

            if (record.TotalCapacityBytes > 0L)
            {
                return Math.Max(
                    0L,
                    Math.Min(record.TotalCapacityBytes, record.TotalCapacityBytes - record.FreeSpaceBytes));
            }

            return Math.Max(0L, record.SizeBytes);
        }

        private static long GetNiceAxisStepInGigabytes(double maximumValueGigabytes, int targetIntervalCount)
        {
            if (maximumValueGigabytes <= 0D)
                return 1L;

            double rawStep = maximumValueGigabytes / Math.Max(1, targetIntervalCount);
            double exponent = Math.Floor(Math.Log10(rawStep));
            double magnitude = Math.Pow(10D, exponent);
            double normalized = rawStep / magnitude;

            double[] niceSteps = { 1D, 2D, 2.5D, 5D, 10D };

            foreach (double niceStep in niceSteps)
            {
                if (normalized <= niceStep)
                {
                    return Math.Max(1L, (long)Math.Ceiling(niceStep * magnitude));
                }
            }

            return Math.Max(1L, (long)Math.Ceiling(10D * magnitude));
        }

        private static string FormatGigabytesAxisLabel(long valueGigabytes)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0} GB",
                valueGigabytes.ToString("N0", CultureInfo.CurrentCulture));
        }

        private Color GetChartLineColor(int alpha)
        {
            Color baseColor = _useDarkMode
                ? Color.White
                : Color.Black;
            return Color.FromArgb(Clamp(alpha, 0, 255), baseColor);
        }

        private static Color BlendWithWhite(Color baseColor, int intensityPercent)
        {
            double ratio = Clamp(intensityPercent, 0, 100) / 100D;
            int red = (int)Math.Round(255D - (255D - baseColor.R) * ratio);
            int green = (int)Math.Round(255D - (255D - baseColor.G) * ratio);
            int blue = (int)Math.Round(255D - (255D - baseColor.B) * ratio);
            return Color.FromArgb(red, green, blue);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
                return minimum;

            if (value > maximum)
                return maximum;

            return value;
        }
    }
}
