using System;
using System.Windows.Forms;

namespace c2flux
{
    public static class AppDialogs
    {
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

        public static ElevationPromptResult ShowElevationPrompt(AppSettings settings)
        {
            using DialogForm dialogForm = new DialogForm(
                settings,
                AppConstants.ApplicationName,
                LocalizationService.Format(
                    "Elevation.Message",
                    AppConstants.ApplicationName),
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
            }

            private void InitializeComponent()
            {
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new System.Drawing.Size(430, 178);
                MinimumSize = Size;
                MaximumSize = Size;
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
            }

            private void InitializeComponent()
            {
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new System.Drawing.Size(430, 178);
                MinimumSize = Size;
                MaximumSize = Size;
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

        private sealed class DialogForm : Form
        {
            private readonly AppSettings _settings;
            private readonly string _messageText;
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
                string checkBoxText,
                string yesButtonText,
                string noButtonText)
            {
                _settings = settings;
                _messageText = messageText;
                _checkBoxText = checkBoxText;
                _yesButtonText = yesButtonText;
                _noButtonText = noButtonText;

                Text = title;

                InitializeComponent();
                AntdThemeService.Apply(this, _settings.Layout);
                ApplyImportantLabelStyle();
            }

            private void InitializeComponent()
            {
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new System.Drawing.Size(480, 220);
                MinimumSize = Size;
                MaximumSize = Size;
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
                    Text =
                        "Important: MFT scanning is much faster but requires" +
                        Environment.NewLine +
                        "administrator rights.",
                    Location = new System.Drawing.Point(78, 82),
                    Size = new System.Drawing.Size(378, 52),
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                checkBoxOption = new AntdUI.Checkbox
                {
                    Name = "checkBoxOption",
                    Text = _checkBoxText,
                    Location = new System.Drawing.Point(24, 142),
                    Size = new System.Drawing.Size(300, 28),
                    BackColor = AntdThemeService.BackgroundPrimary,
                    ForeColor = AntdThemeService.TextPrimary
                };

                buttonYes = new AntdUI.Button
                {
                    Name = "buttonYes",
                    Text = _yesButtonText,
                    Location = new System.Drawing.Point(294, 178),
                    Size = new System.Drawing.Size(84, 32),
                    Type = AntdUI.TTypeMini.Default,
                    DialogResult = DialogResult.Yes
                };

                buttonNo = new AntdUI.Button
                {
                    Name = "buttonNo",
                    Text = _noButtonText,
                    Location = new System.Drawing.Point(386, 178),
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