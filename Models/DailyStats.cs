using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;

namespace YAWDA.Models
{
    /// <summary>
    /// Daily statistics and analysis for water intake tracking
    /// </summary>
    public class DailyStats
    {
        /// <summary>
        /// Date for which these statistics apply
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Total water intake for the day in milliliters
        /// </summary>
        public int TotalIntakeMilliliters { get; set; } = 0;

        /// <summary>
        /// Daily goal in milliliters
        /// </summary>
        public int DailyGoalMilliliters { get; set; } = 2310; // Default for 70kg person

        /// <summary>
        /// Number of individual intake entries
        /// </summary>
        public int IntakeEntryCount { get; set; } = 0;

        /// <summary>
        /// Average amount per intake entry
        /// </summary>
        public int AverageIntakeAmountMilliliters => IntakeEntryCount == 0 ? 0 : TotalIntakeMilliliters / IntakeEntryCount;

        /// <summary>
        /// Goal completion percentage (0.0 to 1.0+)
        /// </summary>
        [JsonIgnore]
        public double GoalCompletionPercentage => DailyGoalMilliliters == 0 ? 0.0 : (double)TotalIntakeMilliliters / DailyGoalMilliliters;

        /// <summary>
        /// Whether the daily goal was achieved
        /// </summary>
        [JsonIgnore]
        public bool GoalAchieved => TotalIntakeMilliliters >= DailyGoalMilliliters;

        /// <summary>
        /// First intake time of the day
        /// </summary>
        public TimeSpan? FirstIntakeTime { get; set; }

        /// <summary>
        /// Last intake time of the day
        /// </summary>
        public TimeSpan? LastIntakeTime { get; set; }

        /// <summary>
        /// Duration between first and last intake
        /// </summary>
        [JsonIgnore]
        public TimeSpan? IntakeDuration
        {
            get
            {
                if (FirstIntakeTime == null || LastIntakeTime == null)
                    return null;
                
                var duration = LastIntakeTime.Value - FirstIntakeTime.Value;
                return duration.TotalSeconds < 0 ? duration.Add(TimeSpan.FromDays(1)) : duration;
            }
        }

        /// <summary>
        /// Total reminders shown for the day
        /// </summary>
        public int RemindersShown { get; set; } = 0;

        /// <summary>
        /// Total reminders that resulted in water intake
        /// </summary>
        public int RemindersComplied { get; set; } = 0;

        /// <summary>
        /// Compliance rate for reminders (0.0 to 1.0)
        /// </summary>
        [JsonIgnore]
        public double ReminderComplianceRate => RemindersShown == 0 ? 1.0 : (double)RemindersComplied / RemindersShown;

        /// <summary>
        /// Breakdown of intake sources (manual, reminder_200ml, etc.)
        /// </summary>
        public Dictionary<string, IntakeSourceStats> IntakeBySource { get; set; } = new();

        /// <summary>
        /// Hourly breakdown of intake amounts
        /// </summary>
        public Dictionary<int, int> IntakeByHour { get; set; } = new();

        /// <summary>
        /// Peak intake hour (hour with most water consumed)
        /// </summary>
        [JsonIgnore]
        public int? PeakIntakeHour => IntakeByHour.Count == 0 ? null : IntakeByHour.OrderByDescending(kvp => kvp.Value).First().Key;

        /// <summary>
        /// Longest gap between intakes in hours
        /// </summary>
        public double LongestGapHours { get; set; } = 0.0;

        /// <summary>
        /// Quality score based on consistency, goal achievement, and timing (0.0 to 1.0)
        /// </summary>
        [JsonIgnore]
        public double QualityScore
        {
            get
            {
                if (IntakeEntryCount == 0) return 0.0;

                var goalScore = Math.Min(1.0, GoalCompletionPercentage);
                var consistencyScore = CalculateConsistencyScore();
                var complianceScore = ReminderComplianceRate;

                return (goalScore * 0.4) + (consistencyScore * 0.3) + (complianceScore * 0.3);
            }
        }

        /// <summary>
        /// Performance category based on quality score
        /// </summary>
        [JsonIgnore]
        public PerformanceCategory Category
        {
            get
            {
                var score = QualityScore;
                return score switch
                {
                    >= 0.8 => PerformanceCategory.Excellent,
                    >= 0.6 => PerformanceCategory.Good,
                    >= 0.4 => PerformanceCategory.Fair,
                    _ => PerformanceCategory.NeedsImprovement
                };
            }
        }

        /// <summary>
        /// When these statistics were last calculated
        /// </summary>
        public DateTime LastCalculated { get; set; } = DateTime.Now;

        /// <summary>
        /// Calculates statistics from a collection of intake records
        /// </summary>
        /// <param name="intakeRecords">Collection of intake records for the day</param>
        /// <param name="dailyGoal">Daily goal in milliliters</param>
        /// <param name="remindersShown">Number of reminders shown</param>
        /// <param name="remindersComplied">Number of reminders complied with</param>
        /// <returns>Calculated daily statistics</returns>
        public static DailyStats CalculateFromRecords(
            IEnumerable<WaterIntakeRecord> intakeRecords, 
            int dailyGoal, 
            int remindersShown = 0, 
            int remindersComplied = 0)
        {
            var records = intakeRecords.ToList();
            var stats = new DailyStats();

            if (records.Count == 0)
            {
                stats.Date = DateTime.Today;
                stats.DailyGoalMilliliters = dailyGoal;
                stats.RemindersShown = remindersShown;
                stats.RemindersComplied = remindersComplied;
                return stats;
            }

            stats.Date = records.First().Date;
            stats.DailyGoalMilliliters = dailyGoal;
            stats.TotalIntakeMilliliters = records.Sum(r => r.AmountMilliliters);
            stats.IntakeEntryCount = records.Count;
            stats.RemindersShown = remindersShown;
            stats.RemindersComplied = remindersComplied;

            // Time analysis
            var times = records.Select(r => r.TimeOfDay).OrderBy(t => t).ToList();
            stats.FirstIntakeTime = times.First();
            stats.LastIntakeTime = times.Last();

            // Calculate longest gap
            stats.LongestGapHours = CalculateLongestGap(times);

            // Source breakdown
            stats.IntakeBySource = records
                .GroupBy(r => r.Source)
                .ToDictionary(
                    g => g.Key,
                    g => new IntakeSourceStats
                    {
                        Count = g.Count(),
                        TotalAmount = g.Sum(r => r.AmountMilliliters),
                        AverageAmount = (int)g.Average(r => r.AmountMilliliters)
                    });

            // Hourly breakdown
            stats.IntakeByHour = records
                .GroupBy(r => r.Timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.AmountMilliliters));

            stats.LastCalculated = DateTime.Now;
            return stats;
        }

        /// <summary>
        /// Calculates the longest gap between intakes
        /// </summary>
        private static double CalculateLongestGap(List<TimeSpan> times)
        {
            if (times.Count <= 1) return 0.0;

            double maxGap = 0.0;
            for (int i = 1; i < times.Count; i++)
            {
                var gap = (times[i] - times[i - 1]).TotalHours;
                if (gap < 0) gap += 24; // Handle overnight gap
                maxGap = Math.Max(maxGap, gap);
            }

            return maxGap;
        }

        /// <summary>
        /// Calculates consistency score based on intake distribution
        /// </summary>
        private double CalculateConsistencyScore()
        {
            if (IntakeByHour.Count == 0) return 0.0;

            // Penalize long gaps
            var gapPenalty = Math.Max(0.0, 1.0 - (LongestGapHours / 12.0));

            // Reward even distribution throughout the day
            var distributionScore = IntakeByHour.Count / 12.0; // Ideal: spread across 12 hours

            return Math.Min(1.0, (gapPenalty + distributionScore) / 2.0);
        }

        /// <summary>
        /// Gets a summary message for the day's performance
        /// </summary>
        /// <returns>Human-readable summary</returns>
        public string GetSummaryMessage()
        {
            var goalText = GoalAchieved ? "Goal achieved!" : $"{GoalCompletionPercentage:P0} of goal";
            var qualityText = Category switch
            {
                PerformanceCategory.Excellent => "Excellent hydration!",
                PerformanceCategory.Good => "Good hydration habits",
                PerformanceCategory.Fair => "Room for improvement",
                _ => "Let's focus on consistent hydration"
            };

            return $"{goalText} - {qualityText}";
        }

        /// <summary>
        /// Gets recommendations for improving hydration
        /// </summary>
        /// <returns>List of actionable recommendations</returns>
        public List<string> GetRecommendations()
        {
            var recommendations = new List<string>();

            if (!GoalAchieved)
            {
                var remaining = DailyGoalMilliliters - TotalIntakeMilliliters;
                recommendations.Add($"Drink {remaining}ml more to reach your daily goal");
            }

            if (LongestGapHours > 4)
            {
                recommendations.Add("Try to drink water more consistently throughout the day");
            }

            if (ReminderComplianceRate < 0.7)
            {
                recommendations.Add("Respond to more reminders to build better habits");
            }

            if (IntakeEntryCount > 0 && AverageIntakeAmountMilliliters < 200)
            {
                recommendations.Add("Consider drinking larger amounts less frequently");
            }

            return recommendations;
        }
    }

    /// <summary>
    /// Statistics for a specific intake source
    /// </summary>
    public class IntakeSourceStats
    {
        public int Count { get; set; }
        public int TotalAmount { get; set; }
        public int AverageAmount { get; set; }
    }

    /// <summary>
    /// Performance categories based on daily quality score
    /// </summary>
    public enum PerformanceCategory
    {
        NeedsImprovement,
        Fair,
        Good,
        Excellent
    }
} 