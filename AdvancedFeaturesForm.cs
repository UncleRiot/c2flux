using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class AdvancedFeaturesForm : Form
    {
        private sealed class FileTypeRow
        {
            public string Extension { get; set; }
            public double UsagePercent { get; set; }
            public string SizeGb { get; set; }
            public string SizeMb { get; set; }
            public long SizeBytes { get; set; }
        }

        private sealed class FileTypeCategoryRow
        {
            public string FileType { get; set; }
            public double UsagePercent { get; set; }
            public string SizeGb { get; set; }
            public string SizeMb { get; set; }
            public long SizeBytes { get; set; }
        }

        private sealed class LargestFileRow
        {
            public string Name { get; set; }
            public double UsagePercent { get; set; }
            public string FormattedSize { get; set; }
            public long SizeBytes { get; set; }
            public DateTime LastWriteTime { get; set; }
            public string FullPath { get; set; }
        }

        private sealed class RedundancyRow
        {
            public string Name { get; set; }
            public double? UsagePercent { get; set; }
            public int? Count { get; set; }
            public long? SizeBytes { get; set; }
            public long? TotalSizeBytes { get; set; }
            public bool IsLocation { get; set; }
            public List<RedundancyRow> Children { get; set; }
        }

        private sealed class RedundancyAnalysisProgress :
            IProgress<RedundancyAnalysisGroup>
        {
            private readonly ConcurrentQueue<RedundancyAnalysisGroup>
                _pendingGroups;

            public RedundancyAnalysisProgress(
                ConcurrentQueue<RedundancyAnalysisGroup> pendingGroups)
            {
                _pendingGroups = pendingGroups ??
                    throw new ArgumentNullException(nameof(pendingGroups));
            }

            public void Report(
                RedundancyAnalysisGroup value)
            {
                if (value != null)
                {
                    _pendingGroups.Enqueue(value);
                }
            }
        }

        private enum SizeUnit
        {
            Bytes,
            KB,
            MB,
            GB,
            TB
        }

        private static readonly Dictionary<string, string>
            FileTypeCategoryByExtension =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
            [".jpg"] = "Advanced.FileType.Images",
            [".jpeg"] = "Advanced.FileType.Images",
            [".jpe"] = "Advanced.FileType.Images",
            [".png"] = "Advanced.FileType.Images",
            [".gif"] = "Advanced.FileType.Images",
            [".bmp"] = "Advanced.FileType.Images",
            [".tif"] = "Advanced.FileType.Images",
            [".tiff"] = "Advanced.FileType.Images",
            [".webp"] = "Advanced.FileType.Images",
            [".heic"] = "Advanced.FileType.Images",
            [".heif"] = "Advanced.FileType.Images",
            [".avif"] = "Advanced.FileType.Images",
            [".ico"] = "Advanced.FileType.Images",
            [".svg"] = "Advanced.FileType.Images",
            [".eps"] = "Advanced.FileType.Images",
            [".psd"] = "Advanced.FileType.Images",
            [".raw"] = "Advanced.FileType.Images",
            [".dng"] = "Advanced.FileType.Images",
            [".cr2"] = "Advanced.FileType.Images",
            [".cr3"] = "Advanced.FileType.Images",
            [".nef"] = "Advanced.FileType.Images",
            [".arw"] = "Advanced.FileType.Images",
            [".orf"] = "Advanced.FileType.Images",
            [".rw2"] = "Advanced.FileType.Images",
            [".jxr"] = "Advanced.FileType.Images",
            [".wdp"] = "Advanced.FileType.Images",
            [".dds"] = "Advanced.FileType.Images",
            [".jfif"] = "Advanced.FileType.Images",
            [".jp2"] = "Advanced.FileType.Images",
            [".j2k"] = "Advanced.FileType.Images",
            [".jpf"] = "Advanced.FileType.Images",
            [".jpx"] = "Advanced.FileType.Images",
            [".psb"] = "Advanced.FileType.Images",
            [".ai"] = "Advanced.FileType.Images",
            [".emf"] = "Advanced.FileType.Images",
            [".wmf"] = "Advanced.FileType.Images",
            [".pcx"] = "Advanced.FileType.Images",
            [".tga"] = "Advanced.FileType.Images",
            [".hdr"] = "Advanced.FileType.Images",
            [".exr"] = "Advanced.FileType.Images",
            [".xcf"] = "Advanced.FileType.Images",
            [".kra"] = "Advanced.FileType.Images",
            [".icns"] = "Advanced.FileType.Images",
            [".pbm"] = "Advanced.FileType.Images",
            [".pgm"] = "Advanced.FileType.Images",
            [".ppm"] = "Advanced.FileType.Images",
            [".pnm"] = "Advanced.FileType.Images",
            [".mp4"] = "Advanced.FileType.Video",
            [".m4v"] = "Advanced.FileType.Video",
            [".mkv"] = "Advanced.FileType.Video",
            [".avi"] = "Advanced.FileType.Video",
            [".mov"] = "Advanced.FileType.Video",
            [".wmv"] = "Advanced.FileType.Video",
            [".flv"] = "Advanced.FileType.Video",
            [".webm"] = "Advanced.FileType.Video",
            [".mpeg"] = "Advanced.FileType.Video",
            [".mpg"] = "Advanced.FileType.Video",
            [".mpe"] = "Advanced.FileType.Video",
            [".m2v"] = "Advanced.FileType.Video",
            [".mts"] = "Advanced.FileType.Video",
            [".m2ts"] = "Advanced.FileType.Video",
            [".vob"] = "Advanced.FileType.Video",
            [".3gp"] = "Advanced.FileType.Video",
            [".3g2"] = "Advanced.FileType.Video",
            [".ogv"] = "Advanced.FileType.Video",
            [".asf"] = "Advanced.FileType.Video",
            [".bk2"] = "Advanced.FileType.Video",
            [".bik"] = "Advanced.FileType.Video",
            [".f4v"] = "Advanced.FileType.Video",
            [".mp4v"] = "Advanced.FileType.Video",
            [".qt"] = "Advanced.FileType.Video",
            [".divx"] = "Advanced.FileType.Video",
            [".mxf"] = "Advanced.FileType.Video",
            [".rm"] = "Advanced.FileType.Video",
            [".rmvb"] = "Advanced.FileType.Video",
            [".dv"] = "Advanced.FileType.Video",
            [".mp3"] = "Advanced.FileType.Audio",
            [".wav"] = "Advanced.FileType.Audio",
            [".flac"] = "Advanced.FileType.Audio",
            [".aac"] = "Advanced.FileType.Audio",
            [".m4a"] = "Advanced.FileType.Audio",
            [".ogg"] = "Advanced.FileType.Audio",
            [".oga"] = "Advanced.FileType.Audio",
            [".opus"] = "Advanced.FileType.Audio",
            [".wma"] = "Advanced.FileType.Audio",
            [".aif"] = "Advanced.FileType.Audio",
            [".aiff"] = "Advanced.FileType.Audio",
            [".aifc"] = "Advanced.FileType.Audio",
            [".mid"] = "Advanced.FileType.Audio",
            [".midi"] = "Advanced.FileType.Audio",
            [".cda"] = "Advanced.FileType.Audio",
            [".ape"] = "Advanced.FileType.Audio",
            [".ac3"] = "Advanced.FileType.Audio",
            [".mka"] = "Advanced.FileType.Audio",
            [".wem"] = "Advanced.FileType.Audio",
            [".bnk"] = "Advanced.FileType.Audio",
            [".xwm"] = "Advanced.FileType.Audio",
            [".fuz"] = "Advanced.FileType.Audio",
            [".m4b"] = "Advanced.FileType.Audio",
            [".amr"] = "Advanced.FileType.Audio",
            [".au"] = "Advanced.FileType.Audio",
            [".snd"] = "Advanced.FileType.Audio",
            [".wv"] = "Advanced.FileType.Audio",
            [".tta"] = "Advanced.FileType.Audio",
            [".dsf"] = "Advanced.FileType.Audio",
            [".dff"] = "Advanced.FileType.Audio",
            [".caf"] = "Advanced.FileType.Audio",
            [".m3u"] = "Advanced.FileType.Audio",
            [".m3u8"] = "Advanced.FileType.Audio",
            [".pls"] = "Advanced.FileType.Audio",
            [".txt"] = "Advanced.FileType.Documents",
            [".rtf"] = "Advanced.FileType.Documents",
            [".pdf"] = "Advanced.FileType.Documents",
            [".xps"] = "Advanced.FileType.Documents",
            [".oxps"] = "Advanced.FileType.Documents",
            [".doc"] = "Advanced.FileType.Documents",
            [".docx"] = "Advanced.FileType.Documents",
            [".docm"] = "Advanced.FileType.Documents",
            [".dot"] = "Advanced.FileType.Documents",
            [".dotx"] = "Advanced.FileType.Documents",
            [".dotm"] = "Advanced.FileType.Documents",
            [".xls"] = "Advanced.FileType.Documents",
            [".xlsx"] = "Advanced.FileType.Documents",
            [".xlsm"] = "Advanced.FileType.Documents",
            [".xlsb"] = "Advanced.FileType.Documents",
            [".xlt"] = "Advanced.FileType.Documents",
            [".xltx"] = "Advanced.FileType.Documents",
            [".xltm"] = "Advanced.FileType.Documents",
            [".csv"] = "Advanced.FileType.Documents",
            [".ods"] = "Advanced.FileType.Documents",
            [".ppt"] = "Advanced.FileType.Documents",
            [".pptx"] = "Advanced.FileType.Documents",
            [".pptm"] = "Advanced.FileType.Documents",
            [".pps"] = "Advanced.FileType.Documents",
            [".ppsx"] = "Advanced.FileType.Documents",
            [".ppsm"] = "Advanced.FileType.Documents",
            [".pot"] = "Advanced.FileType.Documents",
            [".potx"] = "Advanced.FileType.Documents",
            [".potm"] = "Advanced.FileType.Documents",
            [".odp"] = "Advanced.FileType.Documents",
            [".odt"] = "Advanced.FileType.Documents",
            [".pages"] = "Advanced.FileType.Documents",
            [".numbers"] = "Advanced.FileType.Documents",
            [".key"] = "Advanced.FileType.Documents",
            [".pub"] = "Advanced.FileType.Documents",
            [".wpd"] = "Advanced.FileType.Documents",
            [".epub"] = "Advanced.FileType.Documents",
            [".mobi"] = "Advanced.FileType.Documents",
            [".vsd"] = "Advanced.FileType.Documents",
            [".vsdx"] = "Advanced.FileType.Documents",
            [".vsdm"] = "Advanced.FileType.Documents",
            [".eml"] = "Advanced.FileType.Documents",
            [".msg"] = "Advanced.FileType.Documents",
            [".one"] = "Advanced.FileType.Documents",
            [".onetoc2"] = "Advanced.FileType.Documents",
            [".ps"] = "Advanced.FileType.Documents",
            [".djvu"] = "Advanced.FileType.Documents",
            [".djv"] = "Advanced.FileType.Documents",
            [".indd"] = "Advanced.FileType.Documents",
            [".indt"] = "Advanced.FileType.Documents",
            [".idml"] = "Advanced.FileType.Documents",
            [".indb"] = "Advanced.FileType.Documents",
            [".indl"] = "Advanced.FileType.Documents",
            [".icml"] = "Advanced.FileType.Documents",
            [".odg"] = "Advanced.FileType.Documents",
            [".ott"] = "Advanced.FileType.Documents",
            [".ots"] = "Advanced.FileType.Documents",
            [".otp"] = "Advanced.FileType.Documents",
            [".zip"] = "Advanced.FileType.Archives",
            [".7z"] = "Advanced.FileType.Archives",
            [".rar"] = "Advanced.FileType.Archives",
            [".tar"] = "Advanced.FileType.Archives",
            [".gz"] = "Advanced.FileType.Archives",
            [".gzip"] = "Advanced.FileType.Archives",
            [".bz2"] = "Advanced.FileType.Archives",
            [".xz"] = "Advanced.FileType.Archives",
            [".zst"] = "Advanced.FileType.Archives",
            [".tgz"] = "Advanced.FileType.Archives",
            [".tbz"] = "Advanced.FileType.Archives",
            [".tbz2"] = "Advanced.FileType.Archives",
            [".txz"] = "Advanced.FileType.Archives",
            [".cab"] = "Advanced.FileType.Archives",
            [".arj"] = "Advanced.FileType.Archives",
            [".lha"] = "Advanced.FileType.Archives",
            [".lzh"] = "Advanced.FileType.Archives",
            [".ace"] = "Advanced.FileType.Archives",
            [".zipx"] = "Advanced.FileType.Archives",
            [".lz"] = "Advanced.FileType.Archives",
            [".lzma"] = "Advanced.FileType.Archives",
            [".lz4"] = "Advanced.FileType.Archives",
            [".lzo"] = "Advanced.FileType.Archives",
            [".br"] = "Advanced.FileType.Archives",
            [".z"] = "Advanced.FileType.Archives",
            [".cpio"] = "Advanced.FileType.Archives",
            [".zoo"] = "Advanced.FileType.Archives",
            [".sit"] = "Advanced.FileType.Archives",
            [".sitx"] = "Advanced.FileType.Archives",
            [".pak"] = "Advanced.FileType.GameFiles",
            [".ucas"] = "Advanced.FileType.GameFiles",
            [".utoc"] = "Advanced.FileType.GameFiles",
            [".uasset"] = "Advanced.FileType.GameFiles",
            [".uexp"] = "Advanced.FileType.GameFiles",
            [".ubulk"] = "Advanced.FileType.GameFiles",
            [".upk"] = "Advanced.FileType.GameFiles",
            [".assets"] = "Advanced.FileType.GameFiles",
            [".ress"] = "Advanced.FileType.GameFiles",
            [".resource"] = "Advanced.FileType.GameFiles",
            [".forge"] = "Advanced.FileType.GameFiles",
            [".bsa"] = "Advanced.FileType.GameFiles",
            [".ba2"] = "Advanced.FileType.GameFiles",
            [".rpf"] = "Advanced.FileType.GameFiles",
            [".vpk"] = "Advanced.FileType.GameFiles",
            [".pck"] = "Advanced.FileType.GameFiles",
            [".cok"] = "Advanced.FileType.GameFiles",
            [".cas"] = "Advanced.FileType.GameFiles",
            [".bundle"] = "Advanced.FileType.GameFiles",
            [".nxa"] = "Advanced.FileType.GameFiles",
            [".mwm"] = "Advanced.FileType.GameFiles",
            [".tbf"] = "Advanced.FileType.GameFiles",
            [".rda"] = "Advanced.FileType.GameFiles",
            [".kfc"] = "Advanced.FileType.GameFiles",
            [".kfc_resources"] = "Advanced.FileType.GameFiles",
            [".acf"] = "Advanced.FileType.GameFiles",
            [".vdf"] = "Advanced.FileType.GameFiles",
            [".sav"] = "Advanced.FileType.GameFiles",
            [".save"] = "Advanced.FileType.GameFiles",
            [".umap"] = "Advanced.FileType.GameFiles",
            [".locres"] = "Advanced.FileType.GameFiles",
            [".ushaderbytecode"] = "Advanced.FileType.GameFiles",
            [".nif"] = "Advanced.FileType.GameFiles",
            [".esp"] = "Advanced.FileType.GameFiles",
            [".esm"] = "Advanced.FileType.GameFiles",
            [".esl"] = "Advanced.FileType.GameFiles",
            [".pex"] = "Advanced.FileType.GameFiles",
            [".psc"] = "Advanced.FileType.GameFiles",
            [".awc"] = "Advanced.FileType.GameFiles",
            [".ytd"] = "Advanced.FileType.GameFiles",
            [".ydr"] = "Advanced.FileType.GameFiles",
            [".yft"] = "Advanced.FileType.GameFiles",
            [".ymap"] = "Advanced.FileType.GameFiles",
            [".ytyp"] = "Advanced.FileType.GameFiles",
            [".ysc"] = "Advanced.FileType.GameFiles",
            [".bsp"] = "Advanced.FileType.GameFiles",
            [".vtx"] = "Advanced.FileType.GameFiles",
            [".vvd"] = "Advanced.FileType.GameFiles",
            [".mdl"] = "Advanced.FileType.GameFiles",
            [".phy"] = "Advanced.FileType.GameFiles",
            [".nav"] = "Advanced.FileType.GameFiles",
            [".pk3"] = "Advanced.FileType.GameFiles",
            [".pk4"] = "Advanced.FileType.GameFiles",
            [".wad"] = "Advanced.FileType.GameFiles",
            [".iwd"] = "Advanced.FileType.GameFiles",
            [".xpak"] = "Advanced.FileType.GameFiles",
            [".ff"] = "Advanced.FileType.GameFiles",
            [".exe"] = "Advanced.FileType.Applications",
            [".com"] = "Advanced.FileType.Applications",
            [".msi"] = "Advanced.FileType.Applications",
            [".msix"] = "Advanced.FileType.Applications",
            [".msixbundle"] = "Advanced.FileType.Applications",
            [".appx"] = "Advanced.FileType.Applications",
            [".appxbundle"] = "Advanced.FileType.Applications",
            [".dll"] = "Advanced.FileType.Applications",
            [".ocx"] = "Advanced.FileType.Applications",
            [".cpl"] = "Advanced.FileType.Applications",
            [".scr"] = "Advanced.FileType.Applications",
            [".jar"] = "Advanced.FileType.Applications",
            [".war"] = "Advanced.FileType.Applications",
            [".apk"] = "Advanced.FileType.Applications",
            [".aab"] = "Advanced.FileType.Applications",
            [".appinstaller"] = "Advanced.FileType.Applications",
            [".msu"] = "Advanced.FileType.Applications",
            [".msp"] = "Advanced.FileType.Applications",
            [".mst"] = "Advanced.FileType.Applications",
            [".deb"] = "Advanced.FileType.Applications",
            [".rpm"] = "Advanced.FileType.Applications",
            [".sys"] = "Advanced.FileType.SystemFiles",
            [".drv"] = "Advanced.FileType.SystemFiles",
            [".mui"] = "Advanced.FileType.SystemFiles",
            [".efi"] = "Advanced.FileType.SystemFiles",
            [".cat"] = "Advanced.FileType.SystemFiles",
            [".manifest"] = "Advanced.FileType.SystemFiles",
            [".nls"] = "Advanced.FileType.SystemFiles",
            [".inf"] = "Advanced.FileType.SystemFiles",
            [".admx"] = "Advanced.FileType.SystemFiles",
            [".adml"] = "Advanced.FileType.SystemFiles",
            [".pol"] = "Advanced.FileType.SystemFiles",
            [".cs"] = "Advanced.FileType.Development",
            [".csproj"] = "Advanced.FileType.Development",
            [".sln"] = "Advanced.FileType.Development",
            [".slnx"] = "Advanced.FileType.Development",
            [".c"] = "Advanced.FileType.Development",
            [".cc"] = "Advanced.FileType.Development",
            [".cpp"] = "Advanced.FileType.Development",
            [".cxx"] = "Advanced.FileType.Development",
            [".h"] = "Advanced.FileType.Development",
            [".hh"] = "Advanced.FileType.Development",
            [".hpp"] = "Advanced.FileType.Development",
            [".java"] = "Advanced.FileType.Development",
            [".class"] = "Advanced.FileType.Development",
            [".kt"] = "Advanced.FileType.Development",
            [".kts"] = "Advanced.FileType.Development",
            [".py"] = "Advanced.FileType.Development",
            [".pyw"] = "Advanced.FileType.Development",
            [".pyc"] = "Advanced.FileType.Development",
            [".js"] = "Advanced.FileType.Development",
            [".jsx"] = "Advanced.FileType.Development",
            [".tsx"] = "Advanced.FileType.Development",
            [".html"] = "Advanced.FileType.Development",
            [".htm"] = "Advanced.FileType.Development",
            [".css"] = "Advanced.FileType.Development",
            [".scss"] = "Advanced.FileType.Development",
            [".sass"] = "Advanced.FileType.Development",
            [".less"] = "Advanced.FileType.Development",
            [".php"] = "Advanced.FileType.Development",
            [".rb"] = "Advanced.FileType.Development",
            [".go"] = "Advanced.FileType.Development",
            [".rs"] = "Advanced.FileType.Development",
            [".swift"] = "Advanced.FileType.Development",
            [".vb"] = "Advanced.FileType.Development",
            [".fs"] = "Advanced.FileType.Development",
            [".fsx"] = "Advanced.FileType.Development",
            [".ps1"] = "Advanced.FileType.Development",
            [".psm1"] = "Advanced.FileType.Development",
            [".psd1"] = "Advanced.FileType.Development",
            [".bat"] = "Advanced.FileType.Development",
            [".cmd"] = "Advanced.FileType.Development",
            [".sh"] = "Advanced.FileType.Development",
            [".xml"] = "Advanced.FileType.Development",
            [".json"] = "Advanced.FileType.Development",
            [".yaml"] = "Advanced.FileType.Development",
            [".yml"] = "Advanced.FileType.Development",
            [".toml"] = "Advanced.FileType.Development",
            [".sql"] = "Advanced.FileType.Development",
            [".props"] = "Advanced.FileType.Development",
            [".targets"] = "Advanced.FileType.Development",
            [".vcxproj"] = "Advanced.FileType.Development",
            [".fsproj"] = "Advanced.FileType.Development",
            [".vbproj"] = "Advanced.FileType.Development",
            [".gradle"] = "Advanced.FileType.Development",
            [".vue"] = "Advanced.FileType.Development",
            [".svelte"] = "Advanced.FileType.Development",
            [".md"] = "Advanced.FileType.Development",
            [".lua"] = "Advanced.FileType.Development",
            [".pl"] = "Advanced.FileType.Development",
            [".dart"] = "Advanced.FileType.Development",
            [".scala"] = "Advanced.FileType.Development",
            [".groovy"] = "Advanced.FileType.Development",
            [".ipynb"] = "Advanced.FileType.Development",
            [".asm"] = "Advanced.FileType.Development",
            [".inc"] = "Advanced.FileType.Development",
            [".proto"] = "Advanced.FileType.Development",
            [".graphql"] = "Advanced.FileType.Development",
            [".gql"] = "Advanced.FileType.Development",
            [".xaml"] = "Advanced.FileType.Development",
            [".resx"] = "Advanced.FileType.Development",
            [".razor"] = "Advanced.FileType.Development",
            [".cshtml"] = "Advanced.FileType.Development",
            [".vbhtml"] = "Advanced.FileType.Development",
            [".cmake"] = "Advanced.FileType.Development",
            [".wasm"] = "Advanced.FileType.Development",
            [".db"] = "Advanced.FileType.Databases",
            [".db3"] = "Advanced.FileType.Databases",
            [".sqlite"] = "Advanced.FileType.Databases",
            [".sqlite3"] = "Advanced.FileType.Databases",
            [".sqlitedb"] = "Advanced.FileType.Databases",
            [".mdb"] = "Advanced.FileType.Databases",
            [".accdb"] = "Advanced.FileType.Databases",
            [".accde"] = "Advanced.FileType.Databases",
            [".accdr"] = "Advanced.FileType.Databases",
            [".ndf"] = "Advanced.FileType.Databases",
            [".ldf"] = "Advanced.FileType.Databases",
            [".fdb"] = "Advanced.FileType.Databases",
            [".gdb"] = "Advanced.FileType.Databases",
            [".dbf"] = "Advanced.FileType.Databases",
            [".sdf"] = "Advanced.FileType.Databases",
            [".realm"] = "Advanced.FileType.Databases",
            [".odb"] = "Advanced.FileType.Databases",
            [".db-wal"] = "Advanced.FileType.Databases",
            [".db-shm"] = "Advanced.FileType.Databases",
            [".db-journal"] = "Advanced.FileType.Databases",
            [".sqlite-wal"] = "Advanced.FileType.Databases",
            [".sqlite-shm"] = "Advanced.FileType.Databases",
            [".sqlite-journal"] = "Advanced.FileType.Databases",
            [".sqlite3-wal"] = "Advanced.FileType.Databases",
            [".sqlite3-shm"] = "Advanced.FileType.Databases",
            [".sqlite3-journal"] = "Advanced.FileType.Databases",
            [".iso"] = "Advanced.FileType.DiskImages",
            [".img"] = "Advanced.FileType.DiskImages",
            [".ima"] = "Advanced.FileType.DiskImages",
            [".cue"] = "Advanced.FileType.DiskImages",
            [".nrg"] = "Advanced.FileType.DiskImages",
            [".dmg"] = "Advanced.FileType.DiskImages",
            [".vhd"] = "Advanced.FileType.DiskImages",
            [".vhdx"] = "Advanced.FileType.DiskImages",
            [".vmdk"] = "Advanced.FileType.DiskImages",
            [".vdi"] = "Advanced.FileType.DiskImages",
            [".qcow"] = "Advanced.FileType.DiskImages",
            [".qcow2"] = "Advanced.FileType.DiskImages",
            [".wim"] = "Advanced.FileType.DiskImages",
            [".esd"] = "Advanced.FileType.DiskImages",
            [".swm"] = "Advanced.FileType.DiskImages",
            [".ova"] = "Advanced.FileType.DiskImages",
            [".ovf"] = "Advanced.FileType.DiskImages",
            [".vhdset"] = "Advanced.FileType.DiskImages",
            [".bak"] = "Advanced.FileType.Backups",
            [".backup"] = "Advanced.FileType.Backups",
            [".bkf"] = "Advanced.FileType.Backups",
            [".bkp"] = "Advanced.FileType.Backups",
            [".old"] = "Advanced.FileType.Backups",
            [".wbk"] = "Advanced.FileType.Backups",
            [".tib"] = "Advanced.FileType.Backups",
            [".tibx"] = "Advanced.FileType.Backups",
            [".mrimg"] = "Advanced.FileType.Backups",
            [".vbk"] = "Advanced.FileType.Backups",
            [".vib"] = "Advanced.FileType.Backups",
            [".vrb"] = "Advanced.FileType.Backups",
            [".abk"] = "Advanced.FileType.Backups",
            [".vbm"] = "Advanced.FileType.Backups",
            [".vma"] = "Advanced.FileType.Backups",
            [".pxar"] = "Advanced.FileType.Backups",
            [".log"] = "Advanced.FileType.LogFiles",
            [".log1"] = "Advanced.FileType.LogFiles",
            [".log2"] = "Advanced.FileType.LogFiles",
            [".evtx"] = "Advanced.FileType.LogFiles",
            [".evt"] = "Advanced.FileType.LogFiles",
            [".etl"] = "Advanced.FileType.LogFiles",
            [".trace"] = "Advanced.FileType.LogFiles",
            [".trc"] = "Advanced.FileType.LogFiles",
            [".out"] = "Advanced.FileType.LogFiles",
            [".err"] = "Advanced.FileType.LogFiles",
            [".debug"] = "Advanced.FileType.LogFiles",
            [".dmp"] = "Advanced.FileType.LogFiles",
            [".mdmp"] = "Advanced.FileType.LogFiles",
            [".journal"] = "Advanced.FileType.LogFiles",
            [".tmp"] = "Advanced.FileType.TemporaryFiles",
            [".temp"] = "Advanced.FileType.TemporaryFiles",
            [".crdownload"] = "Advanced.FileType.TemporaryFiles",
            [".part"] = "Advanced.FileType.TemporaryFiles",
            [".partial"] = "Advanced.FileType.TemporaryFiles",
            [".download"] = "Advanced.FileType.TemporaryFiles",
            [".cache"] = "Advanced.FileType.TemporaryFiles",
            [".swp"] = "Advanced.FileType.TemporaryFiles",
            [".swo"] = "Advanced.FileType.TemporaryFiles",
                };

        private class Analysis_ResponsiveTableGrid : AntdUI.Table
        {
            public Analysis_ResponsiveTableGrid()
            {
                Dock = DockStyle.Fill;
                FixedHeader = true;
                VisibleHeader = true;
                EnableHeaderResizing = true;
                ColumnDragSort = false;
                MultipleRows = false;
                LostFocusClearSelection = false;
                MouseClickPenetration = true;
                ScrollBarAvoidHeader = true;
                AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
                ShowTip = true;
                EmptyHeader = true;
                EmptyText = string.Empty;
    
                ApplyAntdUIStyle();
            }
    
            public void SetResponsiveColumns(
                params (string ColumnName, int Percentage)[] responsiveColumns)
            {
                if (responsiveColumns == null || Columns == null)
                    return;
    
                foreach ((string ColumnName, int Percentage) definition in responsiveColumns)
                {
                    AntdUI.Column column = Columns.FirstOrDefault(
                        currentColumn => string.Equals(
                            currentColumn.Key,
                            definition.ColumnName,
                            StringComparison.Ordinal));
    
                    if (column == null)
                        continue;
    
                    column.Width = $"{Math.Max(0, definition.Percentage)}%";
                }
    
                LoadLayout();
                Invalidate();
            }
    
            public void ApplyAntdUIStyle()
            {
                AntdThemeService.ConfigureAnalysisTable(this);
                LoadLayout();
                Invalidate();
            }
        }

        private readonly FileSystemEntry _rootEntry;
        private readonly Analysis_ResponsiveTableGrid _fileTypeGrid =
            new Analysis_ResponsiveTableGrid();
        private readonly Analysis_ResponsiveTableGrid _fileTypeCategoryGrid =
            new Analysis_ResponsiveTableGrid();
        private readonly Analysis_ResponsiveTableGrid _largestFilesGrid =
            new Analysis_ResponsiveTableGrid();
        private readonly Analysis_ResponsiveTableGrid _redundancyGrid =
            new Analysis_ResponsiveTableGrid();
        private readonly AntdUI.Progress _redundancyAnalysisProgressBar =
            new AntdUI.Progress();
        private readonly CancellationTokenSource _redundancyCancellationTokenSource =
            new CancellationTokenSource();
        private readonly ContextMenuStrip _largestFilesContextMenu =
            new ContextMenuStrip();
        private readonly ContextMenuStrip _redundancyContextMenu =
            new ContextMenuStrip();
        private LargestFileRow _largestFilesContextRow;
        private RedundancyRow _redundancyContextRow;
        private List<FileTypeRow> _fileTypeRows =
            new List<FileTypeRow>();
        private List<FileTypeCategoryRow> _fileTypeCategoryRows =
            new List<FileTypeCategoryRow>();
        private List<LargestFileRow> _largestFileRows =
            new List<LargestFileRow>();
        private AntdUI.AntList<RedundancyRow> _redundancyRows =
            new AntdUI.AntList<RedundancyRow>();
        private readonly HashSet<RedundancyRow> _expandedRedundancyRows =
            new HashSet<RedundancyRow>();
        private string _redundancyAnalysisProgressText =
            string.Empty;
        private RedundancyAnalysisPhase _displayedRedundancyProgressPhase =
            RedundancyAnalysisPhase.SizeGrouping;
        private RedundancyAnalysisPhase _pendingRedundancyProgressPhase =
            RedundancyAnalysisPhase.SizeGrouping;
        private DateTime _pendingRedundancyProgressPhaseSince =
            DateTime.MinValue;
        private AntdUI.TabPage _redundanciesPage;
        private bool _redundanciesLoaded;
        private bool _redundanciesLoading;
        private SizeUnit _sizeUnit = SizeUnit.MB;

        public AdvancedFeaturesForm(
    FileSystemEntry rootEntry,
    AppSettings settings,
    Chart_TableGridChart entryGrid)
        {
            _rootEntry = rootEntry ??
                throw new ArgumentNullException(nameof(rootEntry));

            AntdThemeService.Apply(settings.Layout);

            Text = LocalizationService.GetText("Advanced.Title");
            Icon = AppResources.ApplicationIcon;
            Width = 1050;
            Height = 700;
            AutoSize = false;
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AntdThemeService.BackgroundPrimary;
            ForeColor = AntdThemeService.TextPrimary;

            AntdUI.Tabs tabs = new AntdUI.Tabs
            {
                Name = "analysisTabs",
                Dock = DockStyle.Fill
            };

            AntdThemeService.ConfigureAnalysisTabs(tabs);
            tabs.Pages.Add(CreateFileTypesPage());
            tabs.Pages.Add(CreateFileTypeCategoriesPage());
            tabs.Pages.Add(CreateLargestFilesPage());
            _redundanciesPage = CreateRedundanciesPage();
            tabs.Pages.Add(_redundanciesPage);
            tabs.SelectedIndexChanged += async (sender, e) =>
            {
                if (ReferenceEquals(
                        tabs.SelectedTab,
                        _redundanciesPage))
                {
                    await LoadRedundanciesAsync();
                }
            };
            Controls.Add(tabs);

            AntdThemeService.Apply(this, settings.Layout);
            ApplyTheme();
            RefreshData();
        }

        private AntdUI.TabPage CreateFileTypesPage()
        {
            _fileTypeGrid.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column(
                    nameof(FileTypeRow.Extension),
                    LocalizationService.GetText("Advanced.FileType"))
                {
                    Ellipsis = false,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        string text = record is FileTypeRow row
                            ? row.Extension
                            : value?.ToString();

                        return AntdThemeService.CreateVisibleTableCellText(
                            text);
                    }
                },
                new AntdUI.Column(
                    nameof(FileTypeRow.UsagePercent),
                    LocalizationService.GetText("Advanced.Usage"),
                    AntdUI.ColumnAlign.Center)
                {
                    Width =
                        (AntdThemeService.TableProgressWidth +
                         (AntdThemeService.TableCellHorizontalPadding * 2))
                        .ToString(),
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        double percent = record is FileTypeRow row
                            ? row.UsagePercent
                            : 0D;

                        return new AnalysisPercentCellProgress(
                            (float)Math.Clamp(
                                percent / 100D,
                                0D,
                                1D),
                            $"{percent:0.0} %");
                    }
                },
                new AntdUI.Column(
                    nameof(FileTypeRow.SizeGb),
                    LocalizationService.GetText("Advanced.SizeGb"),
                    AntdUI.ColumnAlign.Right)
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true
                },
                new AntdUI.Column(
                    nameof(FileTypeRow.SizeMb),
                    LocalizationService.GetText("Advanced.SizeMb"),
                    AntdUI.ColumnAlign.Right)
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true
                }
            };

            _fileTypeGrid.SetResponsiveColumns(
                (
                    nameof(FileTypeRow.Extension),
                    AntdThemeService.AnalysisFileTypeColumnWidthPercent
                ));

            return CreatePage(
                LocalizationService.GetText("Advanced.FileTypes"),
                _fileTypeGrid);
        }

        private AntdUI.TabPage CreateFileTypeCategoriesPage()
        {
            _fileTypeCategoryGrid.Columns =
                new AntdUI.ColumnCollection
                {
                    new AntdUI.Column(
                        nameof(FileTypeCategoryRow.FileType),
                        LocalizationService.GetText(
                            "Advanced.FileCategories"))
                    {
                        Ellipsis = false,
                        SortOrder = true,
                        Render = (value, record, rowIndex) =>
                        {
                            string text =
                                record is FileTypeCategoryRow row
                                    ? row.FileType
                                    : value?.ToString();

                            return AntdThemeService
                                .CreateVisibleTableCellText(text);
                        }
                    },
                    new AntdUI.Column(
                        nameof(FileTypeCategoryRow.UsagePercent),
                        LocalizationService.GetText(
                            "Advanced.Usage"),
                        AntdUI.ColumnAlign.Center)
                    {
                        Width =
                            (AntdThemeService.TableProgressWidth +
                             (AntdThemeService.TableCellHorizontalPadding * 2))
                            .ToString(),
                        SortOrder = true,
                        Render = (value, record, rowIndex) =>
                        {
                            double percent =
                                record is FileTypeCategoryRow row
                                    ? row.UsagePercent
                                    : 0D;

                            return new AnalysisPercentCellProgress(
                                (float)Math.Clamp(
                                    percent / 100D,
                                    0D,
                                    1D),
                                $"{percent:0.0} %");
                        }
                    },
                    new AntdUI.Column(
                        nameof(FileTypeCategoryRow.SizeGb),
                        LocalizationService.GetText(
                            "Advanced.SizeGb"),
                        AntdUI.ColumnAlign.Right)
                    {
                        Width = "auto",
                        Ellipsis = true,
                        SortOrder = true
                    },
                    new AntdUI.Column(
                        nameof(FileTypeCategoryRow.SizeMb),
                        LocalizationService.GetText(
                            "Advanced.SizeMb"),
                        AntdUI.ColumnAlign.Right)
                    {
                        Width = "auto",
                        Ellipsis = true,
                        SortOrder = true
                    }
                };

            _fileTypeCategoryGrid.SetResponsiveColumns(
                (
                    nameof(FileTypeCategoryRow.FileType),
                    AntdThemeService
                        .AnalysisFileTypeCategoryColumnWidthPercent
                ));

            return CreatePage(
                LocalizationService.GetText(
                    "Advanced.FileCategories"),
                _fileTypeCategoryGrid);
        }

        private AntdUI.TabPage CreateRedundanciesPage()
        {
            _redundancyGrid.Columns =
                new AntdUI.ColumnCollection
                {
                    new AntdUI.Column(
                        nameof(RedundancyRow.Name),
                        LocalizationService.GetText(
                            "Common.Name"))
                    {
                        KeyTree =
                            nameof(RedundancyRow.Children),
                        Ellipsis = true,
                        SortOrder = true,
                        Render = (value, record, rowIndex) =>
                        {
                            string name =
                                record is RedundancyRow row
                                    ? row.Name
                                    : value?.ToString();

                            return AntdThemeService
                                .CreateVisibleTableCellText(name);
                        }
                    },
                    new AntdUI.Column(
                        nameof(RedundancyRow.UsagePercent),
                        LocalizationService.GetText(
                            "Advanced.Usage"),
                        AntdUI.ColumnAlign.Center)
                    {
                        Width =
                            (AntdThemeService.TableProgressWidth +
                             (AntdThemeService.TableCellHorizontalPadding * 2))
                            .ToString(),
                        SortOrder = true,
                        Render = (value, record, rowIndex) =>
                        {
                            if (record is not RedundancyRow row ||
                                row.IsLocation ||
                                !row.UsagePercent.HasValue)
                            {
                                return string.Empty;
                            }

                            double percent =
                                row.UsagePercent.Value;

                            return new AnalysisPercentCellProgress(
                                (float)Math.Clamp(
                                    percent / 100D,
                                    0D,
                                    1D),
                                $"{percent:0.0} %");
                        }
                    },
                    new AntdUI.Column(
                        nameof(RedundancyRow.Count),
                        LocalizationService.GetText(
                            "Advanced.Count"),
                        AntdUI.ColumnAlign.Right)
                    {
                        Width = "auto",
                        Ellipsis = true,
                        SortOrder = true
                    },
                    new AntdUI.Column(
                        nameof(RedundancyRow.SizeBytes),
                        LocalizationService.GetText(
                            "Advanced.SizeGb"),
                        AntdUI.ColumnAlign.Right)
                    {
                        Width = "auto",
                        Ellipsis = true,
                        SortOrder = true,
                        Render = (value, record, rowIndex) =>
                        {
                            if (record is not RedundancyRow row ||
                                row.IsLocation ||
                                !row.SizeBytes.HasValue)
                            {
                                return string.Empty;
                            }

                            return
                                (row.SizeBytes.Value /
                                 (1024D * 1024D * 1024D))
                                .ToString("N2") +
                                " GB";
                        }
                    },
                    new AntdUI.Column(
                        nameof(RedundancyRow.TotalSizeBytes),
                        LocalizationService.GetText(
                            "ScanHistory.TotalSize") +
                            " (GB)",
                        AntdUI.ColumnAlign.Right)
                    {
                        Width = "auto",
                        Ellipsis = true,
                        SortOrder = true,
                        Render = (value, record, rowIndex) =>
                        {
                            if (record is not RedundancyRow row ||
                                row.IsLocation ||
                                !row.TotalSizeBytes.HasValue)
                            {
                                return string.Empty;
                            }

                            return
                                (row.TotalSizeBytes.Value /
                                 (1024D * 1024D * 1024D))
                                .ToString("N2") +
                                " GB";
                        }
                    }
                };

            _redundancyGrid.AutoSizeColumnsMode =
                AntdUI.ColumnsMode.Fill;
            _redundancyGrid.DefaultExpand = false;
            _redundancyGrid.TooltipConfig =
                new AntdUI.TooltipConfig
                {
                    CustomWidth =
                        Math.Max(
                            1,
                            Screen.FromControl(
                                _redundancyGrid)
                                .Bounds.Width / 4)
                };
            _redundancyGrid.CellHover +=
                RedundancyGrid_CellHover;
            _redundancyGrid.ExpandChanged +=
                RedundancyGrid_ExpandChanged;
            _redundancyGrid.MouseDown +=
                RedundancyGrid_MouseDown;
            _redundancyGrid.CellClickBegin +=
                RedundancyGrid_CellClickBegin;

            ToolStripMenuItem openParentFolderItem =
                new ToolStripMenuItem(
                    LocalizationService.GetText(
                        "Search.OpenParentFolder"));
            openParentFolderItem.Click +=
                RedundancyOpenParentFolder_Click;
            _redundancyContextMenu.Items.Add(
                openParentFolderItem);
            _redundancyContextMenu.Opening +=
                (sender, e) =>
                    e.Cancel =
                        _redundancyContextRow == null ||
                        !_redundancyContextRow.IsLocation;
            AntdThemeService.ConfigureContextMenu(
                _redundancyContextMenu);
            _redundancyGrid.ContextMenuStrip =
                _redundancyContextMenu;

            _redundancyGrid.SetResponsiveColumns(
                (
                    nameof(RedundancyRow.Name),
                    AntdThemeService
                        .AnalysisRedundancyNameColumnWidthPercent
                ));

            _redundancyAnalysisProgressBar.Dock =
                DockStyle.Top;
            _redundancyAnalysisProgressBar.Height = 28;
            _redundancyAnalysisProgressBar.Margin =
                Padding.Empty;
            _redundancyAnalysisProgressBar.Back =
                AntdThemeService.TableProgressBackColor;
            _redundancyAnalysisProgressBar.Fill =
                AntdThemeService.TableProgressFillColor;
            _redundancyAnalysisProgressBar.ForeColor =
                AntdThemeService.TextPrimary;
            _redundancyAnalysisProgressBar.Radius =
                AntdThemeService.TableProgressRadius;
            _redundancyAnalysisProgressBar.UseSystemText =
                false;
            _redundancyAnalysisProgressBar.UseTextCenter =
                true;
            _redundancyAnalysisProgressBar.ValueFormatChanged +=
                (sender, e) =>
                    _redundancyAnalysisProgressText;
            _redundancyAnalysisProgressBar.Value = 0F;
            _redundancyAnalysisProgressText =
                $"0 % ({LocalizationService.GetText("Advanced.Redundancy.Progress.SizeGrouping")})";

            TableLayoutPanel redundancyContent =
                new TableLayoutPanel
                {
                    BackColor =
                        AntdThemeService.BackgroundPrimary,
                    Padding = Padding.Empty,
                    Margin = Padding.Empty,
                    ColumnCount = 1,
                    RowCount = 2
                };

            redundancyContent.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));
            redundancyContent.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    _redundancyAnalysisProgressBar.Height));
            redundancyContent.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));

            _redundancyAnalysisProgressBar.Dock =
                DockStyle.Fill;
            _redundancyGrid.Dock =
                DockStyle.Fill;

            redundancyContent.Controls.Add(
                _redundancyAnalysisProgressBar,
                0,
                0);
            redundancyContent.Controls.Add(
                _redundancyGrid,
                0,
                1);

            return CreatePage(
                LocalizationService.GetText(
                    "Advanced.Redundancies"),
                redundancyContent);
        }

        private async Task LoadRedundanciesAsync()
        {
            if (_redundanciesLoaded ||
                _redundanciesLoading)
            {
                return;
            }

            _redundanciesLoading = true;

            ConcurrentQueue<RedundancyAnalysisGroup>
                pendingGroups =
                    new ConcurrentQueue<RedundancyAnalysisGroup>();

            using System.Windows.Forms.Timer updateTimer =
                new System.Windows.Forms.Timer
                {
                    Interval = 200
                };

            int previousAnimationTime =
                _redundancyGrid.AnimationTime;

            try
            {
                List<FileSystemEntry> files =
                    GetFiles();

                long totalBytes =
                    files.Sum(file => file.SizeBytes);

                _redundancyRows =
                    new AntdUI.AntList<RedundancyRow>();
                _expandedRedundancyRows.Clear();
                _redundancyGrid.Binding(
                    _redundancyRows);

                _redundancyGrid.AnimationTime = 0;

                RedundancyAnalysisProgress progress =
                    new RedundancyAnalysisProgress(
                        pendingGroups);

                _displayedRedundancyProgressPhase =
                    RedundancyAnalysisPhase.SizeGrouping;
                _pendingRedundancyProgressPhase =
                    RedundancyAnalysisPhase.SizeGrouping;
                _pendingRedundancyProgressPhaseSince =
                    DateTime.UtcNow;
                _redundancyAnalysisProgressText =
                    $"0 % ({LocalizationService.GetText("Advanced.Redundancy.Progress.SizeGrouping")})";
                _redundancyAnalysisProgressBar.Value = 0F;

                Progress<RedundancyAnalysisProgressInfo>
                    analysisProgress =
                        new Progress<RedundancyAnalysisProgressInfo>(
                            progressInfo =>
                            {
                                if (IsDisposed ||
                                    Disposing ||
                                    progressInfo == null)
                                {
                                    return;
                                }

                                int clampedPercentage =
                                    Math.Clamp(
                                        progressInfo.Percentage,
                                        0,
                                        100);

                                UpdateRedundancyProgressPhase(
                                    progressInfo.Phase);

                                _redundancyAnalysisProgressText =
                                    $"{clampedPercentage} % ({GetRedundancyProgressAction(_displayedRedundancyProgressPhase)})";
                                _redundancyAnalysisProgressBar.Value =
                                    clampedPercentage / 100F;
                                _redundancyAnalysisProgressBar.Invalidate();
                            });

                updateTimer.Tick +=
                    (sender, e) =>
                        FlushPendingRedundancyGroups(
                            pendingGroups,
                            totalBytes,
                            100);

                updateTimer.Start();

                await Task.Run(
                    () =>
                        RedundancyAnalysisService.Analyze(
                            files,
                            _redundancyCancellationTokenSource.Token,
                            progress,
                            analysisProgress),
                    _redundancyCancellationTokenSource.Token);

                while (!pendingGroups.IsEmpty)
                {
                    FlushPendingRedundancyGroups(
                        pendingGroups,
                        totalBytes,
                        100);

                    if (!pendingGroups.IsEmpty)
                    {
                        await Task.Delay(50);
                    }
                }

                _redundancyAnalysisProgressText =
                    $"100 % ({LocalizationService.GetText("Advanced.Redundancy.Progress.Completed")})";
                _redundancyAnalysisProgressBar.Value = 1F;
                _redundancyAnalysisProgressBar.Invalidate();
                _redundanciesLoaded = true;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                updateTimer.Stop();
                _redundancyGrid.AnimationTime =
                    previousAnimationTime;
                _redundanciesLoading = false;
            }
        }

        private void FlushPendingRedundancyGroups(
            ConcurrentQueue<RedundancyAnalysisGroup> pendingGroups,
            long totalBytes,
            int maximumGroups,
            bool forceRefresh = false)
        {
            if (IsDisposed ||
                Disposing ||
                pendingGroups == null ||
                maximumGroups <= 0)
            {
                return;
            }

            if (!forceRefresh &&
                _expandedRedundancyRows.Count > 0)
            {
                return;
            }

            int addedGroups = 0;

            _redundancyGrid.PauseLayout = true;

            try
            {
                while (addedGroups < maximumGroups &&
                    pendingGroups.TryDequeue(
                        out RedundancyAnalysisGroup group))
                {
                    RedundancyRow row =
                        new RedundancyRow
                        {
                            Name = group.Name,
                            UsagePercent =
                                totalBytes > 0
                                    ? group.TotalSizeBytes *
                                        100D /
                                        totalBytes
                                    : 0D,
                            Count =
                                group.PhysicalCopyCount,
                            SizeBytes =
                                group.SizeBytes,
                            TotalSizeBytes =
                                group.TotalSizeBytes,
                            IsLocation = false,
                            Children =
                                group.Locations
                                    .Select(path =>
                                        new RedundancyRow
                                        {
                                            Name = path,
                                            IsLocation = true,
                                            Children =
                                                new List<RedundancyRow>()
                                        })
                                    .ToList()
                        };

                    int insertIndex =
                        FindRedundancyInsertIndex(
                            row.TotalSizeBytes ?? 0L);

                    _redundancyRows.Insert(
                        insertIndex,
                        row);

                    addedGroups++;
                }
            }
            finally
            {
                _redundancyGrid.PauseLayout = false;
            }

            if (addedGroups > 0)
            {
                List<RedundancyRow> expandedRows =
                    _expandedRedundancyRows
                        .ToList();

                _redundancyGrid.Refresh(
                    _redundancyRows);

                foreach (RedundancyRow expandedRow in
                    expandedRows)
                {
                    if (_redundancyRows.Contains(
                            expandedRow))
                    {
                        _redundancyGrid.Expand(
                            expandedRow,
                            true);
                    }
                }
            }
        }

        private void RedundancyGrid_ExpandChanged(
            object sender,
            AntdUI.TableExpandEventArgs e)
        {
            if (e.Record is not RedundancyRow row ||
                row.IsLocation)
            {
                return;
            }

            if (e.Expand)
            {
                _expandedRedundancyRows.Add(
                    row);
            }
            else
            {
                _expandedRedundancyRows.Remove(
                    row);
            }
        }

        private void RedundancyGrid_MouseDown(
            object sender,
            MouseEventArgs e)
        {
            _redundancyContextRow = null;
        }

        private void RedundancyGrid_CellHover(
            object sender,
            AntdUI.TableHoverEventArgs e)
        {
            dynamic eventArgs = e;

            RedundancyRow row =
                eventArgs.Record as RedundancyRow;
            AntdUI.Column column =
                eventArgs.Column as AntdUI.Column;

            if (row == null ||
                !row.IsLocation ||
                column == null ||
                !string.Equals(
                    column.Key,
                    nameof(RedundancyRow.Name),
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(
                    row.Name))
            {
                _redundancyGrid.CloseTip();
                return;
            }

            Rectangle rect =
                eventArgs.Rect;

            _redundancyGrid.OpenTip(
                rect,
                row.Name);
        }

        private void RedundancyGrid_CellClickBegin(
            object sender,
            AntdUI.TableClickBeginEventArgs e)
        {
            dynamic eventArgs = e;

            _redundancyContextRow =
                eventArgs.Record as RedundancyRow;
        }

        private void RedundancyOpenParentFolder_Click(
            object sender,
            EventArgs e)
        {
            if (_redundancyContextRow == null ||
                !_redundancyContextRow.IsLocation ||
                string.IsNullOrWhiteSpace(
                    _redundancyContextRow.Name))
            {
                return;
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments =
                        "/select,\"" +
                        _redundancyContextRow.Name +
                        "\"",
                    UseShellExecute = true
                });
        }

        private int FindRedundancyInsertIndex(
            long totalSizeBytes)
        {
            int low = 0;
            int high = _redundancyRows.Count;

            while (low < high)
            {
                int middle =
                    low + ((high - low) / 2);

                long middleSize =
                    _redundancyRows[middle]
                        .TotalSizeBytes ??
                    0L;

                if (middleSize >= totalSizeBytes)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        private void UpdateRedundancyProgressPhase(
            RedundancyAnalysisPhase phase)
        {
            if (phase == RedundancyAnalysisPhase.Completed)
            {
                _displayedRedundancyProgressPhase =
                    RedundancyAnalysisPhase.Completed;
                _pendingRedundancyProgressPhase =
                    RedundancyAnalysisPhase.Completed;
                _pendingRedundancyProgressPhaseSince =
                    DateTime.UtcNow;
                return;
            }

            RedundancyAnalysisPhase displayPhase =
                phase switch
                {
                    RedundancyAnalysisPhase.FirstBlock =>
                        RedundancyAnalysisPhase.FullHashLive,
                    RedundancyAnalysisPhase.LastBlock =>
                        RedundancyAnalysisPhase.FullHashLive,
                    RedundancyAnalysisPhase.FullHashLive =>
                        RedundancyAnalysisPhase.FullHashLive,
                    RedundancyAnalysisPhase.FullHashCache =>
                        RedundancyAnalysisPhase.FullHashCache,
                    _ => phase
                };

            bool isReadPhase =
                displayPhase ==
                    RedundancyAnalysisPhase.FullHashLive ||
                displayPhase ==
                    RedundancyAnalysisPhase.FullHashCache;

            bool pendingIsReadPhase =
                _pendingRedundancyProgressPhase ==
                    RedundancyAnalysisPhase.FullHashLive ||
                _pendingRedundancyProgressPhase ==
                    RedundancyAnalysisPhase.FullHashCache;

            if (!isReadPhase &&
                pendingIsReadPhase)
            {
                return;
            }

            if (displayPhase !=
                _pendingRedundancyProgressPhase)
            {
                _pendingRedundancyProgressPhase =
                    displayPhase;
                _pendingRedundancyProgressPhaseSince =
                    DateTime.UtcNow;
                return;
            }

            if (_displayedRedundancyProgressPhase !=
                    _pendingRedundancyProgressPhase &&
                DateTime.UtcNow -
                    _pendingRedundancyProgressPhaseSince >=
                    TimeSpan.FromSeconds(1))
            {
                _displayedRedundancyProgressPhase =
                    _pendingRedundancyProgressPhase;
            }
        }

        private static string GetRedundancyProgressAction(
            RedundancyAnalysisPhase phase)
        {
            return phase switch
            {
                RedundancyAnalysisPhase.SizeGrouping =>
                    LocalizationService.GetText(
                        "Advanced.Redundancy.Progress.SizeGrouping"),
                RedundancyAnalysisPhase.FirstBlock =>
                    LocalizationService.GetText(
                        "Advanced.Redundancy.Progress.LiveRead"),
                RedundancyAnalysisPhase.LastBlock =>
                    LocalizationService.GetText(
                        "Advanced.Redundancy.Progress.LiveRead"),
                RedundancyAnalysisPhase.FullHashLive =>
                    LocalizationService.GetText(
                        "Advanced.Redundancy.Progress.LiveRead"),
                RedundancyAnalysisPhase.FullHashCache =>
                    LocalizationService.GetText(
                        "Advanced.Redundancy.Progress.CacheRead"),
                RedundancyAnalysisPhase.FileIdentity =>
                    LocalizationService.GetText(
                        "Advanced.Redundancy.Progress.FileIdentity"),
                RedundancyAnalysisPhase.Cache =>
                    LocalizationService.GetText(
                        "Advanced.Redundancy.Progress.CacheSave"),
                RedundancyAnalysisPhase.Completed =>
                    LocalizationService.GetText(
                        "Advanced.Redundancy.Progress.Completed"),
                _ =>
                    LocalizationService.GetText(
                        "Advanced.Redundancies")
            };
        }

        private AntdUI.TabPage CreateLargestFilesPage()
        {
            _largestFilesGrid.Columns = CreateLargestFilesColumns();
            _largestFilesGrid.AutoSizeColumnsMode =
                AntdUI.ColumnsMode.Auto;
            _largestFilesGrid.TooltipConfig =
                new AntdUI.TooltipConfig
                {
                    CustomWidth =
                        Math.Max(
                            1,
                            Screen.FromControl(
                                _largestFilesGrid)
                                .Bounds.Width / 4)
                };
            _largestFilesGrid.CellHover +=
                LargestFilesGrid_CellHover;
            _largestFilesGrid.MouseDown +=
                LargestFilesGrid_MouseDown;
            _largestFilesGrid.CellClickBegin +=
                LargestFilesGrid_CellClickBegin;
            _largestFilesGrid.CellClick +=
                LargestFilesGrid_CellClick;
            _largestFilesGrid.CellDoubleClick +=
                LargestFilesGrid_CellDoubleClick;

            ToolStripMenuItem openParentFolderItem =
                new ToolStripMenuItem(
                    LocalizationService.GetText(
                        "Search.OpenParentFolder"));
            openParentFolderItem.Click +=
                LargestFilesOpenParentFolder_Click;
            _largestFilesContextMenu.Items.Add(
                openParentFolderItem);
            _largestFilesContextMenu.Opening +=
                (sender, e) =>
                    e.Cancel =
                        _largestFilesContextRow == null;
            AntdThemeService.ConfigureContextMenu(
                _largestFilesContextMenu);
            _largestFilesGrid.ContextMenuStrip =
                _largestFilesContextMenu;

            _largestFilesGrid.SetResponsiveColumns(
                (
                    nameof(LargestFileRow.Name),
                    AntdThemeService.AnalysisLargestFilesNameColumnWidthPercent
                ));

            return CreatePage(
                LocalizationService.GetText("Advanced.LargestFiles"),
                _largestFilesGrid);
        }

        private AntdUI.ColumnCollection CreateLargestFilesColumns()
        {
            return new AntdUI.ColumnCollection
            {
                new AntdUI.Column(
                    nameof(LargestFileRow.Name),
                    LocalizationService.GetText("Common.Name"))
                {
                    Ellipsis = true,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        string name = record is LargestFileRow row
                            ? row.Name
                            : value?.ToString();

                        return AntdThemeService.CreateVisibleTableCellText(name);
                    }
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.UsagePercent),
                    LocalizationService.GetText("Advanced.Usage"),
                    AntdUI.ColumnAlign.Center)
                {
                    Width =
                        (AntdThemeService.TableProgressWidth +
                         (AntdThemeService.TableCellHorizontalPadding * 2))
                        .ToString(),
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        double percent = record is LargestFileRow row
                            ? row.UsagePercent
                            : 0D;

                        return new AnalysisPercentCellProgress(
                            (float)Math.Clamp(
                                percent / 100D,
                                0D,
                                1D),
                            $"{percent:0.0} %");
                    }
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.FormattedSize),
                    LocalizationService.GetText("Advanced.SizeGb"),
                    AntdUI.ColumnAlign.Right)
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.SizeBytes),
                    GetSizeUnitHeader(),
                    AntdUI.ColumnAlign.Right)
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        long sizeBytes = record is LargestFileRow row
                            ? row.SizeBytes
                            : 0L;

                        return FormatSizeValue(sizeBytes);
                    }
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.LastWriteTime),
                    LocalizationService.GetText("Advanced.Modified"))
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        string modified =
                            record is LargestFileRow row &&
                            row.LastWriteTime != DateTime.MinValue
                                ? row.LastWriteTime.ToString("g")
                                : string.Empty;

                        return AntdThemeService.CreateVisibleTableCellText(
                            modified);
                    }
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.FullPath),
                    LocalizationService.GetText("Common.Path"))
                {
                    Width = "fill",
                    Ellipsis = true,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        string path = record is LargestFileRow row
                            ? row.FullPath
                            : value?.ToString();

                        return AntdThemeService.CreateVisibleTableCellText(path);
                    }
                }
            };
        }

        private void RefreshData()
        {
            List<FileSystemEntry> files = GetFiles();
            long totalFileTypeBytes =
                files.Sum(file => file.SizeBytes);

            _fileTypeRows = files
                .GroupBy(file => string.IsNullOrWhiteSpace(
                    Path.GetExtension(file.Name))
                    ? LocalizationService.GetText(
                        "Advanced.NoExtension")
                    : Path.GetExtension(file.Name)
                        .ToLowerInvariant())
                .Select(group =>
                {
                    long sizeBytes =
                        group.Sum(file => file.SizeBytes);

                    return new FileTypeRow
                    {
                        Extension = group.Key,
                        UsagePercent = totalFileTypeBytes > 0
                            ? sizeBytes * 100D /
                                totalFileTypeBytes
                            : 0D,
                        SizeGb =
                            (sizeBytes /
                             (1024D * 1024D * 1024D))
                            .ToString("N2") + " GB",
                        SizeMb =
                            (sizeBytes /
                             (1024D * 1024D))
                            .ToString("N0") + " MB",
                        SizeBytes = sizeBytes
                    };
                })
                .OrderByDescending(row => row.SizeBytes)
                .ToList();

            _fileTypeCategoryRows = files
                .GroupBy(file =>
                    GetFileTypeCategoryKey(file.Name))
                .Select(group =>
                {
                    long sizeBytes =
                        group.Sum(file => file.SizeBytes);

                    return new FileTypeCategoryRow
                    {
                        FileType =
                            LocalizationService.GetText(
                                group.Key),
                        UsagePercent = totalFileTypeBytes > 0
                            ? sizeBytes * 100D /
                                totalFileTypeBytes
                            : 0D,
                        SizeGb =
                            (sizeBytes /
                             (1024D * 1024D * 1024D))
                            .ToString("N2") + " GB",
                        SizeMb =
                            (sizeBytes /
                             (1024D * 1024D))
                            .ToString("N0") + " MB",
                        SizeBytes = sizeBytes
                    };
                })
                .OrderByDescending(row => row.SizeBytes)
                .ToList();

            _largestFileRows = files
                .OrderByDescending(file => file.SizeBytes)
                .Take(1000)
                .Select(file => new LargestFileRow
                {
                    Name = file.Name,
                    UsagePercent = totalFileTypeBytes > 0
                        ? file.SizeBytes * 100D /
                            totalFileTypeBytes
                        : 0D,
                    FormattedSize =
                        SizeFormatter.Format(file.SizeBytes),
                    SizeBytes = file.SizeBytes,
                    LastWriteTime =
                        file.LastWriteTimeUtc ==
                            DateTime.MinValue
                            ? DateTime.MinValue
                            : file.LastWriteTimeUtc
                                .ToLocalTime(),
                    FullPath = file.FullPath
                })
                .ToList();

            _fileTypeGrid.DataSource = _fileTypeRows;
            _fileTypeCategoryGrid.DataSource =
                _fileTypeCategoryRows;
            _largestFilesGrid.DataSource =
                _largestFileRows;
        }

        private void LargestFilesGrid_MouseDown(
            object sender,
            MouseEventArgs e)
        {
            _largestFilesContextRow = null;
        }

        private void LargestFilesGrid_CellHover(
            object sender,
            AntdUI.TableHoverEventArgs e)
        {
            dynamic eventArgs = e;

            LargestFileRow row =
                eventArgs.Record as LargestFileRow;
            AntdUI.Column column =
                eventArgs.Column as AntdUI.Column;

            if (row == null ||
                column == null ||
                (!string.Equals(
                    column.Key,
                    nameof(LargestFileRow.Name),
                    StringComparison.Ordinal) &&
                 !string.Equals(
                    column.Key,
                    nameof(LargestFileRow.FullPath),
                    StringComparison.Ordinal)) ||
                string.IsNullOrWhiteSpace(
                    row.FullPath))
            {
                _largestFilesGrid.CloseTip();
                return;
            }

            Rectangle rect =
                eventArgs.Rect;

            _largestFilesGrid.OpenTip(
                rect,
                row.FullPath);
        }

        private void LargestFilesGrid_CellClickBegin(
            object sender,
            AntdUI.TableClickBeginEventArgs e)
        {
            dynamic eventArgs = e;

            _largestFilesContextRow =
                eventArgs.Record as LargestFileRow;
        }

        private void LargestFilesGrid_CellClick(
            object sender,
            AntdUI.TableClickEventArgs e)
        {
            dynamic eventArgs = e;
            object record = eventArgs.Record;
            AntdUI.Column column = eventArgs.Column;

            _largestFilesContextRow =
                record as LargestFileRow;

            if (record != null ||
                column == null ||
                !string.Equals(
                    column.Key,
                    nameof(LargestFileRow.SizeBytes),
                    StringComparison.Ordinal))
            {
                return;
            }

            CycleSizeUnit();
        }

        private void LargestFilesGrid_CellDoubleClick(
            object sender,
            AntdUI.TableClickEventArgs e)
        {
            dynamic eventArgs = e;

            if (eventArgs.Record is not LargestFileRow selectedRow)
                return;

            OpenSelectedFile(selectedRow);
        }

        private void LargestFilesOpenParentFolder_Click(
            object sender,
            EventArgs e)
        {
            if (_largestFilesContextRow == null)
                return;

            OpenSelectedFile(_largestFilesContextRow);
        }

        private void CycleSizeUnit()
        {
            _sizeUnit = _sizeUnit switch
            {
                SizeUnit.Bytes => SizeUnit.KB,
                SizeUnit.KB => SizeUnit.MB,
                SizeUnit.MB => SizeUnit.GB,
                SizeUnit.GB => SizeUnit.TB,
                _ => SizeUnit.Bytes
            };

            AntdUI.Column sizeColumn =
                _largestFilesGrid.Columns.FirstOrDefault(
                    column => string.Equals(
                        column.Key,
                        nameof(LargestFileRow.SizeBytes),
                        StringComparison.Ordinal));

            if (sizeColumn != null)
                sizeColumn.Title = GetSizeUnitHeader();

            _largestFilesGrid.LoadLayout();
            _largestFilesGrid.Invalidate();
        }

        private string GetSizeUnitHeader()
        {
            return $"{LocalizationService.GetText("Common.Size")} ({_sizeUnit})";
        }

        private string FormatSizeValue(long sizeBytes)
        {
            double divisor = _sizeUnit switch
            {
                SizeUnit.KB => 1024D,
                SizeUnit.MB => 1024D * 1024D,
                SizeUnit.GB => 1024D * 1024D * 1024D,
                SizeUnit.TB =>
                    1024D * 1024D * 1024D * 1024D,
                _ => 1D
            };

            if (_sizeUnit == SizeUnit.Bytes)
                return sizeBytes.ToString("N0");

            if (_sizeUnit == SizeUnit.MB)
            {
                return (sizeBytes / divisor).ToString("N0") +
                    " MB";
            }

            return (sizeBytes / divisor).ToString("N2");
        }

        private static string GetFileTypeCategoryKey(
            string fileName)
        {
            if (IsKnownBackupFileName(fileName))
            {
                return "Advanced.FileType.Backups";
            }

            if (IsKnownGameDataFileName(fileName))
            {
                return "Advanced.FileType.GameFiles";
            }

            string extension = Path.GetExtension(fileName);

            if (!string.IsNullOrWhiteSpace(extension) &&
                FileTypeCategoryByExtension.TryGetValue(
                    extension,
                    out string categoryKey))
            {
                return categoryKey;
            }

            return "Advanced.FileType.OtherFiles";
        }

        private static bool IsKnownBackupFileName(
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                !fileName.StartsWith(
                    "vzdump-",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return
                fileName.EndsWith(
                    ".tar",
                    StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(
                    ".tar.zst",
                    StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(
                    ".tar.gz",
                    StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(
                    ".tar.lzo",
                    StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(
                    ".vma",
                    StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(
                    ".vma.zst",
                    StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(
                    ".vma.gz",
                    StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(
                    ".vma.lzo",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKnownGameDataFileName(
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (fileName.Length == 8 &&
                fileName.StartsWith(
                    "data.",
                    StringComparison.OrdinalIgnoreCase) &&
                char.IsDigit(fileName[5]) &&
                char.IsDigit(fileName[6]) &&
                char.IsDigit(fileName[7]))
            {
                return true;
            }

            const string EnshroudedPrefix = "enshrouded_";
            const string DatExtension = ".dat";

            if (!fileName.StartsWith(
                    EnshroudedPrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                !fileName.EndsWith(
                    DatExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int numberStart = EnshroudedPrefix.Length;
            int numberLength =
                fileName.Length -
                EnshroudedPrefix.Length -
                DatExtension.Length;

            return
                numberLength == 3 &&
                char.IsDigit(fileName[numberStart]) &&
                char.IsDigit(fileName[numberStart + 1]) &&
                char.IsDigit(fileName[numberStart + 2]);
        }

        private List<FileSystemEntry> GetFiles()
        {
            if (_rootEntry.AllFiles != null &&
                _rootEntry.AllFiles.Count > 0)
            {
                return _rootEntry.AllFiles
                    .Where(file =>
                        file != null &&
                        !file.IsDirectory)
                    .ToList();
            }

            List<FileSystemEntry> files =
                new List<FileSystemEntry>();

            CollectFiles(_rootEntry, files);
            return files;
        }

        private static void CollectFiles(
            FileSystemEntry entry,
            List<FileSystemEntry> files)
        {
            if (entry == null)
                return;

            foreach (FileSystemEntry child in entry.Children)
            {
                if (child.IsDirectory)
                    CollectFiles(child, files);
                else
                    files.Add(child);
            }
        }

        private static AntdUI.TabPage CreatePage(
            string title,
            Control control)
        {
            AntdUI.TabPage page = new AntdUI.TabPage
            {
                Text = title,
                BackColor =
                    AntdThemeService.BackgroundPrimary,
                ForeColor =
                    AntdThemeService.TextPrimary,
                Padding = Padding.Empty
            };

            control.Dock = DockStyle.Fill;
            page.Controls.Add(control);
            return page;
        }

        private void ApplyTheme()
        {
            BackColor =
                AntdThemeService.BackgroundPrimary;
            ForeColor =
                AntdThemeService.TextPrimary;

            _fileTypeGrid.ApplyAntdUIStyle();
            _fileTypeCategoryGrid.ApplyAntdUIStyle();
            _redundancyGrid.ApplyAntdUIStyle();
            _largestFilesGrid.ApplyAntdUIStyle();
        }

        protected override void OnFormClosing(
            FormClosingEventArgs e)
        {
            _redundancyCancellationTokenSource.Cancel();
            base.OnFormClosing(e);
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _redundancyCancellationTokenSource.Dispose();
            }

            base.Dispose(disposing);
        }

        private static void OpenSelectedFile(
            LargestFileRow selectedRow)
        {
            string path = selectedRow?.FullPath;

            if (string.IsNullOrWhiteSpace(path))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = File.Exists(path)
                    ? "/select,\"" + path + "\""
                    : "\"" + path + "\"",
                UseShellExecute = true
            });
        }

        private sealed class AnalysisPercentCellProgress :
            AntdUI.CellProgress
        {
            private readonly string _text;

            public AnalysisPercentCellProgress(
                float value,
                string text)
                : base(value)
            {
                _text = text;
                Radius =
                    AntdThemeService.TableProgressRadius;
                Back =
                    AntdThemeService.TableProgressBackColor;
                Fill =
                    AntdThemeService.TableProgressFillColor;
                Size = new Size(
                    AntdThemeService.TableProgressWidth,
                    AntdThemeService.TableProgressHeight);
            }

            public override void Paint(
                AntdUI.Canvas g,
                Font font,
                bool enable,
                SolidBrush fore)
            {
                base.Paint(g, font, enable, fore);
                g.String(_text, font, fore, Rect);
            }
        }
    }
}
