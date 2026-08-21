// last comment update 2026-08-21, 09:12
﻿using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class AboutForm : Form
    {
        // UI design, spacing, colors and sizing must follow AntdThemeService.
        // Do not introduce local visual constants unless technically required.

        private readonly AppSettings _settings;

        private PictureBox pictureBoxMolotov;
        private Label labelTitle;
        private Label labelCopyright;
        private Label labelVersion;
        private LinkLabel linkLabelUpdate;
        private LinkLabel linkLabelGithub;
        private LinkLabel linkLabelHelp;
        private Label labelKoFiText;
        private PictureBox pictureBoxKoFi;
        private AntdUI.Button buttonOk;

        public AboutForm(AppSettings settings)
        {
            _settings = settings;

            InitializeComponent();
            AntdThemeService.Apply(this, _settings.Layout);
            ConfigureImageBackgrounds();
            ConfigureLinkColors();

            if (_settings.AutoCheckForUpdates)
            {
                _ = UpdateGitHubStatusAsync();
            }
            else
            {
                linkLabelUpdate.Text =
                    LocalizationService.GetText("About.UpdateCheckDisabled");
                linkLabelUpdate.Enabled = false;
            }

            Shown += AboutForm_Shown;
        }

        // High-DPI fallback layout for 150%+ scaling.
        private void AboutForm_Shown(
            object sender,
            EventArgs e)
        {
            if (DeviceDpi < 144)
                return;

            SuspendLayout();

            try
            {
                MinimumSize = System.Drawing.Size.Empty;
                MaximumSize = System.Drawing.Size.Empty;

                int scale(int logicalPixels)
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

                int rightMargin = scale(20);
                int textGap = scale(4);
                int sectionGap = scale(18);
                int bottomMargin = scale(16);

                int requiredLinkWidth = Math.Max(
                    TextRenderer.MeasureText(
                        linkLabelGithub.Text ?? string.Empty,
                        linkLabelGithub.Font,
                        System.Drawing.Size.Empty,
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPadding).Width,
                    TextRenderer.MeasureText(
                        linkLabelHelp.Text ?? string.Empty,
                        linkLabelHelp.Font,
                        System.Drawing.Size.Empty,
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPadding).Width);

                int requiredClientWidth = Math.Max(
                    ClientSize.Width,
                    linkLabelGithub.Left +
                    requiredLinkWidth +
                    rightMargin);

                if (requiredClientWidth > ClientSize.Width)
                {
                    ClientSize = new System.Drawing.Size(
                        requiredClientWidth,
                        ClientSize.Height);
                }

                int textWidth = Math.Max(
                    1,
                    ClientSize.Width -
                    labelTitle.Left -
                    rightMargin);

                labelTitle.MaximumSize =
                    new System.Drawing.Size(
                        textWidth,
                        0);
                labelCopyright.MaximumSize =
                    new System.Drawing.Size(
                        textWidth,
                        0);
                labelVersion.MaximumSize =
                    new System.Drawing.Size(
                        textWidth,
                        0);
                linkLabelUpdate.MaximumSize =
                    new System.Drawing.Size(
                        textWidth,
                        0);
                linkLabelGithub.MaximumSize =
                    new System.Drawing.Size(
                        textWidth,
                        0);
                linkLabelHelp.MaximumSize =
                    new System.Drawing.Size(
                        textWidth,
                        0);

                labelCopyright.Top =
                    labelTitle.Bottom +
                    textGap;

                labelVersion.Top =
                    labelCopyright.Bottom +
                    textGap;

                linkLabelUpdate.Top =
                    labelVersion.Bottom +
                    textGap;

                linkLabelGithub.Top =
                    linkLabelUpdate.Bottom +
                    textGap;

                linkLabelHelp.Top =
                    linkLabelGithub.Bottom +
                    textGap;

                int lowerTextWidth = Math.Max(
                    1,
                    ClientSize.Width -
                    labelKoFiText.Left -
                    rightMargin);

                labelKoFiText.Width =
                    lowerTextWidth;

                int requiredKoFiTextHeight =
                    TextRenderer.MeasureText(
                        labelKoFiText.Text ?? string.Empty,
                        labelKoFiText.Font,
                        new System.Drawing.Size(
                            lowerTextWidth,
                            int.MaxValue),
                        TextFormatFlags.WordBreak |
                        TextFormatFlags.NoPadding).Height;

                labelKoFiText.Height = Math.Max(
                    labelKoFiText.Height,
                    requiredKoFiTextHeight +
                    scale(4));

                labelKoFiText.Top =
                    linkLabelHelp.Bottom +
                    sectionGap;

                pictureBoxKoFi.Top =
                    labelKoFiText.Bottom +
                    scale(12);

                buttonOk.Left =
                    ClientSize.Width -
                    rightMargin -
                    buttonOk.Width;

                buttonOk.Top =
                    pictureBoxKoFi.Bottom -
                    buttonOk.Height;

                int requiredClientHeight =
                    Math.Max(
                        pictureBoxKoFi.Bottom,
                        buttonOk.Bottom) +
                    bottomMargin;

                if (requiredClientHeight > ClientSize.Height)
                {
                    ClientSize = new System.Drawing.Size(
                        ClientSize.Width,
                        requiredClientHeight);
                }

                MinimumSize = Size;
                MaximumSize = Size;

                PerformLayout();
                Invalidate(true);
                Update();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        // Keeps embedded images visually compatible with the active theme.
        private void ConfigureImageBackgrounds()
        {
            pictureBoxMolotov.BackColor = Color.Transparent;
            pictureBoxKoFi.BackColor = Color.Transparent;
        }

        // Link colors adapt to the current light/dark theme.
        private void ConfigureLinkColors()
        {
            bool useDarkMode = BackColor.GetBrightness() < 0.5f;
            Color linkColor = useDarkMode
                ? Color.FromArgb(140, 200, 255)
                : SystemColors.HotTrack;

            Color activeLinkColor = useDarkMode
                ? Color.FromArgb(185, 220, 255)
                : Color.Red;

            linkLabelUpdate.LinkColor = linkColor;
            linkLabelUpdate.ActiveLinkColor = activeLinkColor;
            linkLabelUpdate.VisitedLinkColor = linkColor;

            linkLabelGithub.LinkColor = linkColor;
            linkLabelGithub.ActiveLinkColor = activeLinkColor;
            linkLabelGithub.VisitedLinkColor = linkColor;

            linkLabelHelp.LinkColor = linkColor;
            linkLabelHelp.ActiveLinkColor = activeLinkColor;
            linkLabelHelp.VisitedLinkColor = linkColor;
        }

        // Builds the About UI; visible texts must use LocalizationService.
        private void InitializeComponent()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            Text = LocalizationService.Format(
                "About.Title",
                AppConstants.ApplicationName);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(475, 309);
            MinimumSize = Size;
            MaximumSize = Size;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            DoubleBuffered = true;

            pictureBoxMolotov = new PictureBox
            {
                Name = "pictureBoxMolotov",
                
                Image = CreateCircularMolotovImage(),
                Size = new Size(82, 82),
                Location = new Point(20, 24),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent
            };

            labelTitle = new Label
            {
                Name = "labelTitle",
                Text = AppConstants.FullApplicationName,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(122, 26),
                BackColor = Color.Transparent
            };

            labelCopyright = new Label
            {
                Name = "labelCopyright",
                Text = AppConstants.CopyrightText,
                AutoSize = true,
                Location = new Point(122, 58),
                BackColor = Color.Transparent
            };

            labelVersion = new Label
            {
                Name = "labelVersion",
                Text = LocalizationService.GetText("About.VersionPrefix") + GitHubUpdateService.GetApplicationVersionText(),
                AutoSize = true,
                Location = new Point(122, 82),
                BackColor = Color.Transparent
            };

            linkLabelUpdate = new LinkLabel
            {
                Name = "linkLabelUpdate",
                Text = LocalizationService.GetText("About.UpdateChecking"),
                AutoSize = true,
                Location = new Point(122, 106),
                BackColor = Color.Transparent,
                LinkBehavior = LinkBehavior.NeverUnderline
            };

            linkLabelUpdate.LinkClicked += linkLabelUpdate_LinkClicked;

            linkLabelGithub = new LinkLabel
            {
                Name = "linkLabelGithub",
                Text = AppConstants.GitHubRepositoryUrl,
                AutoSize = true,
                Location = new Point(122, 130),
                BackColor = Color.Transparent,
                LinkBehavior = LinkBehavior.HoverUnderline
            };

            linkLabelGithub.LinkClicked += linkLabelGithub_LinkClicked;

            linkLabelHelp = new LinkLabel
            {
                Name = "linkLabelHelp",
                Text = "Help: " + AppConstants.HelpUrl,
                AutoSize = true,
                Location = new Point(122, 154),
                BackColor = Color.Transparent,
                LinkBehavior = LinkBehavior.HoverUnderline
            };

            linkLabelHelp.LinkClicked += linkLabelHelp_LinkClicked;

            labelKoFiText = new Label
            {
                Name = "labelKoFiText",
                Text = LocalizationService.Format(
                           "About.FreeText",
                           AppConstants.ApplicationName) +
                       Environment.NewLine +
                       LocalizationService.GetText("About.SupportText"),
                AutoSize = false,
                Location = new Point(20, 194),
                Size = new Size(435, 38),
                BackColor = Color.Transparent
            };

            pictureBoxKoFi = new PictureBox
            {
                Name = "pictureBoxKoFi",
                
                Image = CreateKoFiImage(),
                Size = new Size(179, 42),
                Location = new Point(20, 244),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            pictureBoxKoFi.Click += pictureBoxKoFi_Click;

            buttonOk = new AntdUI.Button
            {
                Name = "buttonOk",
                Text = LocalizationService.GetText("Common.OK"),
                Size = new Size(90, 32),
                Location = new Point(365, 254),
                Type = AntdUI.TTypeMini.Default,
                DialogResult = DialogResult.OK
            };

            Controls.Add(pictureBoxMolotov);
            Controls.Add(labelTitle);
            Controls.Add(labelCopyright);
            Controls.Add(labelVersion);
            Controls.Add(linkLabelUpdate);
            Controls.Add(linkLabelGithub);
            Controls.Add(linkLabelHelp);
            Controls.Add(labelKoFiText);
            Controls.Add(pictureBoxKoFi);
            Controls.Add(buttonOk);

            AcceptButton = buttonOk;
        }

        // Creates the embedded circular application image at runtime.
        private Bitmap CreateCircularMolotovImage()
        {
            Bitmap output = new Bitmap(82, 82, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using Graphics graphics = Graphics.FromImage(output);
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            using Stream stream = typeof(AboutForm).Assembly.GetManifestResourceStream("c2flux.Ressources.molotov.jpg");

            if (stream == null)
            {
                using Pen fallbackPen = new Pen(Color.SteelBlue, 2);
                graphics.DrawEllipse(fallbackPen, 3, 3, 76, 76);
                return output;
            }

            using Image sourceImage = Image.FromStream(stream);
            using System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();

            path.AddEllipse(3, 3, 76, 76);
            graphics.SetClip(path);

            float scale = Math.Max(76f / sourceImage.Width, 76f / sourceImage.Height);
            int scaledWidth = (int)(sourceImage.Width * scale);
            int scaledHeight = (int)(sourceImage.Height * scale);
            int x = 3 + (76 - scaledWidth) / 2;
            int y = 3 + (76 - scaledHeight) / 2;

            graphics.DrawImage(sourceImage, x, y, scaledWidth, scaledHeight);
            graphics.ResetClip();

            using Pen borderPen = new Pen(Color.SteelBlue, 2);
            graphics.DrawEllipse(borderPen, 3, 3, 76, 76);

            return output;
        }

        // Loads the embedded Ko-fi image and removes its white background.
        private Image CreateKoFiImage()
        {
            using Stream stream = typeof(AboutForm).Assembly.GetManifestResourceStream("c2flux.Ressources.ko-fi.png");

            if (stream == null)
            {
                return new Bitmap(179, 42, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }

            using Image sourceImage = Image.FromStream(stream);
            Bitmap output = new Bitmap(sourceImage.Width, sourceImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (Graphics graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(sourceImage, 0, 0, sourceImage.Width, sourceImage.Height);
            }

            output.MakeTransparent(Color.White);

            return output;
        }

        // Updates the release status without blocking the UI thread.
        private async Task UpdateGitHubStatusAsync()
        {
            try
            {
                linkLabelUpdate.Text =
                    LocalizationService.GetText(
                        "About.UpdateChecking");
                linkLabelUpdate.Tag = string.Empty;
                linkLabelUpdate.Links.Clear();

                GitHubUpdateResult result =
                    await GitHubUpdateService.CheckForUpdateAsync();

                if (IsDisposed)
                {
                    return;
                }

                linkLabelUpdate.Tag = string.Empty;
                linkLabelUpdate.Links.Clear();

                if (result.ErrorKind != GitHubUpdateErrorKind.None)
                {
                    linkLabelUpdate.Text =
                        result.ErrorKind == GitHubUpdateErrorKind.Timeout ||
                        result.ErrorKind == GitHubUpdateErrorKind.Network ||
                        result.ErrorKind == GitHubUpdateErrorKind.Http
                        ? LocalizationService.GetText(
                            "About.GitHubUnavailable")
                        : LocalizationService.GetText(
                            "Common.Error");

                    linkLabelUpdate.LinkBehavior =
                        LinkBehavior.NeverUnderline;
                    return;
                }

                if (!result.UpdateAvailable)
                {
                    linkLabelUpdate.Text =
                        LocalizationService.GetText(
                            "About.NoNewVersion");
                    linkLabelUpdate.LinkBehavior =
                        LinkBehavior.NeverUnderline;
                    return;
                }

                linkLabelUpdate.Text =
                    LocalizationService.Format(
                        "About.UpdateAvailable",
                        result.LatestVersion);
                linkLabelUpdate.Tag = result.DownloadUrl;
                linkLabelUpdate.LinkBehavior =
                    LinkBehavior.HoverUnderline;
                linkLabelUpdate.Links.Add(
                    0,
                    linkLabelUpdate.Text.Length);
            }
            catch (Exception exception)
            {
                AppAlertLog.AddWarning(
                    "GitHub update",
                    "The About dialog could not update the GitHub status.",
                    exception.ToString());

                if (!IsDisposed)
                {
                    linkLabelUpdate.Tag = string.Empty;
                    linkLabelUpdate.Links.Clear();
                    linkLabelUpdate.Text =
                        LocalizationService.GetText(
                            "Common.Error");
                    linkLabelUpdate.LinkBehavior =
                        LinkBehavior.NeverUnderline;
                }
            }
        }




        private void linkLabelUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string downloadUrl = linkLabelUpdate.Tag == null ? string.Empty : linkLabelUpdate.Tag.ToString();

            if (string.IsNullOrWhiteSpace(downloadUrl))
                return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = downloadUrl,
                UseShellExecute = true
            });
        }

        private void linkLabelGithub_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppConstants.GitHubRepositoryUrl,
                UseShellExecute = true
            });
        }

        private void linkLabelHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppConstants.HelpUrl,
                UseShellExecute = true
            });
        }

        private void pictureBoxKoFi_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppConstants.KoFiUrl,
                UseShellExecute = true
            });
        }

    }
}