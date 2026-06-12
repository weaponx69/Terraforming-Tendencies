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
        private Coroutine fadeCoroutine;
        private float targetVolume = 0.5f; // Set a default pleasant background volume

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
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f; // 2D Stereo sound
            musicSource.volume = 0f; // Start at 0 for smooth fade-in

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
        }

        /// <summary>
        /// Transitions the background music volume smoothly to a target level.
        /// </summary>
        public void FadeTo(float volume, float duration)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeVolumeCoroutine(volume, duration));
        }

        private IEnumerator FadeVolumeCoroutine(float target, float duration)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
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
    }
}
