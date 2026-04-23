using System;
using System.Collections.Generic;

namespace FarmPrototype
{
    public enum VillageSeason
    {
        Spring,
        Summer,
        Fall,
        Winter
    }

    public readonly struct VillageDate : IEquatable<VillageDate>
    {
        public VillageDate(int year, VillageSeason season, int day)
        {
            Year = year < 1 ? 1 : year;
            Season = season;
            Day = day < 1 ? 1 : day;
        }

        public int Year { get; }

        public VillageSeason Season { get; }

        public int Day { get; }

        public string ToDisplayLabel()
        {
            return "第 " + Year + " 年 " + GetSeasonLabel(Season) + " " + Day + " 日";
        }

        public string ToShortLabel()
        {
            return GetSeasonLabel(Season) + " " + Day + " 日";
        }

        public override string ToString()
        {
            return ToDisplayLabel();
        }

        public bool Equals(VillageDate other)
        {
            return Year == other.Year && Season == other.Season && Day == other.Day;
        }

        public override bool Equals(object obj)
        {
            return obj is VillageDate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Year, (int)Season, Day);
        }

        private static string GetSeasonLabel(VillageSeason season)
        {
            switch (season)
            {
                case VillageSeason.Spring:
                    return "春";
                case VillageSeason.Summer:
                    return "夏";
                case VillageSeason.Fall:
                    return "秋";
                case VillageSeason.Winter:
                    return "冬";
                default:
                    return "季";
            }
        }
    }

    public sealed class VillageFestival
    {
        public VillageFestival(string id, string displayName, VillageSeason season, int day)
        {
            Id = string.IsNullOrEmpty(id) ? "festival" : id;
            DisplayName = string.IsNullOrEmpty(displayName) ? "村庄节日" : displayName;
            Season = season;
            Day = day;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public VillageSeason Season { get; }

        public int Day { get; }
    }

    public sealed class VillageCalendar
    {
        public const int DaysPerSeason = 28;
        public const int SeasonsPerYear = 4;

        private readonly List<VillageFestival> _festivals = new List<VillageFestival>();
        private readonly Dictionary<int, VillageFestival> _festivalLookup = new Dictionary<int, VillageFestival>();

        public VillageCalendar(IEnumerable<VillageFestival> festivals)
        {
            if (festivals == null)
            {
                return;
            }

            foreach (VillageFestival festival in festivals)
            {
                if (festival == null)
                {
                    continue;
                }

                int day = ClampDay(festival.Day);
                VillageFestival normalizedFestival = day == festival.Day
                    ? festival
                    : new VillageFestival(festival.Id, festival.DisplayName, festival.Season, day);

                int key = GetSeasonDayKey(normalizedFestival.Season, normalizedFestival.Day);
                _festivalLookup[key] = normalizedFestival;
                _festivals.Add(normalizedFestival);
            }
        }

        public IReadOnlyList<VillageFestival> Festivals => _festivals;

        public static VillageCalendar CreateDefault()
        {
            VillageFestival[] festivals =
            {
                new VillageFestival("spring_market", "春季集市日", VillageSeason.Spring, 13),
                new VillageFestival("summer_fair", "夏季篝火夜", VillageSeason.Summer, 11),
                new VillageFestival("harvest_festival", "秋收庆典", VillageSeason.Fall, 16),
                new VillageFestival("winter_gathering", "冬日团聚会", VillageSeason.Winter, 25)
            };

            return new VillageCalendar(festivals);
        }

        public VillageDate GetDateForAbsoluteDay(int absoluteDay)
        {
            int safeAbsoluteDay = absoluteDay < 1 ? 1 : absoluteDay;
            int zeroBased = safeAbsoluteDay - 1;
            int yearLength = DaysPerSeason * SeasonsPerYear;
            int year = (zeroBased / yearLength) + 1;
            int dayInYear = zeroBased % yearLength;
            int seasonIndex = dayInYear / DaysPerSeason;
            int dayInSeason = (dayInYear % DaysPerSeason) + 1;
            VillageSeason season = (VillageSeason)seasonIndex;
            return new VillageDate(year, season, dayInSeason);
        }

        public int GetAbsoluteDay(VillageDate date)
        {
            int year = date.Year < 1 ? 1 : date.Year;
            int seasonIndex = (int)date.Season;
            if (seasonIndex < 0 || seasonIndex >= SeasonsPerYear)
            {
                seasonIndex = 0;
            }

            int day = ClampDay(date.Day);
            int dayInYear = seasonIndex * DaysPerSeason + (day - 1);
            return (year - 1) * DaysPerSeason * SeasonsPerYear + dayInYear + 1;
        }

        public bool TryGetFestival(VillageDate date, out VillageFestival festival)
        {
            int key = GetSeasonDayKey(date.Season, ClampDay(date.Day));
            return _festivalLookup.TryGetValue(key, out festival);
        }

        private static int GetSeasonDayKey(VillageSeason season, int day)
        {
            return ((int)season) * 100 + day;
        }

        private static int ClampDay(int day)
        {
            if (day < 1)
            {
                return 1;
            }

            if (day > DaysPerSeason)
            {
                return DaysPerSeason;
            }

            return day;
        }
    }
}
