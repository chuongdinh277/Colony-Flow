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
