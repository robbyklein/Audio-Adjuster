using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using TagLib;
using TagLib.Id3v2;
using UnityEngine;
using File = System.IO.File;
using Debug = UnityEngine.Debug;
using Tag = TagLib.Id3v2.Tag;

namespace RK_Studios.Audio_Adjuster.Editor.Utilities {
    public static class FFmpegUtility {
        private static readonly string BinFolder =
            Path.Combine(Application.dataPath, "RK Studios/Audio Adjuster/Editor/Bin");

        private static string GetExecutablePath() {
            var name = Application.platform switch {
                RuntimePlatform.OSXEditor => "ffmpeg-macos",
                RuntimePlatform.WindowsEditor => "ffmpeg-win.exe",
                RuntimePlatform.LinuxEditor => "ffmpeg-linux",
                _ => throw new PlatformNotSupportedException()
            };
            var path = Path.Combine(BinFolder, name);
            if (!Application.platform.ToString().Contains("Windows") && File.Exists(path)) {
                try {
                    Process.Start(new ProcessStartInfo {
                        FileName = "/bin/chmod",
                        Arguments = $"+x \"{path}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
                catch {
                }
            }

            return path;
        }

        public static string ProcessAudio(string assetPath, bool applyTrim, float volumeMultiplier) {
            var fullPath = Path.IsPathRooted(assetPath)
                ? assetPath
                : Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            fullPath = Path.GetFullPath(fullPath);

            if (!File.Exists(fullPath)) {
                Debug.LogError($"[FFmpeg] Source not found: {fullPath}");
                return null;
            }

            var dir = Path.GetDirectoryName(fullPath);
            var stem = Path.GetFileNameWithoutExtension(fullPath);
            var ext = Path.GetExtension(fullPath);
            var editedStem = stem.EndsWith("_edited") ? stem : $"{stem}_edited";
            var finalPath = Path.Combine(dir, $"{editedStem}{ext}");
            var tempPath = Path.Combine(dir, $"__temp_output{ext}");

            var filters = string.Empty;
            if (applyTrim) {
                filters = "silenceremove=start_periods=1:start_threshold=-50dB:stop_periods=1:stop_threshold=-50dB";
            }

            if (Mathf.Abs(volumeMultiplier - 1f) > 0.001f) {
                var dB = Mathf.Clamp(20f * Mathf.Log10(volumeMultiplier), -60f, 30f)
                    .ToString("0.0", CultureInfo.InvariantCulture);
                filters = string.IsNullOrEmpty(filters) ? $"volume={dB}dB" : $"{filters},volume={dB}dB";
            }

            if (string.IsNullOrEmpty(filters)) {
                filters = "anull";
            }

            var args = $"-y -i \"{fullPath}\" -af \"{filters}\" \"{tempPath}\"";
            if (!RunFFmpeg(args) || !File.Exists(tempPath)) {
                return null;
            }

            WriteMetadata(tempPath, applyTrim, volumeMultiplier);
            File.Copy(tempPath, finalPath, true);
            File.Delete(tempPath);

            AudioFileMetadata.ClearCache(fullPath);
            AudioFileMetadata.ClearCache(finalPath);

            var assetsRoot = Application.dataPath.Replace("\\", "/");
            var rel = finalPath.Replace("\\", "/").Substring(assetsRoot.Length);
            if (rel.StartsWith("/")) {
                rel = rel[1..];
            }

            return "Assets/" + rel;
        }

        private static void WriteMetadata(string path, bool trimmed, float vol) {
            var comment = $"Trimmed:{trimmed}|Volume:{vol:0.00}";
            try {
                using var tf = TagLib.File.Create(path);
                var id3 = (Tag)tf.GetTag(TagTypes.Id3v2, true);
                foreach (var f in id3.GetFrames<CommentsFrame>()) {
                    id3.RemoveFrame(f);
                }

                var frame = CommentsFrame.Get(id3, "eng", string.Empty, true);
                frame.TextEncoding = StringType.UTF8;
                frame.Text = comment;
                id3.Comment = comment;
                tf.Save();
            }
            catch (Exception e) {
                Debug.LogWarning($"[FFmpeg] Tag write failed: {e}");
            }
        }

        private static bool RunFFmpeg(string arguments) {
            var exe = GetExecutablePath();
            if (!File.Exists(exe)) {
                Debug.LogError($"[FFmpeg] Binary missing: {exe}");
                return false;
            }

            try {
                var psi = new ProcessStartInfo {
                    FileName = exe,
                    Arguments = arguments,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                var err = proc!.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0) {
                    Debug.LogError($"[FFmpeg] Exit {proc.ExitCode}\n{err}");
                    return false;
                }

                return true;
            }
            catch (Exception e) {
                Debug.LogError($"[FFmpeg] Could not start process: {e}");
                return false;
            }
        }
    }
}