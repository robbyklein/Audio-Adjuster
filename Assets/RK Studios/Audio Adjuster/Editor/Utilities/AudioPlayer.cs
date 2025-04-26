using UnityEngine;

namespace RK_Studios.Audio_Adjuster.Editor.Utilities {
    public static class AudioPlayer {
        private static AudioSource _previewSource;

        public static void PlayClip(AudioClip clip) {
            if (clip == null) {
                return;
            }

            EnsurePreviewSourceExists();
            _previewSource.clip = clip;
            _previewSource.Play();
        }

        public static void StopClip() {
            if (_previewSource != null && _previewSource.isPlaying) {
                _previewSource.Stop();
            }
        }

        private static void EnsurePreviewSourceExists() {
            if (_previewSource != null) {
                return;
            }

            var go = new GameObject("AudioPreviewSource") { hideFlags = HideFlags.HideAndDontSave };
            _previewSource = go.AddComponent<AudioSource>();
        }
    }
}