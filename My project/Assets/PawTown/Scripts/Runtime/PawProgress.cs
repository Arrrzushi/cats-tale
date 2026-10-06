using UnityEngine;

namespace PawTown
{
    /// <summary>Player level / XP / currencies, saved in PlayerPrefs. Paw Coins are earned; Golden Fish are premium.</summary>
    public static class PawProgress
    {
        const string K = "PawTown.Progress.";

        public static int Level { get => Get("Level", 1); private set => Set("Level", value); }
        public static int Xp { get => Get("Xp", 0); private set => Set("Xp", value); }
        public static int Coins { get => Get("Coins", 120); private set => Set("Coins", value); }
        public static int Fish { get => Get("Fish", 5); private set => Set("Fish", value); }
        public static int Deliveries { get => Get("Deliveries", 0); private set => Set("Deliveries", value); }

        public static int XpToNext(int level) => 150 + level * 50;

        public static event System.Action Changed;

        public static void AddCoins(int v) { Coins = Mathf.Max(0, Coins + v); Changed?.Invoke(); }
        public static void AddFish(int v) { Fish = Mathf.Max(0, Fish + v); Changed?.Invoke(); }
        public static void CountDelivery() { Deliveries = Deliveries + 1; }

        /// <summary>Adds XP; returns how many levels were gained.</summary>
        public static int AddXp(int v)
        {
            int xp = Xp + v, lv = Level, gained = 0;
            while (xp >= XpToNext(lv)) { xp -= XpToNext(lv); lv++; gained++; }
            Xp = xp; Level = lv;
            Changed?.Invoke();
            return gained;
        }

        static int Get(string k, int d) { try { return PlayerPrefs.GetInt(K + k, d); } catch { return d; } }
        static void Set(string k, int v) { try { PlayerPrefs.SetInt(K + k, v); PlayerPrefs.Save(); } catch { } }
    }
}
