using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace c2flux
{
    public static class WindowsFormStyler
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static void Apply(Form form, AppLayout layout)
        {
            form.Icon = AppResources.ApplicationIcon;
            ApplyWindowsTheme(form, layout);
        }

        private static void ApplyWindowsTheme(Form form, AppLayout layout)
        {
            form.SuspendLayout();

            bool useDarkMode = ShouldUseDarkMode(layout);

            form.Font = SystemFonts.MessageBoxFont;
            form.SizeGripStyle = IsResizable(form) ? SizeGripStyle.Auto : SizeGripStyle.Hide;

            SetImmersiveDarkMode(form, useDarkMode);

            if (useDarkMode)
            {
                form.BackColor = Color.FromArgb(32, 32, 32);
                form.ForeColor = Color.White;
            }
            else
            {
                form.BackColor = SystemColors.Control;
                form.ForeColor = SystemColors.ControlText;
            }

            foreach (Control control in form.Controls)
            {
                ApplyWindowsControl(control, useDarkMode);
            }

            form.ResumeLayout(false);
            form.PerformLayout();
        }

        private static bool IsResizable(Form form)
        {
            return form.MinimumSize != form.MaximumSize;
        }

        private static bool IsAntdUIControl(Control control)
        {
            string controlNamespace = control.GetType().Namespace;

            return !string.IsNullOrWhiteSpace(controlNamespace) &&
                   controlNamespace.StartsWith("AntdUI.", StringComparison.Ordinal);
        }

        private static bool ShouldUseDarkMode(AppLayout layout)
        {
            if (layout == AppLayout.WindowsDarkMode)
                return true;

            if (layout == AppLayout.WindowsLightMode)
                return false;

            return IsWindowsAppDarkModeEnabled();
        }

        private static bool IsWindowsAppDarkModeEnabled()
        {
            try
            {
                using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                object value = key?.GetValue("AppsUseLightTheme");

                if (value is int appsUseLightTheme)
                {
                    return appsUseLightTheme == 0;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void ApplyWindowsControl(Control control, bool useDarkMode)
        {
            Color windowBackColor;
            Color controlBackColor;
            Color headerBackColor;
            Color textColor;
            Color selectedBackColor;
            Color selectedTextColor;
            Color gridColor;

            if (useDarkMode)
            {
                windowBackColor = Color.FromArgb(32, 32, 32);
                controlBackColor = Color.FromArgb(45, 45, 45);
                headerBackColor = Color.FromArgb(24, 24, 24);
                textColor = Color.White;
                selectedBackColor = SystemColors.Highlight;
                selectedTextColor = SystemColors.HighlightText;
                gridColor = Color.FromArgb(80, 80, 80);
            }
            else
            {
                windowBackColor = SystemColors.Window;
                controlBackColor = SystemColors.Control;
                headerBackColor = SystemColors.Control;
                textColor = SystemColors.ControlText;
                selectedBackColor = SystemColors.Highlight;
                selectedTextColor = SystemColors.HighlightText;
                gridColor = SystemColors.ControlDark;
            }

            control.Font = SystemFonts.MessageBoxFont;
            control.ForeColor = textColor;

            if (control is TabControl tabControl)
            {
                tabControl.DrawMode = useDarkMode ? TabDrawMode.OwnerDrawFixed : TabDrawMode.Normal;
                tabControl.BackColor = useDarkMode ? windowBackColor : SystemColors.Control;
                tabControl.ForeColor = textColor;
                tabControl.DrawItem -= tabControl_DrawItem;

                if (useDarkMode)
                {
                    tabControl.DrawItem += tabControl_DrawItem;
                    DarkTabControlHeaderPainter.Attach(tabControl);
                }
                else
                {
                    DarkTabControlHeaderPainter.Detach(tabControl);
                }
            }
            else if (control is TabPage tabPage)
            {
                tabPage.BackColor = useDarkMode ? windowBackColor : SystemColors.Control;
                tabPage.ForeColor = textColor;
            }
            else if (control is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.Fixed3D;
                textBox.BackColor = windowBackColor;
                textBox.ForeColor = textColor;
            }
            else if (control is ComboBox comboBox)
            {
                if (comboBox.Parent is not ToolStrip)
                {
                    comboBox.FlatStyle = useDarkMode ? FlatStyle.Flat : FlatStyle.Standard;
                    comboBox.BackColor = windowBackColor;
                    comboBox.ForeColor = textColor;
                    comboBox.DrawItem -= comboBox_DrawItem;
                    comboBox.DrawMode = useDarkMode
                        ? DrawMode.OwnerDrawFixed
                        : DrawMode.Normal;

                    if (useDarkMode)
                    {
                        comboBox.DrawItem += comboBox_DrawItem;
                    }
                }
            }
            else if (control is Button button)
            {
                button.FlatStyle = useDarkMode ? FlatStyle.Flat : FlatStyle.Standard;
                button.UseVisualStyleBackColor = !useDarkMode;
                button.BackColor = useDarkMode ? controlBackColor : SystemColors.Control;
                button.ForeColor = textColor;
                button.Cursor = Cursors.Default;

                if (useDarkMode)
                {
                    button.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 55);
                    button.FlatAppearance.MouseDownBackColor = Color.FromArgb(65, 65, 65);
                }
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.FlatStyle = useDarkMode ? FlatStyle.Flat : FlatStyle.Standard;
                checkBox.UseVisualStyleBackColor = !useDarkMode;
                checkBox.BackColor = useDarkMode ? windowBackColor : SystemColors.Control;
                checkBox.ForeColor = textColor;
            }
            else if (control is Label label)
            {
                label.BackColor = Color.Transparent;
                label.ForeColor = textColor;
            }
            else if (control is LinkLabel linkLabel)
            {
                linkLabel.BackColor = Color.Transparent;
                linkLabel.ForeColor = textColor;
                linkLabel.LinkColor = useDarkMode ? Color.LightSkyBlue : SystemColors.HotTrack;
                linkLabel.ActiveLinkColor = useDarkMode ? Color.DeepSkyBlue : SystemColors.Highlight;
                linkLabel.VisitedLinkColor = useDarkMode ? Color.Plum : SystemColors.HotTrack;
            }
            else if (control is MenuStrip menuStrip)
            {
                menuStrip.BackColor = controlBackColor;
                menuStrip.ForeColor = textColor;
                menuStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
                menuStrip.Renderer = useDarkMode ? new DarkToolStripRenderer() : null;

                foreach (ToolStripItem item in menuStrip.Items)
                {
                    ApplyWindowsToolStripItem(item, useDarkMode);

                    if (item is ToolStripMenuItem menuItem)
                    {
                        ApplyWindowsMenuItem(menuItem, useDarkMode);
                    }
                }
            }
            else if (control is ToolStrip toolStrip)
            {
                toolStrip.BackColor = controlBackColor;
                toolStrip.ForeColor = textColor;
                toolStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
                toolStrip.Renderer = useDarkMode ? new DarkToolStripRenderer() : null;
                toolStrip.GripStyle = ToolStripGripStyle.Hidden;

                foreach (ToolStripItem item in toolStrip.Items)
                {
                    ApplyWindowsToolStripItem(item, useDarkMode);
                }
            }
            else if (control is StatusStrip statusStrip)
            {
                statusStrip.BackColor = controlBackColor;
                statusStrip.ForeColor = textColor;
                statusStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
                statusStrip.Renderer = useDarkMode ? new DarkToolStripRenderer() : null;

                foreach (ToolStripItem item in statusStrip.Items)
                {
                    ApplyWindowsToolStripItem(item, useDarkMode);
                }
            }
            else if (control is DataGridView dataGridView)
            {
                dataGridView.BackgroundColor = windowBackColor;
                dataGridView.GridColor = gridColor;
                dataGridView.BorderStyle = BorderStyle.Fixed3D;
                dataGridView.EnableHeadersVisualStyles = !useDarkMode;

                dataGridView.DefaultCellStyle.BackColor = windowBackColor;
                dataGridView.DefaultCellStyle.ForeColor = textColor;
                dataGridView.DefaultCellStyle.SelectionBackColor = selectedBackColor;
                dataGridView.DefaultCellStyle.SelectionForeColor = selectedTextColor;

                dataGridView.AlternatingRowsDefaultCellStyle.BackColor = windowBackColor;
                dataGridView.AlternatingRowsDefaultCellStyle.ForeColor = textColor;
                dataGridView.AlternatingRowsDefaultCellStyle.SelectionBackColor = selectedBackColor;
                dataGridView.AlternatingRowsDefaultCellStyle.SelectionForeColor = selectedTextColor;

                dataGridView.ColumnHeadersDefaultCellStyle.BackColor = headerBackColor;
                dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = textColor;
                dataGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBackColor;
                dataGridView.ColumnHeadersDefaultCellStyle.SelectionForeColor = textColor;

                dataGridView.RowHeadersDefaultCellStyle.BackColor = headerBackColor;
                dataGridView.RowHeadersDefaultCellStyle.ForeColor = textColor;
                dataGridView.RowHeadersDefaultCellStyle.SelectionBackColor = headerBackColor;
                dataGridView.RowHeadersDefaultCellStyle.SelectionForeColor = textColor;
            }
            else if (control is TreeView treeView)
            {
                treeView.BackColor = windowBackColor;
                treeView.ForeColor = textColor;
                treeView.BorderStyle = BorderStyle.Fixed3D;
            }
            else if (control is ListView listView)
            {
                listView.BackColor = windowBackColor;
                listView.ForeColor = textColor;
                listView.BorderStyle = BorderStyle.Fixed3D;
                listView.Font = SystemFonts.MessageBoxFont;
            }
            else if (control is Chart_PieChart || control is Chart_BarChart)
            {
                control.BackColor = windowBackColor;
                control.ForeColor = textColor;
                control.Font = SystemFonts.MessageBoxFont;
                control.Invalidate();
            }
            else if (control is SplitContainer splitContainer)
            {
                splitContainer.BackColor = controlBackColor;
                splitContainer.ForeColor = textColor;
            }
            else if (control is SplitterPanel splitterPanel)
            {
                splitterPanel.BackColor = windowBackColor;
                splitterPanel.ForeColor = textColor;
            }
            else if (control is TableLayoutPanel tableLayoutPanel)
            {
                tableLayoutPanel.BackColor = windowBackColor;
                tableLayoutPanel.ForeColor = textColor;
            }
            else if (control is ToolStripPanel toolStripPanel)
            {
                toolStripPanel.BackColor = controlBackColor;
                toolStripPanel.ForeColor = textColor;
            }
            else if (control is Panel panel)
            {
                panel.BackColor = windowBackColor;
                panel.ForeColor = textColor;
            }
            else
            {
                control.BackColor = windowBackColor;
            }

            ApplyNativeScrollBarTheme(control, useDarkMode);

            foreach (Control child in control.Controls)
            {
                ApplyWindowsControl(child, useDarkMode);
            }
        }

        private static void ApplyNativeScrollBarTheme(Control control, bool useDarkMode)
        {
            if (!(control is DataGridView) &&
                !(control is TreeView) &&
                !(control is ListView) &&
                !(control is PropertyGrid) &&
                !(control is RichTextBox) &&
                !(control is TextBox textBox && textBox.Multiline))
            {
                return;
            }

            ApplyNativeScrollBarThemeToHandle(control, useDarkMode);
        }

        private static void ApplyNativeScrollBarThemeToHandle(Control control, bool useDarkMode)
        {
            try
            {
                SetWindowTheme(
                    control.Handle,
                    useDarkMode ? "DarkMode_Explorer" : "Explorer",
                    null);
            }
            catch
            {
            }
        }

        private static void tabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabControl)
                return;

            bool selected = e.Index == tabControl.SelectedIndex;

            Color backColor = selected
                ? Color.FromArgb(32, 32, 32)
                : Color.FromArgb(45, 45, 45);

            Color foreColor = Color.White;
            Color borderColor = Color.FromArgb(120, 120, 120);

            Rectangle tabBounds = tabControl.GetTabRect(e.Index);

            using SolidBrush backBrush = new SolidBrush(backColor);
            e.Graphics.FillRectangle(backBrush, tabBounds);

            using Pen borderPen = new Pen(borderColor);
            e.Graphics.DrawRectangle(borderPen, tabBounds);

            TextRenderer.DrawText(
                e.Graphics,
                tabControl.TabPages[e.Index].Text,
                tabControl.Font,
                tabBounds,
                foreColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static void comboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox comboBox)
                return;

            if (e.Index < 0)
                return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            Color backColor = selected
                ? SystemColors.Highlight
                : Color.FromArgb(32, 32, 32);

            Color foreColor = selected
                ? SystemColors.HighlightText
                : Color.White;

            using SolidBrush backBrush = new SolidBrush(backColor);
            e.Graphics.FillRectangle(backBrush, e.Bounds);

            TextRenderer.DrawText(
                e.Graphics,
                comboBox.Items[e.Index].ToString(),
                e.Font,
                e.Bounds,
                foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private static void ApplyWindowsMenuItem(ToolStripMenuItem item, bool useDarkMode)
        {
            Color backColor = useDarkMode
                ? Color.FromArgb(45, 45, 45)
                : SystemColors.Control;

            Color foreColor = useDarkMode
                ? Color.White
                : SystemColors.ControlText;

            item.BackColor = backColor;
            item.ForeColor = foreColor;
            item.Padding = new Padding(4, 0, 4, 0);

            foreach (ToolStripItem child in item.DropDownItems)
            {
                child.BackColor = backColor;
                child.ForeColor = foreColor;
                child.Padding = new Padding(4, 0, 4, 0);

                if (child is ToolStripMenuItem childMenuItem)
                {
                    ApplyWindowsMenuItem(childMenuItem, useDarkMode);
                }
            }
        }

        private static void ApplyWindowsToolStripItem(ToolStripItem item, bool useDarkMode)
        {
            item.BackColor = useDarkMode
                ? Color.FromArgb(45, 45, 45)
                : SystemColors.Control;

            item.ForeColor = useDarkMode
                ? Color.White
                : SystemColors.ControlText;

            if (item is ToolStripComboBox toolStripComboBox)
            {
                toolStripComboBox.ComboBox.BackColor = useDarkMode
                    ? Color.FromArgb(32, 32, 32)
                    : SystemColors.Window;

                toolStripComboBox.ComboBox.ForeColor = useDarkMode
                    ? Color.White
                    : SystemColors.WindowText;

                toolStripComboBox.ComboBox.Invalidate();
            }
        }

        private static void SetImmersiveDarkMode(Form form, bool enabled)
        {
            if (!form.IsHandleCreated)
            {
                form.HandleCreated += (sender, e) => SetImmersiveDarkMode(form, enabled);
                return;
            }

            int useDarkMode = enabled ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        }

        private sealed class DarkTabControlHeaderPainter : NativeWindow
        {
            private const int WM_PAINT = 0x000F;
            private readonly TabControl _tabControl;

            private DarkTabControlHeaderPainter(TabControl tabControl)
            {
                _tabControl = tabControl;
                AssignHandle(tabControl.Handle);
            }

            public static void Attach(TabControl tabControl)
            {
                if (tabControl.Tag is DarkTabControlHeaderPainter)
                    return;

                tabControl.Tag = new DarkTabControlHeaderPainter(tabControl);
            }

            public static void Detach(TabControl tabControl)
            {
                if (tabControl.Tag is not DarkTabControlHeaderPainter painter)
                    return;

                painter.ReleaseHandle();
                tabControl.Tag = null;
                tabControl.Invalidate();
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg != WM_PAINT)
                    return;

                if (_tabControl.TabPages.Count == 0)
                    return;

                using Graphics graphics = Graphics.FromHwnd(_tabControl.Handle);

                int headerHeight = _tabControl.DisplayRectangle.Top;
                Rectangle lastTabBounds = _tabControl.GetTabRect(_tabControl.TabPages.Count - 1);

                Rectangle headerBounds = new Rectangle(
                    lastTabBounds.Right,
                    0,
                    Math.Max(0, _tabControl.Width - lastTabBounds.Right),
                    headerHeight);

                using SolidBrush headerBrush = new SolidBrush(Color.FromArgb(32, 32, 32));
                graphics.FillRectangle(headerBrush, headerBounds);

                using Pen borderPen = new Pen(Color.FromArgb(120, 120, 120));
                graphics.DrawLine(borderPen, 0, headerHeight - 1, _tabControl.Width, headerHeight - 1);
            }
        }

        private sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
        {
            public DarkToolStripRenderer()
                : base(new DarkProfessionalColorTable())
            {
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using SolidBrush brush = new SolidBrush(Color.FromArgb(45, 45, 45));
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);

                Color backColor = e.Item.Selected
                    ? Color.FromArgb(65, 65, 65)
                    : Color.FromArgb(45, 45, 45);

                using SolidBrush brush = new SolidBrush(backColor);
                e.Graphics.FillRectangle(brush, bounds);
            }

            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);

                Color backColor;

                if (e.Item.Pressed || (e.Item is ToolStripButton button && button.Checked))
                {
                    backColor = Color.FromArgb(72, 72, 72);
                }
                else if (e.Item.Selected)
                {
                    backColor = Color.FromArgb(65, 65, 65);
                }
                else
                {
                    backColor = Color.FromArgb(45, 45, 45);
                }

                using SolidBrush brush = new SolidBrush(backColor);
                e.Graphics.FillRectangle(brush, bounds);

                if (e.Item.Selected || e.Item.Pressed)
                {
                    using Pen borderPen = new Pen(Color.FromArgb(95, 95, 95));
                    e.Graphics.DrawRectangle(
                        borderPen,
                        0,
                        0,
                        Math.Max(0, bounds.Width - 1),
                        Math.Max(0, bounds.Height - 1));
                }
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = Color.White;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using Pen pen = new Pen(Color.FromArgb(80, 80, 80));
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
            }

            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
            {
                using SolidBrush brush = new SolidBrush(Color.FromArgb(45, 45, 45));
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using Pen pen = new Pen(Color.FromArgb(80, 80, 80));
                Rectangle bounds = new Rectangle(Point.Empty, e.ToolStrip.Size - new Size(1, 1));
                e.Graphics.DrawRectangle(pen, bounds);
            }
        }

        private sealed class DarkProfessionalColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 45);
            public override Color MenuBorder => Color.FromArgb(80, 80, 80);
            public override Color MenuItemBorder => Color.FromArgb(80, 80, 80);
            public override Color MenuItemSelected => Color.FromArgb(65, 65, 65);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(65, 65, 65);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(65, 65, 65);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(65, 65, 65);
            public override Color MenuItemPressedGradientMiddle => Color.FromArgb(65, 65, 65);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(65, 65, 65);
            public override Color ImageMarginGradientBegin => Color.FromArgb(45, 45, 45);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(45, 45, 45);
            public override Color ImageMarginGradientEnd => Color.FromArgb(45, 45, 45);
            public override Color ToolStripBorder => Color.FromArgb(80, 80, 80);
            public override Color ToolStripGradientBegin => Color.FromArgb(45, 45, 45);
            public override Color ToolStripGradientMiddle => Color.FromArgb(45, 45, 45);
            public override Color ToolStripGradientEnd => Color.FromArgb(45, 45, 45);
        }
    }
}