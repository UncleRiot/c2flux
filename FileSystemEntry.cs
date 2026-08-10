using System.Collections.Generic;
using System.Linq;

namespace c2flux
{
    public sealed class FileSystemEntry
    {
        private List<FileSystemEntry> _allFiles;
        private List<FileSystemEntry> _children;

        public string Name { get; set; }
        public string FullPath { get; set; }
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