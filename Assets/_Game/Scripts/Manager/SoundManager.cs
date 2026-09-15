using UnityEngine;

namespace ColonyFlow
{
    public enum SoundID { BGM_Menu, BGM_Gameplay }
    public enum GameFxID { Win, Lose, BoxCollected, AntSpawn }
    public enum UIFxID { ButtonClick }

    public sealed class SoundManager : Singleton<SoundManager>
    {
        private const string MusicSettingKey = "ColonyFlow.Music";
        private const string FxSettingKey = "ColonyFlow.Fx";

        [Header("BGM")]
        [SerializeField] private AudioClip[] musicClips;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
        [Header("Game FX")]
        [SerializeField] private AudioClip[] gameFxClips;
        [Header("UI FX")]
        [SerializeField] private AudioClip[] uiFxClips;
        [SerializeField, Range(0f, 1f)] private float fxVolume = 1f;

        private AudioSource musicSource;
        private AudioSource fxSource;
        public bool IsMusicOn { get; private set; }
        public bool IsFxOn { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (Ins != this) return;
            musicSource = gameObject.AddComponent<AudioSource>();
            fxSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            fxSource.playOnAwake = false;
            IsMusicOn = PlayerPrefs.GetInt(MusicSettingKey, 1) != 0;
            IsFxOn = PlayerPrefs.GetInt(FxSettingKey, 1) != 0;
            // Đảm bảo có đúng 1 AudioListener trong scene
            if (FindFirstObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            AutoLoadClips();
        }

        /// <summary>
        /// Auto-load AudioClips from Resources/Sound if not assigned in Inspector.
        /// Layout:
        ///   musicClips[0]  = BGM_Menu     -> music.mp3
        ///   musicClips[1]  = BGM_Gameplay -> music.mp3
        ///   gameFxClips[0] = Win          -> PickUp.mp3
        ///   gameFxClips[1] = Lose         -> PickUp.mp3
        ///   gameFxClips[2] = BoxCollected -> PickUp.mp3
        ///   gameFxClips[3] = AntSpawn     -> null (silent)
        ///   uiFxClips[0]   = ButtonClick  -> booster.mp3
        /// </summary>
        private void AutoLoadClips()
        {
            AudioClip musicClip   = Resources.Load<AudioClip>("Sound/music");
            AudioClip pickupClip  = Resources.Load<AudioClip>("Sound/PickUp");
            AudioClip boosterClip = Resources.Load<AudioClip>("Sound/booster");

            // BGM
            if (musicClips == null || musicClips.Length < 2)
                musicClips = new AudioClip[2];
            if (musicClips[0] == null) musicClips[0] = musicClip;
            if (musicClips[1] == null) musicClips[1] = musicClip;

            // Game FX
            if (gameFxClips == null || gameFxClips.Length < 4)
            {
                AudioClip[] old = gameFxClips;
                gameFxClips = new AudioClip[4];
                if (old != null) for (int i = 0; i < Mathf.Min(old.Length, 4); i++) gameFxClips[i] = old[i];
            }
            if (gameFxClips[0] == null) gameFxClips[0] = pickupClip;  // Win
            if (gameFxClips[1] == null) gameFxClips[1] = pickupClip;  // Lose
            if (gameFxClips[2] == null) gameFxClips[2] = pickupClip;  // BoxCollected
            // gameFxClips[3] AntSpawn: null = silent

            // UI FX
            if (uiFxClips == null || uiFxClips.Length < 1)
                uiFxClips = new AudioClip[1];
            if (uiFxClips[0] == null) uiFxClips[0] = boosterClip;     // ButtonClick
        }

        public void PlayMusic(SoundID id)
        {
            int index = (int)id;
            if (!IsMusicOn || musicClips == null || index < 0 || index >= musicClips.Length || musicClips[index] == null)
            {
                musicSource.Stop();
                return;
            }
            AudioClip clip = musicClips[index];
            if (musicSource.isPlaying && musicSource.clip == clip) return;
            musicSource.clip = clip;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }

        public void StopMusic() => musicSource.Stop();
        public void PlayGameFx(GameFxID id) => PlayOneShot(gameFxClips, (int)id);
        public void PlayUIFx(UIFxID id) => PlayOneShot(uiFxClips, (int)id);

        public void SetMusicEnabled(bool enabled)
        {
            IsMusicOn = enabled;
            PlayerPrefs.SetInt(MusicSettingKey, enabled ? 1 : 0);
            if (!enabled) musicSource.Stop();
            else PlayMusic(SoundID.BGM_Gameplay);
        }

        public void SetFxEnabled(bool enabled)
        {
            IsFxOn = enabled;
            PlayerPrefs.SetInt(FxSettingKey, enabled ? 1 : 0);
        }

        private void PlayOneShot(AudioClip[] clips, int index)
        {
            if (!IsFxOn || clips == null || index < 0 || index >= clips.Length || clips[index] == null) return;
            fxSource.PlayOneShot(clips[index], fxVolume);
        }
    }
}
