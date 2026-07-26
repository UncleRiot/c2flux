using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace c2flux
{
    internal static class RightPaneLayoutDiagnostics
    {
        private static readonly object SyncRoot = new object();

        private static Form _mainForm;
        private static SplitContainer _splitContainerMain;
        private static Control _panelRightViewHost;
        private static Timer _delayedSnapshotTimer;
        private static string _lastReason;

        private static string LogFilePath =>
            Path.Combine(AppContext.BaseDirectory, "right-pane-layout.log");

        public static void Attach(
            Form mainForm,
            SplitContainer splitContainerMain,
            Control panelRightViewHost)
        {
            _mainForm = mainForm;
            _splitContainerMain = splitContainerMain;
            _panelRightViewHost = panelRightViewHost;

            _delayedSnapshotTimer = new Timer
            {
                Interval = 250
            };

            _delayedSnapshotTimer.Tick += DelayedSnapshotTimer_Tick;

            SubscribeControlTree(_mainForm);
            WriteSnapshot("Attach");
        }

        private static void SubscribeControlTree(Control control)
        {
            control.SizeChanged -= Control_SizeChanged;
            control.SizeChanged += Control_SizeChanged;

            control.VisibleChanged -= Control_VisibleChanged;
            control.VisibleChanged += Control_VisibleChanged;

            control.ControlAdded -= Control_ControlAdded;
            control.ControlAdded += Control_ControlAdded;

            foreach (Control child in control.Controls)
            {
                SubscribeControlTree(child);
            }
        }

        private static void Control_SizeChanged(object sender, EventArgs e)
        {
            Control control = sender as Control;

            if (!IsRelevant(control))
                return;

            ScheduleSnapshot(
                "SizeChanged: " +
                GetControlName(control));
        }

        private static void Control_VisibleChanged(object sender, EventArgs e)
        {
            Control control = sender as Control;

            if (!IsRelevant(control))
                return;

            ScheduleSnapshot(
                "VisibleChanged: " +
                GetControlName(control) +
                " = " +
                control.Visible);
        }

        private static void Control_ControlAdded(object sender, ControlEventArgs e)
        {
            SubscribeControlTree(e.Control);

            if (!IsRelevant(e.Control))
                return;

            ScheduleSnapshot(
                "ControlAdded: " +
                GetControlName(e.Control));
        }

        private static bool IsRelevant(Control control)
        {
            if (control == null)
                return false;

            if (ReferenceEquals(control, _mainForm) ||
                ReferenceEquals(control, _splitContainerMain) ||
                ReferenceEquals(control, _splitContainerMain.Panel2) ||
                ReferenceEquals(control, _panelRightViewHost))
            {
                return true;
            }

            return IsDescendantOf(control, _panelRightViewHost);
        }

        private static bool IsDescendantOf(
            Control control,
            Control possibleParent)
        {
            Control current = control.Parent;

            while (current != null)
            {
                if (ReferenceEquals(current, possibleParent))
                    return true;

                current = current.Parent;
            }

            return false;
        }

        private static void ScheduleSnapshot(string reason)
        {
            _lastReason = reason;

            WriteSnapshot(reason + " [sofort]");

            _delayedSnapshotTimer.Stop();
            _delayedSnapshotTimer.Start();
        }

        private static void DelayedSnapshotTimer_Tick(
            object sender,
            EventArgs e)
        {
            _delayedSnapshotTimer.Stop();
            WriteSnapshot(_lastReason + " [nach 250 ms]");
        }

        private static void WriteSnapshot(string reason)
        {
            if (_mainForm == null ||
                _splitContainerMain == null ||
                _panelRightViewHost == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendLine(
                "============================================================");
            builder.AppendLine(
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                " | " +
                reason);

            AppendControl(builder, _mainForm, 0);
            AppendControl(builder, _splitContainerMain, 1);
            AppendControl(builder, _splitContainerMain.Panel2, 2);
            AppendControl(builder, _panelRightViewHost, 3);

            foreach (Control child in _panelRightViewHost.Controls)
            {
                AppendControlTree(builder, child, 4);
            }

            builder.AppendLine();

            lock (SyncRoot)
            {
                File.AppendAllText(
                    LogFilePath,
                    builder.ToString(),
                    Encoding.UTF8);
            }
        }

        private static void AppendControlTree(
            StringBuilder builder,
            Control control,
            int depth)
        {
            AppendControl(builder, control, depth);

            foreach (Control child in control.Controls)
            {
                AppendControlTree(builder, child, depth + 1);
            }
        }

        private static void AppendControl(
            StringBuilder builder,
            Control control,
            int depth)
        {
            Rectangle screenBounds = control.RectangleToScreen(
                control.ClientRectangle);

            builder.Append(' ', depth * 2);
            builder.Append(GetControlName(control));
            builder.Append(" | Type=");
            builder.Append(control.GetType().FullName);
            builder.Append(" | Visible=");
            builder.Append(control.Visible);
            builder.Append(" | Dock=");
            builder.Append(control.Dock);
            builder.Append(" | Anchor=");
            builder.Append(control.Anchor);
            builder.Append(" | Bounds=");
            builder.Append(control.Bounds);
            builder.Append(" | ClientSize=");
            builder.Append(control.ClientSize);
            builder.Append(" | MinimumSize=");
            builder.Append(control.MinimumSize);
            builder.Append(" | MaximumSize=");
            builder.Append(control.MaximumSize);
            builder.Append(" | PreferredSize=");
            builder.Append(control.PreferredSize);
            builder.Append(" | AutoSize=");
            builder.Append(control.AutoSize);
            builder.Append(" | ScreenClientBounds=");
            builder.Append(screenBounds);
            builder.AppendLine();
        }

        private static string GetControlName(Control control)
        {
            if (!string.IsNullOrWhiteSpace(control.Name))
                return control.Name;

            return control.GetType().Name;
        }
    }
}
