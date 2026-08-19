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
            string json = File.ReadAllText(filePath);

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };

            return JsonSerializer.Deserialize<FileSystemEntry>(json, options);
        }
    }
}
