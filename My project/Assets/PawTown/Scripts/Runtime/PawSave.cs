using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// Persistent game state that is not level/currency (see PawProgress): bag, cosmetics, Paw Pass, daily rewards,
    /// missions, starter bundle and settings. Everything lives in PlayerPrefs under "PawTown.".
    /// </summary>
    public static class PawSave
    {
        const string K = "PawTown.";
        public static event Action Changed;
        public static void Touch() => Changed?.Invoke();

        static int GetI(string k, int d) { try { return PlayerPrefs.GetInt(K + k, d); } catch { return d; } }
        static void SetI(string k, int v) { try { PlayerPrefs.SetInt(K + k, v); PlayerPrefs.Save(); } catch { } }
        static float GetF(string k, float d) { try { return PlayerPrefs.GetFloat(K + k, d); } catch { return d; } }
        static void SetF(string k, float v) { try { PlayerPrefs.SetFloat(K + k, v); PlayerPrefs.Save(); } catch { } }
        static string GetS(string k, string d) { try { return PlayerPrefs.GetString(K + k, d); } catch { return d; } }
        static void SetS(string k, string v) { try { PlayerPrefs.SetString(K + k, v); PlayerPrefs.Save(); } catch { } }
        static HashSet<string> GetSet(string k) => new HashSet<string>(GetS(k, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        static void SetSet(string k, HashSet<string> s) => SetS(k, string.Join(",", s));

        public static string Today => DateTime.Now.ToString("yyyy-MM-dd");

        // ------------------------------------------------------------------ bag
        public const int BagBase = 2, BagMax = 9;   // a new player carries 2 treats; Bigger Bag upgrades unlock the rest
        public static int BagSize { get => Mathf.Clamp(GetI("BagSize", BagBase), BagBase, BagMax); set => SetI("BagSize", Mathf.Clamp(value, BagBase, BagMax)); }
        public static List<TreatKind> LoadBag()
        {
            var l = new List<TreatKind>();
            foreach (var s in GetS("Bag", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(s, out int v) && v >= 0 && v <= 2) l.Add((TreatKind)v);
            return l;
        }
        public static void SaveBag(List<TreatKind> bag)
        {
            var parts = new List<string>();
            foreach (var t in bag) parts.Add(((int)t).ToString());
            SetS("Bag", string.Join(",", parts));
        }

        // ------------------------------------------------------------------ cosmetics
        public static bool Owns(string id) => PawCosmetics.IsDefault(id) || GetSet("Owned").Contains(id);
        public static void Own(string id) { var s = GetSet("Owned"); if (s.Add(id)) { SetSet("Owned", s); MarkNew(id); Touch(); } }
        public static bool IsNew(string id) => GetSet("New").Contains(id);
        static void MarkNew(string id) { var s = GetSet("New"); s.Add(id); SetSet("New", s); }
        public static void Seen(string id) { var s = GetSet("New"); if (s.Remove(id)) SetSet("New", s); }
        public static string Equipped(string slot, string dflt) => GetS("Eq." + slot, dflt);
        public static void Equip(string slot, string id) { SetS("Eq." + slot, id); Touch(); }

        // ------------------------------------------------------------------ paw pass
        public const int PassTiers = 10, PassXpPerTier = 100;
        public static int PassXp { get { _ = SeasonEnd; return GetI("PassXp", 0); } set { SetI("PassXp", Mathf.Clamp(value, 0, PassTiers * PassXpPerTier)); Touch(); } }
        public static int PassTier => Mathf.Min(PassTiers, PassXp / PassXpPerTier);   // tiers reached (0..10)
        /// <summary>Paw Pass premium (VIP) is a monthly subscription: active until PassPremiumUntil, then locked again.
        /// Setting it true starts (or extends) 30 days.</summary>
        public static bool PassPremium
        {
            get => DateTime.Now < PassPremiumUntil;
            set
            {
                var from = value && PassPremiumUntil > DateTime.Now ? PassPremiumUntil : DateTime.Now;
                SetS("PassPremiumUntil", value ? from.AddDays(30).Ticks.ToString() : "0");
                Touch();
            }
        }
        public static DateTime PassPremiumUntil => long.TryParse(GetS("PassPremiumUntil", "0"), out var t) && t > 0 ? new DateTime(t) : DateTime.MinValue;
        public static bool PassClaimed(int tier, bool premium) => SeasonEnd > DateTime.MinValue && GetSet("PassClaimed").Contains((premium ? "p" : "f") + tier);
        public static void PassClaim(int tier, bool premium) { var s = GetSet("PassClaimed"); s.Add((premium ? "p" : "f") + tier); SetSet("PassClaimed", s); Touch(); }
        public static DateTime SeasonEnd
        {
            get
            {
                string s = GetS("SeasonEnd", "");
                if (!DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d)) { d = DateTime.Now.AddDays(30); SetS("SeasonEnd", d.ToString("o")); }
                if (DateTime.Now >= d)
                {
                    // season over: a fresh 30-day season with tier progress and claims reset
                    while (DateTime.Now >= d) d = d.AddDays(30);
                    SetS("SeasonEnd", d.ToString("o"));
                    SetI("PassXp", 0);
                    SetS("PassClaimed", "");
                    Touch();
                }
                return d;
            }
        }

        // ------------------------------------------------------------------ daily rewards (7-day streak)
        public static int DailyDay => GetI("DailyDay", 0);                 // days already claimed in the current streak
        public static string DailyLast => GetS("DailyLast", "");
        public static bool DailyClaimableToday
        {
            get
            {
                RefreshStreak();
                return DailyLast != Today;
            }
        }
        public static void RefreshStreak()
        {
            string last = DailyLast;
            if (string.IsNullOrEmpty(last)) return;
            if (DateTime.TryParse(last, out var d) && (DateTime.Now.Date - d.Date).TotalDays > 1.5) { SetI("DailyDay", 0); SetS("DailyLast", ""); }
            if (DailyDay >= 7 && last != Today) SetI("DailyDay", 0);   // a new week starts after day 7
        }
        public static void DailyClaim() { SetI("DailyDay", DailyDay + 1); SetS("DailyLast", Today); Touch(); }

        // ------------------------------------------------------------------ missions (reset daily)
        public static readonly string[] MissionIds = { "deliver", "treats", "zebra" };
        public static readonly int[] MissionGoal = { 3, 10, 5 };
        static void MissionDayCheck()
        {
            if (GetS("MissionDay", "") == Today) return;
            SetS("MissionDay", Today);
            foreach (var m in MissionIds) { SetI("M." + m, 0); SetI("MC." + m, 0); }
        }
        public static int Mission(string id) { MissionDayCheck(); return GetI("M." + id, 0); }
        public static void MissionAdd(string id, int v = 1)
        {
            MissionDayCheck();
            int i = Array.IndexOf(MissionIds, id);
            SetI("M." + id, Mathf.Min(MissionGoal[i], GetI("M." + id, 0) + v));
            Touch();
        }
        public static bool MissionClaimed(string id) { MissionDayCheck(); return GetI("MC." + id, 0) == 1; }
        public static void MissionClaim(string id) { SetI("MC." + id, 1); Touch(); }
        public static bool AnyMissionClaimable()
        {
            for (int i = 0; i < MissionIds.Length; i++) if (Mission(MissionIds[i]) >= MissionGoal[i] && !MissionClaimed(MissionIds[i])) return true;
            return false;
        }

        // ------------------------------------------------------------------ starter bundle
        public static bool BundleBought { get => GetI("BundleBought", 0) == 1; set { SetI("BundleBought", value ? 1 : 0); Touch(); } }
        public static DateTime BundleEnd
        {
            get
            {
                string s = GetS("BundleEnd", "");
                if (!DateTime.TryParse(s, out var d)) { d = DateTime.Now.AddHours(24); SetS("BundleEnd", d.ToString("o")); }
                return d;
            }
        }
        public static bool BundleAvailable => !BundleBought && DateTime.Now < BundleEnd;

        // ------------------------------------------------------------------ settings
        public static float MusicVol { get => GetF("Set.Music", 0.8f); set => SetF("Set.Music", value); }
        public static float SoundVol { get => GetF("Set.Sound", 1f); set => SetF("Set.Sound", value); }
        /// <summary>The orange ground arrow that points along the route (the destination beacon always shows).</summary>
        public static bool ArrowGuide { get => GetI("Set.Arrow", 1) == 1; set => SetI("Set.Arrow", value ? 1 : 0); }
        public static bool Vibration { get => GetI("Set.Vibe", 1) == 1; set => SetI("Set.Vibe", value ? 1 : 0); }
        public static float JoystickSize { get => GetF("Set.Stick", 1f); set => SetF("Set.Stick", value); }
        public static int Quality { get => GetI("Set.Quality", 1); set => SetI("Set.Quality", value); }
        public static bool SeenTitle { get => GetI("SeenTitle", 0) == 1; set => SetI("SeenTitle", value ? 1 : 0); }
    }
}
