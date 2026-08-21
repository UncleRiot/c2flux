// Last comment Update 2026-08-21 09:33
using System.Drawing;
using System.IO;
using System.Reflection;

namespace c2flux
{
    public static class AppResources
    {
        // Central access point for embedded application images and icons.
        private static Icon _applicationIcon;
        private static Bitmap _applicationImage;
        private static Bitmap _storageHistoryDetailsPreviewImage;

        // Lazily loads and reuses the embedded application icon.
        public static Icon ApplicationIcon
        {
            get
            {
                if (_applicationIcon == null)
                {
                    _applicationIcon = LoadIcon("c2flux.Ressources.c2flux.ico");
                }

                return _applicationIcon;
            }
        }

        // Lazily loads and reuses the embedded application image.
        public static Bitmap ApplicationImage
        {
            get
            {
                if (_applicationImage == null)
                {
                    _applicationImage = LoadBitmap("c2flux.Ressources.c2flux.png");
                }

                return _applicationImage;
            }
        }

        // Lazily loads and reuses the Storage History preview image.
        public static Bitmap StorageHistoryDetailsPreviewImage
        {
            get
            {
                if (_storageHistoryDetailsPreviewImage == null)
                {
                    _storageHistoryDetailsPreviewImage = LoadBitmap(
                        "c2flux.Ressources.scan-history-details.png");
                }

                return _storageHistoryDetailsPreviewImage;
            }
        }

        // Loads an embedded icon and falls back to the system application icon.
        private static Icon LoadIcon(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                return SystemIcons.Application;
            }

            using (stream)
            {
                return new Icon(stream);
            }
        }

        // Loads an embedded bitmap and falls back to the system application image.
        private static Bitmap LoadBitmap(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                return SystemIcons.Application.ToBitmap();
            }

            using (stream)
            using (Image image = Image.FromStream(stream))
            {
                return new Bitmap(image);
            }
        }
    }
}