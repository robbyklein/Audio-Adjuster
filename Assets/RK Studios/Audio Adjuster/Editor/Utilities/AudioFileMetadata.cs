using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TagLib;
using TagLib.Id3v2;
using UnityEngine;
using Debug = UnityEngine.Debug;
using File = System.IO.File;
using Tag = TagLib.Id3v2.Tag;

namespace RK_Studios.Audio_Adjuster.Editor.Utilities {
    public static class AudioFileMetadata {
        private static readonly Dictionary<string, (bool trimmed, float vol)> Cache = new();

        public static (bool trimmed, float volume) ReadMetadata(string assetPath) {
            if (Cache.TryGetValue(assetPath, out var cached)) {
                return cached;
            }

            var full = Path.Combine(Application.dataPath, assetPath.Replace("Assets/", ""));
            if (!File.Exists(full)) {
                Debug.LogWarning($"[Meta] file not found: {full}");
                return (false, 1f);
            }

            string aggregated = null;

            try {
                using var tf = TagLib.File.Create(full);

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var sb = new StringBuilder();

                if (!string.IsNullOrEmpty(tf.Tag.Comment) && seen.Add(tf.Tag.Comment)) {
                    sb.Append(tf.Tag.Comment).Append('|');
                }

                if (tf.TagTypes.HasFlag(TagTypes.Id3v2)) {
                    var id3 = (Tag)tf.GetTag(TagTypes.Id3v2);
                    foreach (var frame in id3.GetFrames()) {
                        switch (frame) {
                            case CommentsFrame cf when !string.IsNullOrEmpty(cf.Text):
                                if (seen.Add(cf.Text)) {
                                    sb.Append(cf.Text).Append('|');
                                }

                                break;

                            case UserTextInformationFrame ux
                                when ux.Description.Equals("comment", StringComparison.OrdinalIgnoreCase)
                                     && ux.Text is { Length: > 0 }:
                                var joined = string.Join("|", ux.Text);
                                if (seen.Add(joined)) {
                                    sb.Append(joined).Append('|');
                                }

                                break;
                        }
                    }
                }

                aggregated = sb.ToString().TrimEnd('|');
            }
            catch (Exception ex) {
                Debug.LogError($"[Meta] TagLib exception: {ex}");
            }

            var trimmed = aggregated?.IndexOf("Trimmed:true", StringComparison.OrdinalIgnoreCase) >= 0;

            var vol = 1f;
            var idx = aggregated?.IndexOf("Volume:", StringComparison.OrdinalIgnoreCase) ?? -1;
            if (idx >= 0) {
                var slice = aggregated.Substring(idx + 7)
                    .Split('|', ';', '\n', '\r', '\0')[0]
                    .Trim()
                    .Replace(',', '.');

                if (!float.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out vol)) {
                    vol = 1f;
                }

                vol = Mathf.Clamp(vol, 0f, 3f);
            }

            Cache[assetPath] = (trimmed, vol);
            return (trimmed, vol);
        }


        public static void ClearCache(string path) {
            var key = path.StartsWith("Assets/")
                ? path
                : "Assets/" + path.Replace(Application.dataPath + Path.DirectorySeparatorChar, "").Replace("\\", "/");
            Cache.Remove(key);
        }
    }
}