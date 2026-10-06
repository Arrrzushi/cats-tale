using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// All PawTown sounds in one place: music + birds ambience (2D), and pooled one-shots (2D or 3D).
    /// Other scripts call PawAudio.Instance?.Play...(). Mute with SetMuted (the SOUND button does this).
    /// </summary>
    public class PawAudio : MonoBehaviour
    {
        public static PawAudio Instance { get; private set; }

        [Header("Loops")]
        public AudioClip music;
        public AudioClip ambience;
        [Range(0, 1)] public float musicVolume = 0.32f;
        [Range(0, 1)] public float ambienceVolume = 0.5f;

        [Header("Pet")]
        public AudioClip[] steps;
        public AudioClip jump, land;
        [Tooltip("Picked at random each meow, never the same one twice in a row")] public AudioClip[] meows;

        [Header("Town")]
        public AudioClip engineLoop, horn, trainLoop, trainHorn, crossingBell, pedBeep;

        [Header("UI")]
        public AudioClip click, success;

        [Header("Mix")]
        [Range(0, 1)] public float sfxVolume = 1f;

        AudioSource musicSrc, ambSrc;
        readonly List<AudioSource> pool = new List<AudioSource>();
        int next;
        const string MuteKey = "PawTown.Muted";

        public bool Muted { get; private set; }

        void Awake()
        {
            Instance = this;
            musicSrc = Loop2D("Music", music, musicVolume);
            if (musicSrc) musicSrc.ignoreListenerPause = true;      // menus keep their music; the town goes quiet
            ambSrc = Loop2D("Ambience", ambience, ambienceVolume);
            for (int i = 0; i < 16; i++)
            {
                var go = new GameObject("OneShot" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.ignoreListenerPause = true;                       // UI clicks / success jingles work in menus
                pool.Add(s);
            }
            bool m = false;
            try { m = PlayerPrefs.GetInt(MuteKey, 0) == 1; } catch { }
            SetMuted(m);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        AudioSource Loop2D(string n, AudioClip c, float vol)
        {
            if (!c) return null;
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = c; s.loop = true; s.volume = vol; s.spatialBlend = 0f; s.playOnAwake = false;
            s.Play();
            return s;
        }

        public void SetMuted(bool m)
        {
            Muted = m;
            AudioListener.volume = m ? 0f : 1f;
            try { PlayerPrefs.SetInt(MuteKey, m ? 1 : 0); } catch { }
        }

        public void ToggleMute() => SetMuted(!Muted);

        /// <summary>Settings sliders: music (music + ambience) and sound effects, 0..1.</summary>
        public void SetVolumes(float music01, float sfx01)
        {
            if (musicSrc) musicSrc.volume = musicVolume * music01;
            if (ambSrc) ambSrc.volume = ambienceVolume * music01;
            sfxVolume = sfx01;
        }

        /// <summary>One-shot. position == null plays 2D (UI / the pet), otherwise 3D at that spot.</summary>
        public void Play(AudioClip c, Vector3? position = null, float volume = 1f, float pitch = 1f, float maxDistance = 30f)
        {
            if (!c) return;
            if (position.HasValue && AudioListener.pause) return;   // no town sounds (horns...) while a menu is open
            var s = pool[next];
            next = (next + 1) % pool.Count;
            s.Stop();
            s.clip = c;
            s.volume = volume * sfxVolume;
            s.pitch = pitch;
            if (position.HasValue)
            {
                s.transform.position = position.Value;
                Make3D(s, maxDistance);
            }
            else s.spatialBlend = 0f;
            s.Play();
        }

        public static void Make3D(AudioSource s, float maxDistance, float minDistance = 3f)
        {
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
        }

        /// <summary>Adds (or returns) a looping 3D source on a GameObject, e.g. a car engine.</summary>
        public static AudioSource LoopOn(GameObject go, AudioClip clip, float volume, float maxDistance, float minDistance = 3f)
        {
            if (!clip) return null;
            var s = go.GetComponent<AudioSource>();
            if (s == null) s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true; s.volume = volume; s.playOnAwake = false;
            Make3D(s, maxDistance, minDistance);
            s.time = Random.value * clip.length;   // so engines are not in phase
            s.Play();
            return s;
        }

        // ---- convenience
        public void Step(float volume) { if (steps != null && steps.Length > 0) Play(steps[Random.Range(0, steps.Length)], null, volume, Random.Range(0.92f, 1.08f)); }
        public void Jump() => Play(jump, null, 0.55f, Random.Range(0.95f, 1.05f));
        public void Land() => Play(land, null, 0.6f);

        // swimming (Resources/Audio, synthesised water sounds)
        AudioClip[] swim; AudioClip splash;
        public void Swim()
        {
            if (swim == null) swim = new[] { Resources.Load<AudioClip>("Audio/sfx_swim_01"), Resources.Load<AudioClip>("Audio/sfx_swim_02"), Resources.Load<AudioClip>("Audio/sfx_swim_03") };
            var c = swim[Random.Range(0, swim.Length)];
            if (c) Play(c, null, 0.5f, Random.Range(0.9f, 1.1f));
        }
        /// <summary>Rule broken: a short car horn, played flat (not from the town) so it is heard.</summary>
        public void Oops() => Play(horn, null, 0.45f, 1.15f);

        public void Splash()
        {
            if (!splash) splash = Resources.Load<AudioClip>("Audio/sfx_splash");
            if (splash) Play(splash, null, 0.75f, Random.Range(0.95f, 1.05f));
        }
        int lastMeow = -1;

        public void Meow()
        {
            if (meows == null || meows.Length == 0) return;
            int i = Random.Range(0, meows.Length);
            if (meows.Length > 1 && i == lastMeow) i = (i + 1 + Random.Range(0, meows.Length - 1)) % meows.Length;
            lastMeow = i;
            Play(meows[i], null, 0.8f, Random.Range(0.93f, 1.1f));
        }
        public void Click() => Play(click, null, 0.6f);
        public void Success() => Play(success, null, 0.8f);
    }
}
