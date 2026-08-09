using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class LayoutMainFormController
    {
        private readonly AppSettings _settings;
        private readonly Form _form;
        private readonly SplitContainer _splitContainerMain;
        private readonly SplitContainer _splitContainerLeft;
        private readonly FlowLayoutPanel _toolStripPanelMain;
        private readonly ToolStrip _toolStripMain;
        private readonly ToolStrip _toolStripViewMode;
        private readonly ToolStrip _toolStripExport;
        private readonly ToolStrip _toolStripFeatures;
        private readonly AntdUI.Panel _panelRightViewHost;
        private readonly Chart_TableGridChart _dataGridViewEntries;
        private readonly Chart_PieChart _pieChartView;
        private readonly Chart_BarChart _barChartView;
        private readonly Chart_Sunburst _sunburstView;
        private readonly Chart_Treemap _treemapView;
        private readonly AntdUI.Button _toolStripButtonTable;
        private readonly AntdUI.Button _toolStripButtonPieChart;
        private readonly AntdUI.Button _toolStripButtonBarChart;
        private readonly AntdUI.Button _toolStripButtonSunburst;
        private readonly AntdUI.Button _toolStripButtonTreemap;
        private ToolStrip _draggedToolStrip;
        private Point _dragStartPoint;
        private ViewMode _viewMode;

        public LayoutMainFormController(
            AppSettings settings,
            Form form,
            SplitContainer splitContainerMain,
            SplitContainer splitContainerLeft,
            FlowLayoutPanel toolStripPanelMain,
            ToolStrip toolStripMain,
            ToolStrip toolStripViewMode,
            ToolStrip toolStripExport,
            ToolStrip toolStripFeatures,
            AntdUI.Panel panelRightViewHost,
            Chart_TableGridChart dataGridViewEntries,
            Chart_PieChart pieChartView,
            Chart_BarChart barChartView,
            Chart_Sunburst sunburstView,
            Chart_Treemap treemapView,
            AntdUI.Button toolStripButtonTable,
            AntdUI.Button toolStripButtonPieChart,
            AntdUI.Button toolStripButtonBarChart,
            AntdUI.Button toolStripButtonSunburst,
            AntdUI.Button toolStripButtonTreemap,
            ViewMode viewMode)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _form = form ?? throw new ArgumentNullException(nameof(form));
            _splitContainerMain = splitContainerMain ?? throw new ArgumentNullException(nameof(splitContainerMain));
            _splitContainerLeft = splitContainerLeft ?? throw new ArgumentNullException(nameof(splitContainerLeft));
            _toolStripPanelMain = toolStripPanelMain ?? throw new ArgumentNullException(nameof(toolStripPanelMain));
            _toolStripMain = toolStripMain ?? throw new ArgumentNullException(nameof(toolStripMain));
            _toolStripViewMode = toolStripViewMode ?? throw new ArgumentNullException(nameof(toolStripViewMode));
            _toolStripExport = toolStripExport ?? throw new ArgumentNullException(nameof(toolStripExport));
            _toolStripFeatures = toolStripFeatures ?? throw new ArgumentNullException(nameof(toolStripFeatures));
            _panelRightViewHost = panelRightViewHost ?? throw new ArgumentNullException(nameof(panelRightViewHost));
            _dataGridViewEntries = dataGridViewEntries ?? throw new ArgumentNullException(nameof(dataGridViewEntries));
            _pieChartView = pieChartView ?? throw new ArgumentNullException(nameof(pieChartView));
            _barChartView = barChartView ?? throw new ArgumentNullException(nameof(barChartView));
            _sunburstView = sunburstView ?? throw new ArgumentNullException(nameof(sunburstView));
            _treemapView = treemapView ?? throw new ArgumentNullException(nameof(treemapView));
            _toolStripButtonTable = toolStripButtonTable ?? throw new ArgumentNullException(nameof(toolStripButtonTable));
            _toolStripButtonPieChart = toolStripButtonPieChart ?? throw new ArgumentNullException(nameof(toolStripButtonPieChart));
            _toolStripButtonBarChart = toolStripButtonBarChart ?? throw new ArgumentNullException(nameof(toolStripButtonBarChart));
            _toolStripButtonSunburst = toolStripButtonSunburst ?? throw new ArgumentNullException(nameof(toolStripButtonSunburst));
            _toolStripButtonTreemap = toolStripButtonTreemap ?? throw new ArgumentNullException(nameof(toolStripButtonTreemap));
            _viewMode = viewMode;

            ConfigureToolStripGroupDragAndDrop();
        }

        public void SavePersistentSettings(bool suspendPersistentSettingsSave)
        {
            if (suspendPersistentSettingsSave)
                return;

            SaveViewSettings();
            _settings.Save();
        }

        public void ApplyMainWindowSettings()
        {
            if (!_settings.HasMainWindowBounds)
                return;

            if (_settings.MainWindowWidth < _form.MinimumSize.Width || _settings.MainWindowHeight < _form.MinimumSize.Height)
                return;

            Rectangle savedBounds = new Rectangle(
                _settings.MainWindowLeft,
                _settings.MainWindowTop,
                _settings.MainWindowWidth,
                _settings.MainWindowHeight);

            if (!IsVisibleOnAnyScreen(savedBounds))
                return;

            _form.StartPosition = FormStartPosition.Manual;
            _form.Bounds = savedBounds;

            if (_settings.MainWindowMaximized)
            {
                _form.WindowState = FormWindowState.Maximized;
            }
        }

        public void SaveMainWindowSettings()
        {
            Rectangle bounds = _form.WindowState == FormWindowState.Normal
                ? _form.Bounds
                : _form.RestoreBounds;

            _settings.HasMainWindowBounds = true;
            _settings.MainWindowLeft = bounds.Left;
            _settings.MainWindowTop = bounds.Top;
            _settings.MainWindowWidth = bounds.Width;
            _settings.MainWindowHeight = bounds.Height;
            _settings.MainWindowMaximized = _form.WindowState == FormWindowState.Maximized;
        }

        public void SaveSplitterLayout()
        {
            _settings.HasSplitterLayout = true;
            _settings.PartitionPanelLayoutVersion = 2;
            _settings.SplitContainerMainDistance = _splitContainerMain.SplitterDistance;
            _settings.SplitContainerLeftDistance = _splitContainerLeft.Height - _splitContainerLeft.SplitterDistance - _splitContainerLeft.SplitterWidth;
        }

        public void ApplySplitterLayout()
        {
            if (!_settings.HasSplitterLayout)
                return;

            if (_settings.SplitContainerMainDistance >= _splitContainerMain.Panel1MinSize &&
                _settings.SplitContainerMainDistance <= _splitContainerMain.Width - _splitContainerMain.Panel2MinSize)
            {
                _splitContainerMain.SplitterDistance = _settings.SplitContainerMainDistance;
            }

            int splitContainerLeftDistance = _splitContainerLeft.Height - _settings.SplitContainerLeftDistance - _splitContainerLeft.SplitterWidth;

            if (splitContainerLeftDistance >= _splitContainerLeft.Panel1MinSize &&
                splitContainerLeftDistance <= _splitContainerLeft.Height - _splitContainerLeft.Panel2MinSize)
            {
                _splitContainerLeft.SplitterDistance = splitContainerLeftDistance;
            }
        }

        public void ApplyDefaultToolStripLayout()
        {
            SetToolStripOrder(
                new[]
                {
                    _toolStripMain,
                    _toolStripViewMode,
                    _toolStripExport,
                    _toolStripFeatures
                });
        }

        public void ApplyToolStripLayout()
        {
            if (!_settings.HasToolStripLayout)
                return;

            if (_settings.ToolStripLayoutVersion != 14)
                return;

            List<(ToolStrip ToolStrip, int Order)> toolStrips =
                new List<(ToolStrip ToolStrip, int Order)>
                {
                    (
                        _toolStripMain,
                        _settings.ToolStripMainLeft),
                    (
                        _toolStripViewMode,
                        _settings.ToolStripViewModeLeft),
                    (
                        _toolStripExport,
                        _settings.ToolStripExportLeft),
                    (
                        _toolStripFeatures,
                        _settings.ToolStripFeaturesLeft)
                };

            toolStrips.Sort(
                (left, right) => left.Order.CompareTo(right.Order));

            SetToolStripOrder(
                toolStrips
                    .ConvertAll(item => item.ToolStrip)
                    .ToArray());
        }

        private void SetToolStripOrder(
            IReadOnlyList<ToolStrip> toolStrips)
        {
            _toolStripPanelMain.SuspendLayout();

            try
            {
                for (int index = 0; index < toolStrips.Count; index++)
                {
                    ToolStrip toolStrip = toolStrips[index];

                    if (!_toolStripPanelMain.Controls.Contains(toolStrip))
                        _toolStripPanelMain.Controls.Add(toolStrip);

                    _toolStripPanelMain.Controls.SetChildIndex(
                        toolStrip,
                        index);
                }
            }
            finally
            {
                _toolStripPanelMain.ResumeLayout(true);
                _toolStripPanelMain.PerformLayout();
            }
        }

        private void ConfigureToolStripGroupDragAndDrop()
        {
            _toolStripPanelMain.DragEnter += ToolStripPanelMain_DragEnter;
            _toolStripPanelMain.DragOver += ToolStripPanelMain_DragOver;
            _toolStripPanelMain.DragDrop += ToolStripPanelMain_DragDrop;

            ConfigureToolStripDragSource(_toolStripMain);
            ConfigureToolStripDragSource(_toolStripViewMode);
            ConfigureToolStripDragSource(_toolStripExport);
            ConfigureToolStripDragSource(_toolStripFeatures);
        }

        private void ConfigureToolStripDragSource(ToolStrip toolStrip)
        {
            toolStrip.MouseDown += ToolStrip_MouseDown;
            toolStrip.MouseMove += ToolStrip_MouseMove;
            toolStrip.MouseUp += ToolStrip_MouseUp;
        }

        private void ToolStrip_MouseDown(
            object sender,
            MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _draggedToolStrip = sender as ToolStrip;
            _dragStartPoint = e.Location;
        }

        private void ToolStrip_MouseMove(
            object sender,
            MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            if (_draggedToolStrip == null)
                return;

            Rectangle dragRectangle = new Rectangle(
                _dragStartPoint.X - SystemInformation.DragSize.Width / 2,
                _dragStartPoint.Y - SystemInformation.DragSize.Height / 2,
                SystemInformation.DragSize.Width,
                SystemInformation.DragSize.Height);

            if (dragRectangle.Contains(e.Location))
                return;

            _draggedToolStrip.DoDragDrop(
                _draggedToolStrip,
                DragDropEffects.Move);
        }

        private void ToolStrip_MouseUp(
            object sender,
            MouseEventArgs e)
        {
            _draggedToolStrip = null;
        }

        private void ToolStripPanelMain_DragEnter(
            object sender,
            DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(ToolStrip))
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }

        private void ToolStripPanelMain_DragOver(
            object sender,
            DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ToolStrip)))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            ToolStrip draggedToolStrip =
                e.Data.GetData(typeof(ToolStrip)) as ToolStrip;

            if (draggedToolStrip == null)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            Point clientPoint = _toolStripPanelMain.PointToClient(
                new Point(e.X, e.Y));

            int targetIndex = GetToolStripInsertIndex(
                clientPoint,
                draggedToolStrip);

            _toolStripPanelMain.Controls.SetChildIndex(
                draggedToolStrip,
                targetIndex);

            _toolStripPanelMain.PerformLayout();
            e.Effect = DragDropEffects.Move;
        }

        private void ToolStripPanelMain_DragDrop(
            object sender,
            DragEventArgs e)
        {
            _draggedToolStrip = null;
            SaveToolStripLayout();
            _settings.Save();
        }

        private int GetToolStripInsertIndex(
            Point clientPoint,
            ToolStrip draggedToolStrip)
        {
            List<ToolStrip> orderedToolStrips =
                new List<ToolStrip>();

            foreach (Control control in _toolStripPanelMain.Controls)
            {
                if (control is ToolStrip toolStrip &&
                    !ReferenceEquals(toolStrip, draggedToolStrip))
                {
                    orderedToolStrips.Add(toolStrip);
                }
            }

            orderedToolStrips.Sort(
                (left, right) =>
                {
                    int topComparison = left.Top.CompareTo(right.Top);

                    return topComparison != 0
                        ? topComparison
                        : left.Left.CompareTo(right.Left);
                });

            for (int index = 0;
                 index < orderedToolStrips.Count;
                 index++)
            {
                ToolStrip toolStrip = orderedToolStrips[index];

                int middleX =
                    toolStrip.Left + toolStrip.Width / 2;

                int middleY =
                    toolStrip.Top + toolStrip.Height / 2;

                if (clientPoint.Y < middleY ||
                    (clientPoint.Y <= toolStrip.Bottom &&
                     clientPoint.X < middleX))
                {
                    return index;
                }
            }

            return orderedToolStrips.Count;
        }

        public void SaveToolStripLayout()
        {
            _settings.HasToolStripLayout = true;
            _settings.ToolStripLayoutVersion = 14;

            _settings.ToolStripMainLeft =
                _toolStripPanelMain.Controls.GetChildIndex(
                    _toolStripMain);
            _settings.ToolStripMainTop = 0;

            _settings.ToolStripViewModeLeft =
                _toolStripPanelMain.Controls.GetChildIndex(
                    _toolStripViewMode);
            _settings.ToolStripViewModeTop = 0;

            _settings.ToolStripExportLeft =
                _toolStripPanelMain.Controls.GetChildIndex(
                    _toolStripExport);
            _settings.ToolStripExportTop = 0;

            _settings.ToolStripFeaturesLeft =
                _toolStripPanelMain.Controls.GetChildIndex(
                    _toolStripFeatures);
            _settings.ToolStripFeaturesTop = 0;
        }

        public void SaveViewSettings()
        {
            _settings.SelectedViewMode = _viewMode;
        }

        public void BindGrid(FileSystemEntry entry)
        {
            _dataGridViewEntries.SetEntry(entry);
            _pieChartView.SetEntry(entry);
            _barChartView.SetEntry(entry);
            _sunburstView.SetDisplayOptions(
                _settings.SunburstDepth,
                _settings.SunburstMaxItems);
            _sunburstView.SetEntry(entry);
            _treemapView.SetEntry(entry);
            UpdateRightView();
        }

        public void SetRootEntry(FileSystemEntry rootEntry)
        {
            _treemapView.SetRootEntry(rootEntry);
        }

        public void SetViewMode(ViewMode viewMode, bool suspendPersistentSettingsSave)
        {
            _viewMode = viewMode;
            _settings.SelectedViewMode = viewMode;
            UpdateViewModeButtons();
            UpdateRightView();
            SavePersistentSettings(suspendPersistentSettingsSave);
        }

        public void UpdateRightViewBounds()
        {
            if (_panelRightViewHost == null)
                return;

            if (_dataGridViewEntries != null)
            {
                _dataGridViewEntries.Dock = DockStyle.Fill;
                _dataGridViewEntries.ApplyEntryGridColumnWidths();
            }

            if (_pieChartView != null)
            {
                _pieChartView.Dock = DockStyle.Fill;
                _pieChartView.Invalidate();
            }

            if (_barChartView != null)
            {
                _barChartView.Dock = DockStyle.Fill;
                _barChartView.Invalidate();
            }

            if (_sunburstView != null)
            {
                _sunburstView.Dock = DockStyle.Fill;
                _sunburstView.Invalidate();
            }

            if (_treemapView != null)
            {
                _treemapView.Dock = DockStyle.Fill;
                _treemapView.Invalidate();
            }
        }

        private void UpdateViewModeButtons()
        {
            _toolStripButtonTable.Toggle = _viewMode == ViewMode.Table;
            _toolStripButtonPieChart.Toggle = _viewMode == ViewMode.PieChart;
            _toolStripButtonBarChart.Toggle = _viewMode == ViewMode.BarChart;
            _toolStripButtonSunburst.Toggle = _viewMode == ViewMode.Sunburst;
            _toolStripButtonTreemap.Toggle = _viewMode == ViewMode.Treemap;
        }

        private void UpdateRightView()
        {
            UpdateRightViewBounds();

            _dataGridViewEntries.Visible = _viewMode == ViewMode.Table;
            _pieChartView.Visible = _viewMode == ViewMode.PieChart;
            _barChartView.Visible = _viewMode == ViewMode.BarChart;
            _sunburstView.Visible = _viewMode == ViewMode.Sunburst;
            _treemapView.Visible = _viewMode == ViewMode.Treemap;

            if (_dataGridViewEntries.Visible)
            {
                _dataGridViewEntries.BringToFront();
            }
            else if (_pieChartView.Visible)
            {
                _pieChartView.BringToFront();
                _pieChartView.Invalidate();
            }
            else if (_barChartView.Visible)
            {
                _barChartView.BringToFront();
                _barChartView.Invalidate();
            }
            else if (_sunburstView.Visible)
            {
                _sunburstView.BringToFront();
                _sunburstView.Invalidate();
            }
            else if (_treemapView.Visible)
            {
                _treemapView.BringToFront();
                _treemapView.Invalidate();
            }
        }

        private bool IsVisibleOnAnyScreen(Rectangle bounds)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(bounds))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
