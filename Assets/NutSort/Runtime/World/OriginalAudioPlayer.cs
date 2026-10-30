using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NutSort.World
{
    public sealed class OriginalAudioPlayer : MonoBehaviour
    {
        [SerializeField] private OriginalAudioSettings settings;
        [SerializeField] private OriginalUserSession userState;
        [SerializeField] private AudioSource background;
        [SerializeField] private AudioSource voicePrefab;
        private sealed class ClipEntry { public AudioClip Clip; public string ObjectName; }
        private struct Voice { public AudioSource Source; public int StartFrame; }
        private readonly Dictionary<string, ClipEntry> clips = new Dictionary<string, ClipEntry>(StringComparer.Ordinal);
        private readonly Stack<AudioSource> available = new Stack<AudioSource>();
        private readonly List<Voice> active = new List<Voice>();
        private OriginalLevelView level;
        private bool initialized;
        private int vibrationFrame = -1;
        public bool AudioEnabled => userState.Data.IsAudio;
        public bool VibrationEnabled => userState.Data.IsVibrate;
        public OriginalUserSession UserState => userState;
        public AudioSource Background => background;
        public int ActiveVoiceCount => active.Count;
        public int CachedClipCount => clips.Count;
        public event Action<string> SoundStarted;
        public event Action VibrationRequested;

        private void Awake() { Initialize(); }
        public void Initialize()
        {
            if (initialized) return;
            if (settings == null || userState == null || background == null || voicePrefab == null)
                throw new InvalidOperationException("Original audio prefab references are incomplete.");
            userState.Initialize();
            initialized = true;
        }

        public void Bind(OriginalLevelView value)
        {
            Unbind(); level = value;
            level.SelectionSoundRequested += PlaySelection;
            level.MoveSoundRequested += PlayMove;
        }
        public void Unbind()
        {
            if (level == null) return;
            level.SelectionSoundRequested -= PlaySelection;
            level.MoveSoundRequested -= PlayMove;
            level = null;
        }
        private void PlaySelection() { PlaySound(settings.SelectName); PlayVibration(); }
        private void PlayMove(int count) { PlaySound(string.Format(CultureInfo.InvariantCulture, settings.MoveNameFormat, count)); PlayVibration(); }

        public void SetVibrationEnabled(bool value)
        {
            Initialize();
            userState.Data.IsVibrate = value;
        }

        public void PlayVibration()
        {
            Initialize();
            if (!VibrationEnabled || vibrationFrame == Time.frameCount) return;
            // Selection and transfer can be emitted together by one operation.
            // One platform request gives that action a single haptic response.
            vibrationFrame = Time.frameCount;
            VibrationRequested?.Invoke();
            Handheld.Vibrate();
        }

        private ClipEntry GetClip(string name)
        {
            if (clips.TryGetValue(name, out ClipEntry entry)) return entry;
            string path = settings.ResourcePrefix + name;
            entry = new ClipEntry { Clip = Resources.Load<AudioClip>(path), ObjectName = "audio_" + name };
            clips.Add(name, entry);
            if (entry.Clip == null) Debug.LogError("GetTextAsset not find path: " + path);
            return entry;
        }

        public AudioSource PlaySound(string name)
        {
            Initialize();
            if (!AudioEnabled) return null;
            ClipEntry entry = GetClip(name);
            if (entry.Clip == null) return null;
            AudioSource source = null;
            while (available.Count > 0 && source == null) source = available.Pop();
            if (source == null) source = Instantiate(voicePrefab, transform, false);
            source.gameObject.name = entry.ObjectName;
            source.gameObject.SetActive(true);
            source.clip = entry.Clip;
            source.Play();
            active.Add(new Voice { Source = source, StartFrame = Time.frameCount });
            SoundStarted?.Invoke(name);
            return source;
        }

        public void PlaySound(string name, float delay) { StartCoroutine(PlayDelayed(name, delay)); }
        private IEnumerator PlayDelayed(string name, float delay)
        {
            yield return new WaitForSeconds(delay);
            PlaySound(name); // Check the audio setting when the delay expires.
        }

        public void PlayBgm()
        {
            Initialize();
            if (!AudioEnabled) return;
            if (background.clip == null) background.clip = GetClip(settings.BgmName).Clip;
            background.Play();
        }
        public void SetAudioEnabled(bool value)
        {
            Initialize(); userState.Data.IsAudio = value;
            if (value) PlayBgm(); else background.Pause();
            // Source leaves already-playing short effects alone.
        }

        private void LateUpdate()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Voice voice = active[i];
                if (Time.frameCount <= voice.StartFrame || voice.Source.isPlaying) continue;
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Source.gameObject.SetActive(false);
                available.Push(voice.Source);
                active.RemoveAt(i);
            }
        }
        private void OnDestroy() { Unbind(); }
    }
}
