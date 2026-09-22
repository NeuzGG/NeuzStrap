using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NeuzStrap.Core;

namespace NeuzStrap.Roblox
{
    /// <summary>
    /// Copies files from the Modifications folder over the Roblox install (same folder layout as Roblox),
    /// plus the built-in custom font mod. Original files are backed up and restored when a mod is removed.
    /// </summary>
    public static class ModManager
    {
        const string ManifestName = ".neuzstrap-mods.txt";
        const string BackupFolder = ".neuzstrap-backup";
        public const string CustomFontAsset = "NeuzCustomFont.ttf";

        class ModFile
        {
            public string RelativePath;
            public string SourcePath;   // copy this file...
            public byte[] Content;      // ...or write these bytes
        }

        public static int Apply(string versionDir, Settings s)
        {
            string manifestPath = Path.Combine(versionDir, ManifestName);
            var previous = File.Exists(manifestPath)
                ? new HashSet<string>(File.ReadAllLines(manifestPath).Where(l => l.Length > 0), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var desired = CollectUserMods();
            if (s.UseCustomFont && File.Exists(Paths.CustomFontFile))
                desired.AddRange(BuildFontMod(versionDir));

            // later entries win (e.g. the font mod over a user file with the same path)
            var byPath = new Dictionary<string, ModFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in desired) byPath[m.RelativePath] = m;

            foreach (var rel in previous.Where(p => !byPath.ContainsKey(p)).ToList())
                Restore(versionDir, rel);

            int applied = 0;
            foreach (var m in byPath.Values)
            {
                try
                {
                    string target = Paths.SafeCombine(versionDir, m.RelativePath);
                    string backup = Paths.SafeCombine(Path.Combine(versionDir, BackupFolder), m.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));

                    if (File.Exists(target) && !previous.Contains(m.RelativePath) && !File.Exists(backup))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        File.Copy(target, backup);
                    }

                    if (m.Content != null) File.WriteAllBytes(target, m.Content);
                    else File.Copy(m.SourcePath, target, true);
                    applied++;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Mods", $"Couldn't apply {m.RelativePath}: {ex.Message}");
                }
            }

            File.WriteAllLines(manifestPath, byPath.Keys);
            if (applied > 0 || previous.Count > 0)
                Logger.Info("Mods", $"{applied} mod file(s) applied, {previous.Count(p => !byPath.ContainsKey(p))} removed");
            return applied;
        }

        static void Restore(string versionDir, string rel)
        {
            try
            {
                string target = Paths.SafeCombine(versionDir, rel);
                string backup = Paths.SafeCombine(Path.Combine(versionDir, BackupFolder), rel);
                if (File.Exists(backup))
                {
                    File.Copy(backup, target, true);
                    File.Delete(backup);
                }
                else if (File.Exists(target))
                {
                    File.Delete(target); // it was a brand-new file added by a mod
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Mods", $"Couldn't restore {rel}: {ex.Message}");
            }
        }

        static List<ModFile> CollectUserMods()
        {
            var list = new List<ModFile>();
            if (!Directory.Exists(Paths.Modifications)) return list;

            foreach (var file in Directory.EnumerateFiles(Paths.Modifications, "*", SearchOption.AllDirectories))
            {
                string rel = Paths.GetRelativePath(Paths.Modifications, file);
                string name = Path.GetFileName(rel);
                if (name.StartsWith(".", StringComparison.Ordinal) || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                // FastFlags are managed by NeuzStrap itself (use the FastFlags page or import)
                if (rel.Equals(@"ClientSettings\ClientAppSettings.json", StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new ModFile { RelativePath = rel, SourcePath = file });
            }
            return list;
        }

        /// <summary>Points every font family at the custom font (skips the CJK fallback so those characters still render).</summary>
        static IEnumerable<ModFile> BuildFontMod(string versionDir)
        {
            yield return new ModFile { RelativePath = @"content\fonts\" + CustomFontAsset, SourcePath = Paths.CustomFontFile };

            string families = Path.Combine(versionDir, "content", "fonts", "families");
            if (!Directory.Exists(families)) yield break;

            string backupFamilies = Path.Combine(versionDir, BackupFolder, "content", "fonts", "families");
            foreach (var file in Directory.GetFiles(families, "*.json"))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.IndexOf("CJK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fileName.IndexOf("Fallback", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fileName.IndexOf("Emoji", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                // always start from the original file if we've backed it up
                string source = File.Exists(Path.Combine(backupFamilies, fileName)) ? Path.Combine(backupFamilies, fileName) : file;
                byte[] content;
                try
                {
                    var json = Json.Parse(File.ReadAllText(source, Encoding.UTF8));
                    if (Json.Get(json, "faces") is List<object> faces)
                        foreach (var face in faces.OfType<Dictionary<string, object>>())
                            face["assetId"] = "rbxasset://fonts/" + CustomFontAsset;
                    content = new UTF8Encoding(false).GetBytes(Json.Serialize(json));
                }
                catch (Exception ex)
                {
                    Logger.Warn("Mods", $"Skipping font family {fileName}: {ex.Message}");
                    continue;
                }
                yield return new ModFile { RelativePath = @"content\fonts\families\" + fileName, Content = content };
            }
        }

        public static int CountUserMods() => CollectUserMods().Count;
    }
}
