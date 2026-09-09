using UnityEngine;
using System.Collections;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// Manages the game's background music (BGM) and audio loop systems.
    /// Uses DontDestroyOnLoad to persist across scenes (Main Menu -> Gameplay) seamlessly.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    // Create persistent Audio Manager if not present
                    GameObject go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private AudioSource musicSource;
        private AudioSource soundEffectSource;
        private AudioClip hexHoverClip;
        private AudioClip tileSnapClip;
        private AudioClip placeClickClip;
        private Coroutine fadeCoroutine;
        private float targetVolume = 0.5f; // Music master (0–1)
        private float sfxVolume = 0.85f;   // SFX master (0–1)
        private bool setupComplete;

        private const string PrefMusic = "tt_music_volume";
        private const string PrefSfx = "tt_sfx_volume";

        public float MusicVolume => targetVolume;
        public float SfxVolume => sfxVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnStartup()
        {
            // Forces AudioManager instantiation and music playback immediately on startup
            var activeInstance = Instance;
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                SetupAudio();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void SetupAudio()
        {
            targetVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMusic, 0.5f));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefSfx, 0.85f));

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f; // 2D Stereo sound
                musicSource.volume = 0f; // Start at 0 for smooth fade-in
            }

            if (soundEffectSource == null)
            {
                soundEffectSource = gameObject.AddComponent<AudioSource>();
                soundEffectSource.playOnAwake = false;
                soundEffectSource.spatialBlend = 0f;
            }
            soundEffectSource.volume = sfxVolume;

            EnsureSfxClips();

            if (!setupComplete)
            {
                AudioClip bgm = Resources.Load<AudioClip>("Audio/Music/AtmosphericSoundtrack");
                if (bgm != null)
                {
                    musicSource.clip = bgm;
                    musicSource.Play();
                    FadeTo(targetVolume, 3f);
                    Debug.Log("[AudioManager] Atmospheric BGM started playing and fading in.");
                }
                else
                {
                    Debug.LogError("[AudioManager] Could not load BGM clip from Resources path 'Audio/Music/AtmosphericSoundtrack'.");
                }
                setupComplete = true;
            }
            else
            {
                ApplyMusicVolumeImmediate(targetVolume);
                if (soundEffectSource != null) soundEffectSource.volume = sfxVolume;
            }
        }

        /// <summary>Master music volume 0–1 (persisted). Applies immediately.</summary>
        public void SetMusicVolume(float volume01)
        {
            targetVolume = Mathf.Clamp01(volume01);
            PlayerPrefs.SetFloat(PrefMusic, targetVolume);
            PlayerPrefs.Save();
            ApplyMusicVolumeImmediate(targetVolume);
        }

        /// <summary>Master SFX volume 0–1 (persisted).</summary>
        public void SetSfxVolume(float volume01)
        {
            sfxVolume = Mathf.Clamp01(volume01);
            PlayerPrefs.SetFloat(PrefSfx, sfxVolume);
            PlayerPrefs.Save();
            EnsureSfxReady();
            if (soundEffectSource != null) soundEffectSource.volume = sfxVolume;
        }

        private void ApplyMusicVolumeImmediate(float volume)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
            if (musicSource != null) musicSource.volume = volume;
        }

        private void EnsureSfxClips()
        {
            if (hexHoverClip == null) hexHoverClip = CreateHexHoverClip();
            if (tileSnapClip == null) tileSnapClip = CreateTileSnapClip();
            if (placeClickClip == null) placeClickClip = CreatePlaceClickClip();
        }

        private void EnsureSfxReady()
        {
            if (soundEffectSource == null || hexHoverClip == null || tileSnapClip == null || placeClickClip == null)
                SetupAudio();
            else
                EnsureSfxClips();
        }

        /// <summary>
        /// Transitions the background music volume smoothly to a target level.
        /// </summary>
        public void FadeTo(float volume, float duration)
        {
            targetVolume = Mathf.Clamp01(volume);
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            if (duration <= 0.01f || musicSource == null)
            {
                ApplyMusicVolumeImmediate(targetVolume);
                return;
            }
            fadeCoroutine = StartCoroutine(FadeVolumeCoroutine(targetVolume, duration));
        }

        private IEnumerator FadeVolumeCoroutine(float target, float duration)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, target, elapsed / duration);
                yield return null;
            }

            musicSource.volume = target;
            fadeCoroutine = null;
        }

        /// <summary>
        /// Pause background music.
        /// </summary>
        public void PauseMusic()
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Pause();
            }
        }

        /// <summary>
        /// Resume background music.
        /// </summary>
        public void ResumeMusic()
        {
            if (musicSource != null && !musicSource.isPlaying)
            {
                musicSource.UnPause();
            }
        }

        public void PlayHexHoverSound()
        {
            PlaySfx(hexHoverClip, 0.7f);
        }

        /// <summary>Soft tick when the tile ghost snaps to a new cell.</summary>
        public void PlayTileSnapSound(bool joining = false)
        {
            PlaySfx(tileSnapClip, joining ? 1.1f : 0.75f);
        }

        /// <summary>Click when the player confirms a card tile placement.</summary>
        public void PlayPlaceClickSound()
        {
            PlaySfx(placeClickClip, 1.2f);
        }

        private void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            EnsureSfxReady();
            if (soundEffectSource == null || clip == null) return;
            soundEffectSource.PlayOneShot(clip, volumeScale);
        }

        private static AudioClip CreateHexHoverClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.08f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                float time = sampleIndex / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 28f);
                samples[sampleIndex] = (Mathf.Sin(2f * Mathf.PI * 880f * time) +
                    0.35f * Mathf.Sin(2f * Mathf.PI * 1320f * time)) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create("HexHover", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTileSnapClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.07f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 36f);
                samples[i] = (Mathf.Sin(2f * Mathf.PI * 620f * time) +
                    0.45f * Mathf.Sin(2f * Mathf.PI * 930f * time)) * envelope * 0.45f;
            }

            AudioClip clip = AudioClip.Create("TileSnap", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreatePlaceClickClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.1f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float clickEnv = Mathf.Exp(-time * 48f);
                float bodyEnv = Mathf.Exp(-time * 16f);
                float click = Mathf.Sin(2f * Mathf.PI * 1600f * time) * clickEnv;
                float body = Mathf.Sin(2f * Mathf.PI * 240f * time) * bodyEnv * 0.6f;
                samples[i] = (click + body) * 0.5f;
            }

            AudioClip clip = AudioClip.Create("PlaceClick", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
