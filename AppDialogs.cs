// Last comment Update 2026-08-21 09:20
using System;
using System.Windows.Forms;

namespace c2flux
{
    public static class AppDialogs
    {
        // Central entry point for application dialogs.
        // Visual styling must come from AntdThemeService; all visible UI text must use LocalizationService or
        // already-localized parameters.
        private const int IDI_QUESTION = 32514;
        private const int IDI_WARNING = 32515;
        private const int DI_NORMAL = 0x0003;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DrawIconEx(
            IntPtr hdc,
            int xLeft,
            int yTop,
            IntPtr hIcon,
            int cxWidth,
            int cyWidth,
            int istepIfAniCur,
            IntPtr hbrFlickerFreeDraw,
            int diFlags);

        // Uses the current AntdUI theme when no AppSettings instance is available.
        public static DialogResult ShowWarningOk(
            string messageText,
            string title,
            string okButtonText)
        {
            AppSettings settings = new AppSettings
            {
                Layout =
                    AntdUI.Config.Mode == AntdUI.TMode.Dark
                        ? AppLayout.WindowsDarkMode
                        : AppLayout.WindowsLightMode
            };

            return ShowWarningOk(
                settings,
                messageText,
                title,
                okButtonText);
        }

        public static DialogResult ShowWarningOk(
            AppSettings settings,
            string messageText,
            string title,
            string okButtonText)
        {
            using WarningOkDialogForm dialogForm =
                new WarningOkDialogForm(
                    settings,
                    title,
                    messageText,
                    okButtonText);

            return dialogForm.ShowDialog();
        }

        public static DialogResult ShowWarningYesNo(
            IWin32Window owner,
            AppSettings settings,
            string messageText,
            string title,
            string yesButtonText,
            string noButtonText)
        {
            using WarningYesNoDialogForm dialogForm =
                new WarningYesNoDialogForm(
                    settings,
                    title,
                    messageText,
                    yesButtonText,
                    noButtonText);

            return dialogForm.ShowDialog(owner);
        }

        // Returns both the user's elevation choice and the "do not show again" state.
        public static ElevationPromptResult ShowElevationPrompt(AppSettings settings)
        {
            using DialogForm dialogForm = new DialogForm(
                settings,
                AppConstants.ApplicationName,
                LocalizationService.Format(
                    "Elevation.Message",
                    AppConstants.ApplicationName),
                LocalizationService.GetText("Elevation.Important"),
                LocalizationService.GetText("Elevation.DoNotShowAgain"),
                LocalizationService.GetText("Common.Yes"),
                LocalizationService.GetText("Common.No"));

            DialogResult dialogResult = dialogForm.ShowDialog();

            return new ElevationPromptResult(
                dialogResult == DialogResult.Yes,
                dialogForm.IsCheckBoxChecked);
        }

        public readonly struct ElevationPromptResult
        {
            public ElevationPromptResult(bool shouldRestartElevated, bool doNotShowAgain)
            {
                ShouldRestartElevated = shouldRestartElevated;
                DoNotShowAgain = doNotShowAgain;
            }

            public bool ShouldRestartElevated { get; }
            public bool DoNotShowAgain { get; }
        }

        public static DialogResult ShowSelectFolderDialog(
            IWin32Window owner,
            AppSettings settings,
            string title,
            out string selectedPath)
        {
            using AppFileDialog dialog =
                new AppFileDialog(
                    settings,
                    AppFileDialogMode.SelectFolder,
                    title,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    LocalizationService.GetText(
                        "Dialog.SelectFolder"));

            DialogResult result =
                owner == null
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(owner);

            selectedPath =
                result == DialogResult.OK
                    ? dialog.SelectedPath
                    : string.Empty;

            return result;
        }

        public static DialogResult ShowOpenFileDialog(
            IWin32Window owner,
            AppSettings settings,
            string title,
            string filter,
            out string fileName)
        {
            using AppFileDialog dialog =
                new AppFileDialog(
                    settings,
                    AppFileDialogMode.OpenFile,
                    title,
                    filter,
                    string.Empty,
                    string.Empty,
                    LocalizationService.GetText(
                        "Toolbar.Open"));

            DialogResult result =
                owner == null
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(owner);

            fileName =
                result == DialogResult.OK
                    ? dialog.SelectedPath
                    : string.Empty;

            return result;
        }

        public static DialogResult ShowSaveFileDialog(
            IWin32Window owner,
            AppSettings settings,
            string title,
            string filter,
            string defaultExtension,
            string initialFileName,
            string confirmButtonText,
            out string fileName)
        {
            using AppFileDialog dialog =
                new AppFileDialog(
                    settings,
                    AppFileDialogMode.SaveFile,
                    title,
                    filter,
                    defaultExtension,
                    initialFileName,
                    confirmButtonText);

            DialogResult result =
                owner == null
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(owner);

            fileName =
                result == DialogResult.OK
                    ? dialog.SelectedPath
                    : string.Empty;

            return result;
        }

        // Fixed warning dialog using the shared application theme.
        private sealed class WarningOkDialogForm : Form
        {
            private readonly AppSettings _settings;
            private readonly string _messageText;
            private readonly string _okButtonText;

            private NativeWarningIconControl nativeWarningIconControl;
            private AntdUI.Label labelMessage;
            private AntdUI.Button buttonOk;

            public WarningOkDialogForm(
                AppSettings settings,
                string title,
                string messageText,
                string okButtonText)
            {
                _settings = settings;
                _messageText = messageText;
                _okButtonText = okButtonText;

                Text = title;

                InitializeComponent();
                AntdThemeService.Apply(this, _settings.Layout);
                Shown += Dialog_Shown;
            }

            private void Dialog_Shown(
                object sender,
                EventArgs e)
            {
                SuspendLayout();

                try
                {
                    MinimumSize = System.Drawing.Size.Empty;
                    MaximumSize = System.Drawing.Size.Empty;

                    PerformAutoScale();
                    PerformLayout();

                    MinimumSize = Size;
                    MaximumSize = Size;

                    Invalidate(true);
                    Update();
                }
                finally
                {
                    ResumeLayout(true);
                }
            }

            private void InitializeComponent()
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);

                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new System.Drawing.Size(430, 178);
                MinimumSize = System.Drawing.Size.Empty;
                MaximumSize = System.Drawing.Size.Empty;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                BackColor = AntdThemeService.BackgroundPrimary;
                ForeColor = AntdThemeService.TextPrimary;

                nativeWarningIconControl = new NativeWarningIconControl
                {
                    Name = "nativeWarningIconControl",
                    Location = new System.Drawing.Point(28, 42),
                    Size = new System.Drawing.Size(32, 32)
                };

                labelMessage = new AntdUI.Label
                {
                    Name = "labelMessage",
                    Text = _messageText,
                    Location = new System.Drawing.Point(82, 28),
                    Size = new System.Drawing.Size(324, 60),
                    ForeColor = AntdThemeService.TextPrimary,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                buttonOk = new AntdUI.Button
                {
                    Name = "buttonOk",
                    Text = _okButtonText,
                    Location = new System.Drawing.Point(326, 122),
                    Size = new System.Drawing.Size(84, 32),
                    Type = AntdUI.TTypeMini.Primary,
                    DialogResult = DialogResult.OK
                };

                Controls.Add(nativeWarningIconControl);
                Controls.Add(labelMessage);
                Controls.Add(buttonOk);

                AcceptButton = buttonOk;
                CancelButton = buttonOk;
            }
        }

        // Fixed Yes/No warning dialog using the shared application theme.
        private sealed class WarningYesNoDialogForm : Form
        {
            private readonly AppSettings _settings;
            private readonly string _messageText;
            private readonly string _yesButtonText;
            private readonly string _noButtonText;

            private NativeWarningIconControl nativeWarningIconControl;
            private AntdUI.Label labelMessage;
            private AntdUI.Button buttonYes;
            private AntdUI.Button buttonNo;

            public WarningYesNoDialogForm(
                AppSettings settings,
                string title,
                string messageText,
                string yesButtonText,
                string noButtonText)
            {
                _settings = settings;
                _messageText = messageText;
                _yesButtonText = yesButtonText;
                _noButtonText = noButtonText;

                Text = title;

                InitializeComponent();
                AntdThemeService.Apply(this, _settings.Layout);
                Shown += Dialog_Shown;
            }

            private void Dialog_Shown(
                object sender,
                EventArgs e)
            {
                SuspendLayout();

                try
                {
                    MinimumSize = System.Drawing.Size.Empty;
                    MaximumSize = System.Drawing.Size.Empty;

                    PerformAutoScale();
                    PerformLayout();

                    MinimumSize = Size;
                    MaximumSize = Size;

                    Invalidate(true);
                    Update();
                }
                finally
                {
                    ResumeLayout(true);
                }
            }

            private void InitializeComponent()
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);

                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new System.Drawing.Size(430, 178);
                MinimumSize = System.Drawing.Size.Empty;
                MaximumSize = System.Drawing.Size.Empty;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                BackColor = AntdThemeService.BackgroundPrimary;
                ForeColor = AntdThemeService.TextPrimary;

                nativeWarningIconControl = new NativeWarningIconControl
                {
                    Name = "nativeWarningIconControl",
                    Location = new System.Drawing.Point(28, 42),
                    Size = new System.Drawing.Size(32, 32)
                };

                labelMessage = new AntdUI.Label
                {
                    Name = "labelMessage",
                    Text = _messageText,
                    Location = new System.Drawing.Point(82, 28),
                    Size = new System.Drawing.Size(324, 60),
                    ForeColor = AntdThemeService.TextPrimary,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                buttonYes = new AntdUI.Button
                {
                    Name = "buttonYes",
                    Text = _yesButtonText,
                    Location = new System.Drawing.Point(232, 122),
                    Size = new System.Drawing.Size(84, 32),
                    Type = AntdUI.TTypeMini.Default,
                    DialogResult = DialogResult.Yes
                };

                buttonNo = new AntdUI.Button
                {
                    Name = "buttonNo",
                    Text = _noButtonText,
                    Location = new System.Drawing.Point(326, 122),
                    Size = new System.Drawing.Size(84, 32),
                    Type = AntdUI.TTypeMini.Primary,
                    DialogResult = DialogResult.No
                };

                Controls.Add(nativeWarningIconControl);
                Controls.Add(labelMessage);
                Controls.Add(buttonYes);
                Controls.Add(buttonNo);

                AcceptButton = buttonYes;
                CancelButton = buttonNo;
            }
        }

        // Elevation prompt with an additional persisted user choice.
        private sealed class DialogForm : Form
        {
            private readonly AppSettings _settings;
            private readonly string _messageText;
            private readonly string _importantText;
            private readonly string _checkBoxText;
            private readonly string _yesButtonText;
            private readonly string _noButtonText;

            private NativeQuestionIconControl nativeQuestionIconControl;
            private AntdUI.Label labelMessage;
            private AntdUI.Label labelImportant;
            private AntdUI.Checkbox checkBoxOption;
            private AntdUI.Button buttonYes;
            private AntdUI.Button buttonNo;

            public bool IsCheckBoxChecked
            {
                get { return checkBoxOption.Checked; }
            }

            public DialogForm(
                AppSettings settings,
                string title,
                string messageText,
                string importantText,
                string checkBoxText,
                string yesButtonText,
                string noButtonText)
            {
                _settings = settings;
                _messageText = messageText;
                _importantText = importantText;
                _checkBoxText = checkBoxText;
                _yesButtonText = yesButtonText;
                _noButtonText = noButtonText;

                Text = title;

                InitializeComponent();
                AntdThemeService.Apply(this, _settings.Layout);
                ApplyThemeColors();
                ApplyImportantLabelStyle();
                Shown += Dialog_Shown;
            }

            private void Dialog_Shown(
                object sender,
                EventArgs e)
            {
                SuspendLayout();

                try
                {
                    MinimumSize = System.Drawing.Size.Empty;
                    MaximumSize = System.Drawing.Size.Empty;

                    if (DeviceDpi >= 144)
                    {
                        int rightMargin = Math.Max(
                            12,
                            ClientSize.Width -
                            labelMessage.Right);
                        int messageImportantGap = Math.Max(
                            6,
                            labelImportant.Top -
                            labelMessage.Bottom);
                        int importantCheckBoxGap = Math.Max(
                            6,
                            checkBoxOption.Top -
                            labelImportant.Bottom);
                        int checkBoxButtonGap = Math.Max(
                            6,
                            buttonYes.Top -
                            checkBoxOption.Bottom);
                        int buttonGap = Math.Max(
                            6,
                            buttonNo.Left -
                            buttonYes.Right);
                        int bottomMargin = Math.Max(
                            10,
                            ClientSize.Height -
                            buttonNo.Bottom);

                        int checkBoxTextWidth =
                            TextRenderer.MeasureText(
                                _checkBoxText ?? string.Empty,
                                checkBoxOption.Font,
                                System.Drawing.Size.Empty,
                                TextFormatFlags.SingleLine |
                                TextFormatFlags.NoPadding).Width;

                        int requiredClientWidth = Math.Max(
                            ClientSize.Width,
                            checkBoxOption.Left +
                            checkBoxTextWidth +
                            rightMargin +
                            24);

                        if (requiredClientWidth >
                            ClientSize.Width)
                        {
                            ClientSize =
                                new System.Drawing.Size(
                                    requiredClientWidth,
                                    ClientSize.Height);
                        }

                        int textWidth = Math.Max(
                            1,
                            ClientSize.Width -
                            labelMessage.Left -
                            rightMargin);

                        labelMessage.Width = textWidth;
                        labelImportant.Width = textWidth;

                        int messageHeight =
                            TextRenderer.MeasureText(
                                _messageText ?? string.Empty,
                                labelMessage.Font,
                                new System.Drawing.Size(
                                    textWidth,
                                    int.MaxValue),
                                TextFormatFlags.WordBreak |
                                TextFormatFlags.NoPadding).Height;

                        labelMessage.Height = Math.Max(
                            labelMessage.Height,
                            messageHeight + 8);

                        labelImportant.Top =
                            labelMessage.Bottom +
                            messageImportantGap;

                        int importantHeight =
                            TextRenderer.MeasureText(
                                _importantText ?? string.Empty,
                                labelImportant.Font,
                                new System.Drawing.Size(
                                    textWidth,
                                    int.MaxValue),
                                TextFormatFlags.WordBreak |
                                TextFormatFlags.NoPadding).Height;

                        labelImportant.Height = Math.Max(
                            labelImportant.Height,
                            importantHeight + 8);

                        checkBoxOption.Top =
                            labelImportant.Bottom +
                            importantCheckBoxGap;
                        checkBoxOption.Width = Math.Max(
                            checkBoxOption.Width,
                            ClientSize.Width -
                            checkBoxOption.Left -
                            rightMargin);

                        buttonYes.Top =
                            checkBoxOption.Bottom +
                            checkBoxButtonGap;
                        buttonNo.Top = buttonYes.Top;

                        buttonNo.Left =
                            ClientSize.Width -
                            rightMargin -
                            buttonNo.Width;
                        buttonYes.Left =
                            buttonNo.Left -
                            buttonGap -
                            buttonYes.Width;

                        ClientSize =
                            new System.Drawing.Size(
                                ClientSize.Width,
                                buttonNo.Bottom +
                                bottomMargin);
                    }

                    PerformLayout();

                    MinimumSize = Size;
                    MaximumSize = Size;

                    Invalidate(true);
                    Update();
                }
                finally
                {
                    ResumeLayout(true);
                }
            }

            private void InitializeComponent()
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);

                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new System.Drawing.Size(480, 250);
                MinimumSize = System.Drawing.Size.Empty;
                MaximumSize = System.Drawing.Size.Empty;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                BackColor = AntdThemeService.BackgroundPrimary;
                ForeColor = AntdThemeService.TextPrimary;

                nativeQuestionIconControl = new NativeQuestionIconControl
                {
                    Name = "nativeQuestionIconControl",
                    Location = new System.Drawing.Point(24, 37),
                    Size = new System.Drawing.Size(32, 32)
                };

                labelMessage = new AntdUI.Label
                {
                    Name = "labelMessage",
                    Text = _messageText,
                    Location = new System.Drawing.Point(78, 20),
                    Size = new System.Drawing.Size(378, 64),
                    ForeColor = AntdThemeService.TextPrimary,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                labelImportant = new AntdUI.Label
                {
                    Name = "labelImportant",
                    Text = _importantText,
                    Location = new System.Drawing.Point(78, 94),
                    Size = new System.Drawing.Size(378, 64),
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                checkBoxOption = new AntdUI.Checkbox
                {
                    Name = "checkBoxOption",
                    Text = _checkBoxText,
                    Location = new System.Drawing.Point(24, 170),
                    Size = new System.Drawing.Size(300, 28),
                    BackColor = AntdThemeService.BackgroundPrimary,
                    ForeColor = AntdThemeService.TextPrimary
                };

                buttonYes = new AntdUI.Button
                {
                    Name = "buttonYes",
                    Text = _yesButtonText,
                    Location = new System.Drawing.Point(294, 208),
                    Size = new System.Drawing.Size(84, 32),
                    Type = AntdUI.TTypeMini.Default,
                    DialogResult = DialogResult.Yes
                };

                buttonNo = new AntdUI.Button
                {
                    Name = "buttonNo",
                    Text = _noButtonText,
                    Location = new System.Drawing.Point(386, 208),
                    Size = new System.Drawing.Size(84, 32),
                    Type = AntdUI.TTypeMini.Primary,
                    DialogResult = DialogResult.No
                };

                Controls.Add(nativeQuestionIconControl);
                Controls.Add(labelMessage);
                Controls.Add(labelImportant);
                Controls.Add(checkBoxOption);
                Controls.Add(buttonYes);
                Controls.Add(buttonNo);

                AcceptButton = buttonYes;
                CancelButton = buttonNo;
            }

            private void ApplyThemeColors()
            {
                BackColor = AntdThemeService.BackgroundPrimary;
                ForeColor = AntdThemeService.TextPrimary;

                labelMessage.ForeColor = AntdThemeService.TextPrimary;

                checkBoxOption.BackColor = AntdThemeService.BackgroundPrimary;
                checkBoxOption.ForeColor = AntdThemeService.TextPrimary;
            }

            private void ApplyImportantLabelStyle()
            {
                labelImportant.Font = new System.Drawing.Font(
                    labelImportant.Font.FontFamily,
                    labelImportant.Font.Size,
                    System.Drawing.FontStyle.Bold);

                labelImportant.ForeColor =
                    BackColor.GetBrightness() < 0.5f
                        ? System.Drawing.Color.Gold
                        : System.Drawing.Color.Red;
            }
        }

        // Draws the native Windows warning icon without external image assets.
        private sealed class NativeWarningIconControl : Control
        {
            public NativeWarningIconControl()
            {
                SetStyle(ControlStyles.UserPaint, true);
                SetStyle(ControlStyles.AllPaintingInWmPaint, true);
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Parent != null)
                {
                    using System.Drawing.SolidBrush backgroundBrush =
                        new System.Drawing.SolidBrush(Parent.BackColor);

                    e.Graphics.FillRectangle(
                        backgroundBrush,
                        ClientRectangle);
                }

                IntPtr warningIconHandle =
                    LoadIcon(
                        IntPtr.Zero,
                        new IntPtr(IDI_WARNING));

                if (warningIconHandle == IntPtr.Zero)
                    return;

                IntPtr hdc = e.Graphics.GetHdc();

                try
                {
                    DrawIconEx(
                        hdc,
                        0,
                        0,
                        warningIconHandle,
                        32,
                        32,
                        0,
                        IntPtr.Zero,
                        DI_NORMAL);
                }
                finally
                {
                    e.Graphics.ReleaseHdc(hdc);
                }
            }
        }

        // Draws the native Windows question icon without external image assets.
        private sealed class NativeQuestionIconControl : Control
        {
            public NativeQuestionIconControl()
            {
                SetStyle(ControlStyles.UserPaint, true);
                SetStyle(ControlStyles.AllPaintingInWmPaint, true);
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Parent != null)
                {
                    using System.Drawing.SolidBrush backgroundBrush = new System.Drawing.SolidBrush(Parent.BackColor);
                    e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
                }

                IntPtr questionIconHandle = LoadIcon(IntPtr.Zero, new IntPtr(IDI_QUESTION));

                if (questionIconHandle == IntPtr.Zero)
                    return;

                IntPtr hdc = e.Graphics.GetHdc();

                try
                {
                    DrawIconEx(
                        hdc,
                        0,
                        0,
                        questionIconHandle,
                        32,
                        32,
                        0,
                        IntPtr.Zero,
                        DI_NORMAL);
                }
                finally
                {
                    e.Graphics.ReleaseHdc(hdc);
                }
            }
        }
    }
}