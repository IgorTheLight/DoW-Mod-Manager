using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoW_Mod_Manager.Services
{
    /// <summary>What one install by the manager left in a game folder, so it can be undone.</summary>
    public sealed class InstalledModRecord
    {
        public string Name { get; set; } = "";
        public string Module { get; set; } = "";
        public string InstallDir { get; set; } = "";

        /// <summary>Files extracted from archives that did not exist before.</summary>
        public List<string> Files { get; set; } = [];

        /// <summary>Uninstallers that the mod's installer added to the game folder.</summary>
        public List<string> Uninstallers { get; set; } = [];

        public DateTime InstalledUtc { get; set; }
    }

    /// <summary>
    /// JSON file in the manager's data folder: install records per game folder and module, and the
    /// file names seen for each download link (to find an earlier download again).
    /// </summary>
    public sealed partial class InstalledModsStore
    {
        internal sealed class Data
        {
            public Dictionary<string, InstalledModRecord> Mods { get; set; } = new(StringComparer.Ordinal);
            public Dictionary<string, string> Downloads { get; set; } = new(StringComparer.Ordinal);
        }

        /// <summary>Source-generated serializer (reflection-based JSON does not survive trimming).</summary>
        [JsonSourceGenerationOptions(WriteIndented = true)]
        [JsonSerializable(typeof(Data))]
        private sealed partial class JsonContext : JsonSerializerContext;

        private readonly string _path;
        private readonly object _lock = new();

        public InstalledModsStore(string path)
        {
            _path = path;
        }

        public InstalledModRecord? Get(string installDir, string module)
        {
            lock (_lock)
                return Load().Mods.GetValueOrDefault(Key(installDir, module));
        }

        public void Save(InstalledModRecord record) => Update(d => d.Mods[Key(record.InstallDir, record.Module)] = record);

        public void Remove(string installDir, string module) => Update(d => d.Mods.Remove(Key(installDir, module)));

        public string? GetDownloadFileName(string url)
        {
            lock (_lock)
                return Load().Downloads.GetValueOrDefault(url);
        }

        public void RememberDownload(string url, string fileName) => Update(d => d.Downloads[url] = fileName);

        private static string Key(string installDir, string module) =>
            Path.TrimEndingDirectorySeparator(installDir) + "|" + module.ToLowerInvariant();

        private void Update(Action<Data> change)
        {
            lock (_lock)
            {
                var data = Load();
                change(data);
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonContext.Default.Data));
                File.Move(tmp, _path, overwrite: true);
            }
        }

        private Data Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return new Data();
                var data = JsonSerializer.Deserialize(File.ReadAllText(_path), JsonContext.Default.Data) ?? new Data();
                data.Mods = new Dictionary<string, InstalledModRecord>(data.Mods, StringComparer.Ordinal);
                return data;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return new Data();
            }
        }
    }
}
