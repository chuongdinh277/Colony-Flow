using UnityEngine;

namespace ColonyFlow
{
    public class DataManager : Singleton<DataManager>
    {
        private const string PREF_COINS = "Coins";
        private const string PREF_LIVES = "Lives";
        private const string PREF_CUR_LEVEL = "CurrentLevel";

        public int Coins
        {
            get => PlayerPrefs.GetInt(PREF_COINS, 0);
            set { PlayerPrefs.SetInt(PREF_COINS, value); PlayerPrefs.Save(); }
        }

        public int Lives
        {
            get => PlayerPrefs.GetInt(PREF_LIVES, 5);
            set { PlayerPrefs.SetInt(PREF_LIVES, value); PlayerPrefs.Save(); }
        }

        public int CurrentLevel
        {
            get => PlayerPrefs.GetInt(PREF_CUR_LEVEL, 1);
            set { PlayerPrefs.SetInt(PREF_CUR_LEVEL, value); PlayerPrefs.Save(); }
        }

        // You can add more data fields here (e.g., Settings, HighScores)
        public void AddCoins(int amount) => Coins += amount;
        public void ConsumeLife() => Lives = Mathf.Max(0, Lives - 1);
    }
}
