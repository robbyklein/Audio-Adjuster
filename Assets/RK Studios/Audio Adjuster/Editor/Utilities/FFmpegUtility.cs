using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using TagLib;
using TagLib.Id3v2;
using UnityEngine;
using Debug = UnityEngine.Debug;
using File = System.IO.File;
using Tag = TagLib.Id3v2.Tag;

namespace RK_Studios.Audio_Adjuster.Editor.Utilities {
    public static class FFmpegUtility {
        private static readonly string BinFolder =
            Path.Combine(Application.dataPath, "RK Studios/Audio Adjuster/Editor/Bin");

        private static string GetExecutablePath() {
            var fileName = Application.platform switch {
                RuntimePlatform.OSXEditor => "ffmpeg-macos",
                RuntimePlatform.WindowsEditor => "ffmpeg-win.exe",
                RuntimePlatform.LinuxEditor => "ffmpeg-linux",
                _ => throw new PlatformNotSupportedException("Unsupported editor platform")
            };
            var fullPath = Path.Combine(BinFolder, fileName);
            if (!Application.platform.ToString().Contains("Windows") && File.Exists(fullPath)) {
                try {
                    Process.Start(new ProcessStartInfo {
                        FileName = "/bin/chmod", Arguments = $"+x \"{fullPath}\"", UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
                catch {
                }
            }

            return fullPath;
        }

        public static string ProcessAudio(string assetPath, bool applyTrim, float volumeMultiplier) {
            var fullPath = Path.Combine(Application.dataPath, assetPath.Replace("Assets/", ""));
            if (!File.Exists(fullPath)) {
                Debug.LogError($"[FFmpeg] Source not found: {fullPath}");
                return null;
            }

            var dir = Path.GetDirectoryName(fullPath);
            var stem = Path.GetFileNameWithoutExtension(fullPath);
            var ext = Path.GetExtension(fullPath);
            var editedStem = stem.EndsWith("_edited") ? stem : stem + "_edited";
            var finalPath = Path.Combine(dir, $"{editedStem}{ext}");
            var tempPath = Path.Combine(dir, $"__temp_output{ext}");

            var filters = string.Empty;
            if (applyTrim) {
                filters += "silenceremove=start_periods=1:start_threshold=-50dB:stop_periods=1:stop_threshold=-50dB";
            }

            if (Mathf.Abs(volumeMultiplier - 1f) > 0.001f) {
                var dB = Mathf.Clamp(20f * Mathf.Log10(volumeMultiplier), -60f, 30f);
                if (filters.Length > 0) {
                    filters += ",";
                }

                filters += $"volume={dB.ToString("0.0", CultureInfo.InvariantCulture)}dB";
            }

            if (filters.Length == 0) {
                filters = "anull";
            }

            var args = $"-y -i \"{fullPath}\" -af \"{filters}\" \"{tempPath}\"";
            if (!RunFFmpeg(args) || !File.Exists(tempPath)) {
                Debug.LogError("[FFmpeg] Failed or produced no output.");
                return null;
            }

            WriteMetadata(tempPath, applyTrim, volumeMultiplier);

            File.Copy(tempPath, finalPath, true);
            File.Delete(tempPath);

            AudioFileMetadata.ClearCache(fullPath);
            AudioFileMetadata.ClearCache(finalPath);

            return "Assets/" + finalPath.Replace(Application.dataPath + Path.DirectorySeparatorChar, "")
                .Replace("\\", "/");
        }

        private static void WriteMetadata(string path, bool trimmed, float vol) {
            var comment = $"Trimmed:{trimmed}|Volume:{vol:0.00}";

            try {
                using var tf = TagLib.File.Create(path);

                // Ensure we have a writable ID3v2 tag
                var id3 = (Tag)tf.GetTag(TagTypes.Id3v2, true);

                // Remove any existing comment frames so we don’t accumulate duplicates
                foreach (var f in id3.GetFrames<CommentsFrame>()) {
                    id3.RemoveFrame(f);
                }

                // Create (or fetch) the single ENG/"" comment frame
                var comm = CommentsFrame.Get(id3, "eng", string.Empty, true);
                comm.TextEncoding = StringType.UTF8;
                comm.Text = comment; // <-- write the payload

                // Optional: keep TagLib’s convenience property in sync
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
                    FileName = exe, Arguments = arguments, RedirectStandardError = true, RedirectStandardOutput = true,
                    UseShellExecute = false, CreateNoWindow = true
                };
                using var proc = Process.Start(psi); // 123 
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