using System;
using System.Collections.Generic;
using System.IO;
using RK_Studios.Audio_Adjuster.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RK_Studios.Audio_Adjuster.Editor {
    public class AudioAdjuster : EditorWindow {
        private const string UIPath = "Assets/RK Studios/Audio Adjuster/Editor/UI";
        private const int GraphSegments = 120;

        private readonly List<AudioClip> _audioFiles = new();
        private VisualElement _rows;
        private VisualElement _refresh;
        private VisualElement _emptySfx;
        private VisualElement _emptyBin;

        [MenuItem("Tools/Audio Adjuster")]
        public static void ShowWindow() {
            var wnd = GetWindow<AudioAdjuster>();
            wnd.titleContent = new GUIContent("Audio Adjuster");
            wnd.minSize = new Vector2(800, 600);
            wnd.maxSize = new Vector2(800, 600);
        }

        public void CreateGUI() {
            var view = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UIPath}/home.uxml");
            if (view == null) {
                return;
            }

            rootVisualElement.Add(view.Instantiate());

            _rows = rootVisualElement.Q(className: "rows");
            _refresh = rootVisualElement.Q(className: "header__refresh");
            _emptySfx = rootVisualElement.Q(className: "empty--sfx");
            _emptyBin = rootVisualElement.Q(className: "empty--bin");

            _refresh.RegisterCallback<ClickEvent>(_ => Refresh());
            _emptyBin.Q(className: "empty__button")
                ?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(
                    "https://github.com/robbyklein/Audio-Adjuster/releases"));

            Refresh();
        }

        private void Refresh() {
            _audioFiles.Clear();
            _rows.Clear();

            if (!HasFFmpegBinary()) {
                ShowMissingBin();
                return;
            }

            LoadAudioFiles();
            if (_audioFiles.Count == 0) {
                ShowMissingSfx();
            }
            else {
                ShowRows();
            }
        }

        private bool HasFFmpegBinary() {
            var bin = Path.Combine(Application.dataPath, "RK Studios/Audio Adjuster/Editor/Bin");
            return File.Exists(Path.Combine(bin, "ffmpeg-macos"))
                   || File.Exists(Path.Combine(bin, "ffmpeg-win.exe"))
                   || File.Exists(Path.Combine(bin, "ffmpeg-linux"));
        }

        private void ShowMissingBin() {
            _emptyBin.RemoveFromClassList("empty--hidden");
            _emptySfx.AddToClassList("empty--hidden");
        }

        private void ShowMissingSfx() {
            _emptySfx.RemoveFromClassList("empty--hidden");
            _emptyBin.AddToClassList("empty--hidden");
        }

        private void ShowRows() {
            _emptySfx.AddToClassList("empty--hidden");
            _emptyBin.AddToClassList("empty--hidden");
            CreateAudioRows();
        }

        private void LoadAudioFiles() {
            var map = Application.platform == RuntimePlatform.WindowsEditor
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>();

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip")) {
                var p = AssetDatabase.GUIDToAssetPath(guid).Replace("\\", "/");
                if (p.StartsWith("Assets/RK Studios/Audio Adjuster")) {
                    continue;
                }

                var dir = Path.GetDirectoryName(p)?.Replace("\\", "/");
                var fn = Path.GetFileNameWithoutExtension(p);
                var isEd = fn.EndsWith("_edited");
                var baseName = isEd ? fn[..^7] : fn;
                var key = $"{dir}/{baseName}";
                if (isEd || !map.ContainsKey(key)) {
                    map[key] = p;
                }
            }

            foreach (var path in map.Values) {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip) {
                    _audioFiles.Add(clip);
                }
            }
        }

        private void CreateAudioRows() {
            foreach (var clip in _audioFiles) {
                var assetPath = AssetDatabase.GetAssetPath(clip);
                var (isTrimmed, vol) = AudioFileMetadata.ReadMetadata(assetPath);

                var row = UIBuilders.Row(clip, ExtractWaveformData(clip, GraphSegments));
                _rows.Add(row);

                var playBtn = row.Q(className: "row-bottom__play-btn");
                var trimBtn = row.Q(className: "row-bottom__trim-btn");
                var undoBtn = row.Q(className: "row-bottom__undo-btn");
                var plusBtn = row.Q(className: "volume-adjustor__plus-btn");
                var minusBtn = row.Q(className: "volume-adjustor__minus-btn");
                var volLbl = row.Q<Label>(className: "volume-adjustor__label");

                if (volLbl != null) {
                    volLbl.text = Mathf.RoundToInt(vol * 100f) + "%";
                }

                ToggleHidden(trimBtn?.parent, isTrimmed);
                ToggleHidden(undoBtn?.parent, !isTrimmed);

                playBtn?.RegisterCallback<ClickEvent>(_ => AudioPlayer.PlayClip(clip));
                trimBtn?.RegisterCallback<ClickEvent>(_ => Reprocess(assetPath, true, vol));
                undoBtn?.RegisterCallback<ClickEvent>(_ => Reprocess(assetPath, false, vol));

                plusBtn?.RegisterCallback<ClickEvent>(_ => {
                    vol = Mathf.Min(3f, vol + 0.1f);
                    if (volLbl != null) {
                        volLbl.text = Mathf.RoundToInt(vol * 100f) + "%";
                    }

                    Reprocess(assetPath, isTrimmed, vol);
                });

                minusBtn?.RegisterCallback<ClickEvent>(_ => {
                    vol = Mathf.Max(0f, vol - 0.1f);
                    if (volLbl != null) {
                        volLbl.text = Mathf.RoundToInt(vol * 100f) + "%";
                    }

                    Reprocess(assetPath, isTrimmed, vol);
                });
            }
        }

        private void Reprocess(string assetPath, bool trim, float vol) {
            var src = SrcOf(assetPath);
            var newAsset = FFmpegUtility.ProcessAudio(src, trim, vol);
            if (!string.IsNullOrEmpty(newAsset)) {
                AssetDatabase.ImportAsset(newAsset);
                Refresh();
            }
        }

        private static string SrcOf(string path) {
            var dir = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var n = Path.GetFileNameWithoutExtension(path);
            var e = Path.GetExtension(path);
            if (n.EndsWith("_edited")) {
                n = n[..^7];
            }

            return $"{dir}/{n}{e}".Replace("\\", "/");
        }

        private static void ToggleHidden(VisualElement ve, bool hide) {
            if (ve == null) {
                return;
            }

            if (hide) {
                ve.AddToClassList(UIBuilders.BottomCellHiddenClass);
            }
            else {
                ve.RemoveFromClassList(UIBuilders.BottomCellHiddenClass);
            }
        }

        private static List<float> ExtractWaveformData(AudioClip clip, int resolution) {
            var samples = new float[clip.samples];
            clip.GetData(samples, 0);
            var seg = clip.samples / resolution;
            var wf = new List<float>();

            for (var i = 0; i < resolution; i++) {
                var sum = 0f;
                var s = i * seg;
                var e = Mathf.Min(s + seg, samples.Length);
                for (var j = s; j < e; j++) {
                    sum += Mathf.Abs(samples[j]);
                }

                wf.Add(sum / (e - s));
            }

            var max = Mathf.Max(0.001f, Mathf.Max(wf.ToArray()));
            for (var i = 0; i < wf.Count; i++) {
                wf[i] /= max;
            }

            return wf;
        }
    }
}