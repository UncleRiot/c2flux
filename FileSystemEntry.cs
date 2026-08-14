using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace c2flux
{
    public sealed class FileSystemEntry
    {
        private List<FileSystemEntry> _allFiles;
        private List<FileSystemEntry> _children;
        private string _fullPath;

        internal FileSystemEntry ParentEntry { get; set; }

        public string Name { get; set; }

        public string FullPath
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_fullPath))
                {
                    return _fullPath;
                }

                if (ParentEntry == null ||
                    string.IsNullOrWhiteSpace(Name))
                {
                    return _fullPath;
                }

                string parentPath = ParentEntry.FullPath;

                if (string.IsNullOrWhiteSpace(parentPath))
                {
                    return _fullPath;
                }

                _fullPath = Path.Combine(parentPath, Name);
                return _fullPath;
            }
            set
            {
                _fullPath = value;
            }
        }
        public long SizeBytes { get; set; }
        public bool IsDirectory { get; set; }
        public System.DateTime LastWriteTimeUtc { get; set; }

        public List<FileSystemEntry> AllFiles
        {
            get
            {
                if (_allFiles == null)
                {
                    _allFiles = new List<FileSystemEntry>();
                }

                return _allFiles;
            }
            set
            {
                _allFiles = value;
            }
        }

        public List<FileSystemEntry> Children
        {
            get
            {
                if (_children == null)
                {
                    _children = new List<FileSystemEntry>();
                }

                return _children;
            }
            set
            {
                _children = value;
            }
        }

        public int DirectoryCount
        {
            get { return Children.Count(child => child.IsDirectory); }
        }

        public int FileCount
        {
            get { return Children.Count(child => !child.IsDirectory); }
        }
    }
}