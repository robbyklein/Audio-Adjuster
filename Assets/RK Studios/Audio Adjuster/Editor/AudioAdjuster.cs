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

            var downloadButton = _emptyBin.Q(className: "empty__button");
            if (downloadButton != null) {
                downloadButton.RegisterCallback<ClickEvent>(_ => OpenDownloadPage());
            }

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
            var binPath = Path.Combine(Application.dataPath, "RK Studios/Audio Adjuster/Editor/Bin");
            return File.Exists(Path.Combine(binPath, "ffmpeg-macos"))
                   || File.Exists(Path.Combine(binPath, "ffmpeg-win.exe"))
                   || File.Exists(Path.Combine(binPath, "ffmpeg-linux"));
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
            var guids = AssetDatabase.FindAssets("t:AudioClip");
            var map = new Dictionary<string, string>();

            foreach (var g in guids) {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (p.StartsWith("Assets/RK Studios/Audio Adjuster")) {
                    continue;
                }

                var dir = Path.GetDirectoryName(p);
                var fn = Path.GetFileNameWithoutExtension(p);
                var isEd = fn.EndsWith("_edited");
                var baseName = isEd ? fn[..^7] : fn;
                var key = $"{dir}/{baseName}";
                if (isEd || !map.ContainsKey(key)) {
                    map[key] = p;
                }
            }

            foreach (var p in map.Values) {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (c) {
                    _audioFiles.Add(c);
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

        private string SrcOf(string path) {
            var d = Path.GetDirectoryName(path);
            var n = Path.GetFileNameWithoutExtension(path);
            var e = Path.GetExtension(path);
            if (n.EndsWith("_edited")) {
                n = n[..^7];
            }

            return Path.Combine(d ?? "", n + e).Replace("\\", "/");
        }

        private void Reprocess(string assetPath, bool trim, float v) {
            var newAsset = FFmpegUtility.ProcessAudio(SrcOf(assetPath), trim, v);
            if (!string.IsNullOrEmpty(newAsset)) {
                AssetDatabase.ImportAsset(newAsset);
                Refresh();
            }
        }

        private static void ToggleHidden(VisualElement cell, bool hide) {
            if (cell == null) {
                return;
            }

            if (hide) {
                cell.AddToClassList(UIBuilders.BottomCellHiddenClass);
            }
            else {
                cell.RemoveFromClassList(UIBuilders.BottomCellHiddenClass);
            }
        }

        private List<float> ExtractWaveformData(AudioClip clip, int resolution) {
            var samples = new float[clip.samples];
            clip.GetData(samples, 0);
            var wf = new List<float>();
            var seg = clip.samples / resolution;

            for (var i = 0; i < resolution; i++) {
                var sum = 0f;
                int s = i * seg, e = Mathf.Min(s + seg, samples.Length);
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

        private void OpenDownloadPage() {
            Application.OpenURL("https://github.com/robbyklein/Audio-Adjuster/releases");
        }
    }
}