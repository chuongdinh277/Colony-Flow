using UnityEngine;

namespace ColonyFlow.UI
{
    [CreateAssetMenu(menuName = "Colony Flow/UI Visual Library")]
    public sealed class UIVisualLibrary : ScriptableObject
    {
        private static UIVisualLibrary current;
        public static UIVisualLibrary Current => current != null ? current : current = Resources.Load<UIVisualLibrary>("UI/UIVisualLibrary");

        [Header("Icons")]
        public Sprite pause, speed, settings, plus, avatar;
        public Sprite boosterAdd, boosterShuffle, boosterVacuum, boosterFan, hint;
        public Sprite ranking, shop, tasks, rewards, cube;
        public Sprite heart, retry, home, sound, music;
        public Sprite vibration, theme, close, loadingTip, padlock;

        [Header("Panels")]
        public Sprite playButton, blueButton, speedButton, boosterButton, boosterBar;
        public Sprite popupPanel, popupHeader, cyanButton, redButton, toggles;
    }
}
