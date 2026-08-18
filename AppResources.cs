using System.Drawing;
using System.IO;
using System.Reflection;

namespace c2flux
{
    public static class AppResources
    {
        private static Icon _applicationIcon;
        private static Bitmap _applicationImage;
        private static Bitmap _storageHistoryDetailsPreviewImage;

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