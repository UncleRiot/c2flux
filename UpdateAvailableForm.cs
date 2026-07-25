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

            buttonDownload.Click += buttonDownload_Click;

            Controls.Add(pictureBoxInformation);
            Controls.Add(labelMessage);
            Controls.Add(buttonDownload);
            Controls.Add(buttonLater);

            AcceptButton = buttonDownload;
            CancelButton = buttonLater;
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
