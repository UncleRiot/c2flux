using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class UpdateAvailableForm : Form
    {
        private readonly string _downloadUrl;

        private PictureBox pictureBoxInformation;
        private Label labelMessage;
        private Label labelReleaseNotes;
        private AntdUI.Button buttonChangelog;
        private AntdUI.Button buttonDownload;
        private AntdUI.Button buttonLater;

        public UpdateAvailableForm(
            AppLayout layout,
            GitHubUpdateResult updateResult)
        {
            _downloadUrl = updateResult?.DownloadUrl;

            InitializeComponent(updateResult);
            AntdThemeService.ConfigureUpdateAvailableForm(
                this,
                pictureBoxInformation,
                labelMessage,
                labelReleaseNotes,
                buttonChangelog,
                buttonDownload,
                buttonLater,
                layout);
        }

        private void InitializeComponent(
            GitHubUpdateResult updateResult)
        {
            Text = AppConstants.ApplicationName;

            pictureBoxInformation = new PictureBox
            {
                Name = "pictureBoxInformation",
                Image = SystemIcons.Exclamation.ToBitmap()
            };

            labelMessage = new Label
            {
                Name = "labelMessage",
                Text = LocalizationService.Format(
                    "About.UpdateAvailableMessage",
                    AppConstants.ApplicationName,
                    updateResult?.LatestVersion ?? string.Empty)
            };

            labelReleaseNotes = new Label
            {
                Name = "labelReleaseNotes",
                Text = updateResult?.ReleaseNotes ?? string.Empty
            };

            buttonChangelog = new AntdUI.Button
            {
                Name = "buttonChangelog",
                Text = LocalizationService.GetText(
                    "About.UpdateChangelog")
            };

            buttonDownload = new AntdUI.Button
            {
                Name = "buttonDownload",
                Text = LocalizationService.GetText(
                    "About.UpdateDownload")
            };

            buttonLater = new AntdUI.Button
            {
                Name = "buttonLater",
                Text = LocalizationService.GetText(
                    "About.UpdateLater"),
                DialogResult = DialogResult.Cancel
            };

            buttonChangelog.Click += buttonChangelog_Click;
            buttonDownload.Click += buttonDownload_Click;

            Controls.Add(pictureBoxInformation);
            Controls.Add(labelMessage);
            Controls.Add(labelReleaseNotes);
            Controls.Add(buttonChangelog);
            Controls.Add(buttonDownload);
            Controls.Add(buttonLater);

            AcceptButton = buttonDownload;
            CancelButton = buttonLater;
        }

        private void buttonChangelog_Click(
            object sender,
            EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_downloadUrl))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = _downloadUrl,
                UseShellExecute = true
            });
        }

        private void buttonDownload_Click(
            object sender,
            EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_downloadUrl))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = _downloadUrl,
                UseShellExecute = true
            });

            Close();
        }
    }
}
