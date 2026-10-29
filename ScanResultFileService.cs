using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace c2flux
{
    public static class ScanResultFileService
    {
        public static void Save(string filePath, FileSystemEntry rootEntry)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = false,
                ReferenceHandler = ReferenceHandler.Preserve
            };

            File.WriteAllText(filePath, JsonSerializer.Serialize(rootEntry, options));
        }

        public static FileSystemEntry Load(string filePath)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };

            // SECURITY INVARIANT: Read imported scans as a stream rather than
            // materializing a second full-size JSON string in memory.
            // Preserve the existing JSON format and reference handling.
            using FileStream input = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<FileSystemEntry>(input, options);
        }
    }
}
