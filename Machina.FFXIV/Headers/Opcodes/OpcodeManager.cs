// Copyright © 2021 Ravahn - All Rights Reserved
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY. without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see<http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Machina.FFXIV.Headers.Opcodes
{
    public class OpcodeManager
    {
        public static OpcodeManager Instance { get; } = new OpcodeManager();

        private readonly Dictionary<GameRegion, Dictionary<string, ushort>> _opcodes;
        private List<string> _loadLog;

        public Dictionary<string, ushort> CurrentOpcodes { get; set; }

        public GameRegion GameRegion { get; private set; }

        /// <summary>
        /// Custom directory path for external opcode files. When set, this path
        /// takes priority over the default auto-detected directory.
        /// Call <see cref="Reload"/> after setting this to apply the change.
        /// </summary>
        public string CustomOpcodeDirectory { get; set; }

        public OpcodeManager()
        {
            _opcodes = new Dictionary<GameRegion, Dictionary<string, ushort>>();
            LoadVersions();
        }

        /// <summary>
        /// Reloads all opcode data. External files are checked first, then
        /// embedded resources are used as a fallback for any missing regions.
        /// </summary>
        public void Reload()
        {
            _opcodes.Clear();
            LoadVersions();

            // Re-apply current region if one was previously set
            if (CurrentOpcodes != null)
                SetRegion(GameRegion);
        }

        private void LoadVersions()
        {
            _loadLog = new List<string>();
            _loadLog.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OpcodeManager.LoadVersions started");

            // Try loading from external files first (allows updating without recompilation)
            string opcodesDir = !string.IsNullOrEmpty(CustomOpcodeDirectory)
                ? CustomOpcodeDirectory
                : GetOpcodeDirectory();

            _loadLog.Add($"Opcodes directory resolved to: {opcodesDir}");
            _loadLog.Add($"Directory exists: {Directory.Exists(opcodesDir)}");

            if (Directory.Exists(opcodesDir))
            {
                foreach (string filePath in Directory.GetFiles(opcodesDir, "*.txt"))
                {
                    string regionString = Path.GetFileNameWithoutExtension(filePath);
                    if (!Enum.TryParse(regionString, out GameRegion gameRegion))
                    {
                        _loadLog.Add($"Skipped file (unknown region): {filePath}");
                        continue;
                    }

                    Dictionary<string, ushort> dict = ParseOpcodeData(File.ReadAllText(filePath));
                    _opcodes[gameRegion] = dict;
                    _loadLog.Add($"Loaded {dict.Count} opcodes for {gameRegion} from external file: {filePath}");
                }
            }

            // Fall back to embedded resources for any regions not already loaded
            System.Reflection.Assembly assembly = typeof(OpcodeManager).Assembly;
            _loadLog.Add($"Assembly.Location: {assembly.Location}");

            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (!resource.Contains(".Opcodes."))
                    continue;

                string regionString = resource.Substring(resource.IndexOf(".Opcodes.", StringComparison.InvariantCulture) + 9, resource.LastIndexOf('.') - resource.IndexOf(".Opcodes.", StringComparison.InvariantCulture) - 9);
                if (!Enum.TryParse(regionString, out GameRegion gameRegion))
                    continue;

                if (_opcodes.ContainsKey(gameRegion))
                {
                    _loadLog.Add($"Skipped embedded resource for {gameRegion} (already loaded from external file)");
                    continue;
                }

                using (Stream stream = assembly.GetManifestResourceStream(resource))
                {
                    using (StreamReader sr = new StreamReader(stream))
                    {
                        Dictionary<string, ushort> dict = ParseOpcodeData(sr.ReadToEnd());
                        _opcodes.Add(gameRegion, dict);
                        _loadLog.Add($"Loaded {dict.Count} opcodes for {gameRegion} from embedded resource");
                    }
                }
            }

            _loadLog.Add($"Total regions loaded: {_opcodes.Count}");
            WriteLogFile();
        }

        private static string GetOpcodeDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Advanced Combat Tracker",
                "Plugins",
                "Opcodes");
        }

        private void WriteLogFile()
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Advanced Combat Tracker",
                    "Plugins");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "machina_opcodes.log");
                File.WriteAllText(logPath, string.Join(Environment.NewLine, _loadLog));
            }
            catch
            {
                // Ignore write failures - diagnostics should not break the application
            }
        }

        private static Dictionary<string, ushort> ParseOpcodeData(string content)
        {
            string[][] data = content
                .Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries)).ToArray();

            return data.ToDictionary(
                x => x[0].Trim(),
                x => Convert.ToUInt16(x[1].Trim(), 16));
        }
        public void SetRegion(GameRegion region)
        {
            if (!_opcodes.ContainsKey(region))
                region = GameRegion.Global;

            GameRegion = region;
            CurrentOpcodes = _opcodes[GameRegion];

            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Advanced Combat Tracker",
                    "Plugins",
                    "machina_opcodes.log");
                File.AppendAllText(logPath, Environment.NewLine + $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SetRegion: {region}, opcodes count: {CurrentOpcodes.Count}");
            }
            catch { }
        }
    }
}
