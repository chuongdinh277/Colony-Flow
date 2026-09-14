using UnityEngine;

namespace ColonyFlow
{
    public enum BoosterType
    {
        Bomb,
        Magnet,
        Shovel,
        Shuffle
    }

    public class BoosterManager : Singleton<BoosterManager>
    {
        private const string PREF_BOOSTER = "Booster_";

        public void OnInit()
        {
            // Initialize any runtime booster logic if needed
        }

        public int GetBoosterCount(BoosterType type)
        {
            return PlayerPrefs.GetInt(PREF_BOOSTER + type.ToString(), 3); // Default 3 for testing
        }

        public void AddBooster(BoosterType type, int amount)
        {
            int current = GetBoosterCount(type);
            PlayerPrefs.SetInt(PREF_BOOSTER + type.ToString(), current + amount);
            PlayerPrefs.Save();
        }

        public bool UseBooster(BoosterType type)
        {
            int current = GetBoosterCount(type);
            if (current > 0)
            {
                PlayerPrefs.SetInt(PREF_BOOSTER + type.ToString(), current - 1);
                PlayerPrefs.Save();
                // TODO: Trigger actual gameplay effect here via GameFlowController or AntRouteService
                Debug.Log($"Used Booster: {type}");
                return true;
            }
            return false;
        }
    }
}
