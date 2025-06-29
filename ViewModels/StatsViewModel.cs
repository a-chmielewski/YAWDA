using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YAWDA.Models;
using YAWDA.Services;

namespace YAWDA.ViewModels
{
    /// <summary>
    /// ViewModel for the statistics page showing historical data and trends
    /// </summary>
    public partial class StatsViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        private DailyStats? _todaysStats;

        [ObservableProperty]
        private ObservableCollection<DailyStats> _weeklyStats = new();

        [ObservableProperty]
        private ObservableCollection<DailyStats> _monthlyStats = new();

        [ObservableProperty]
        private ObservableCollection<WaterIntakeRecord> _recentIntakeHistory = new();

        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private DateTime _selectedDate = DateTime.Today;

        [ObservableProperty]
        private StatsTimeframe _selectedTimeframe = StatsTimeframe.Week;

        // Summary statistics
        [ObservableProperty]
        private double _weeklyAverageIntake = 0;

        [ObservableProperty]
        private double _weeklyGoalAchievementRate = 0;

        [ObservableProperty]
        private int _currentStreak = 0;

        [ObservableProperty]
        private int _bestStreak = 0;

        [ObservableProperty]
        private double _overallComplianceRate = 0;

        [ObservableProperty]
        private string _topPerformanceDay = "Monday";

        public StatsViewModel(IDataService dataService)
        {
            _dataService = dataService;

            // Initialize async
            Task.Run(LoadStatsAsync);
        }

        [RelayCommand]
        private async Task LoadStatsAsync()
        {
            try
            {
                IsLoading = true;

                // Load today's stats
                TodaysStats = await _dataService.GetDailyStatsAsync(DateTime.Today);

                // Load weekly stats (last 7 days)
                await LoadWeeklyStatsAsync();

                // Load monthly stats (last 30 days)
                await LoadMonthlyStatsAsync();

                // Load recent intake history (last 20 records)
                await LoadRecentHistoryAsync();

                // Calculate summary statistics
                CalculateSummaryStats();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading stats: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ChangeTimeframeAsync(StatsTimeframe timeframe)
        {
            SelectedTimeframe = timeframe;
            await LoadStatsAsync();
        }

        [RelayCommand]
        private async Task ExportDataAsync()
        {
            try
            {
                var startDate = SelectedTimeframe switch
                {
                    StatsTimeframe.Week => DateTime.Today.AddDays(-7),
                    StatsTimeframe.Month => DateTime.Today.AddDays(-30),
                    StatsTimeframe.ThreeMonths => DateTime.Today.AddDays(-90),
                    _ => DateTime.Today.AddDays(-7)
                };

                var csvData = await _dataService.ExportDataToCsvAsync(startDate, DateTime.Today);
                
                // In a real implementation, you'd save this to a file or clipboard
                // For now, we'll just log it
                System.Diagnostics.Debug.WriteLine($"Exported {csvData.Length} characters of CSV data");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting data: {ex.Message}");
            }
        }

        private async Task LoadWeeklyStatsAsync()
        {
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-6); // Last 7 days including today

            WeeklyStats.Clear();
            
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var stats = await _dataService.GetDailyStatsAsync(date);
                WeeklyStats.Add(stats);
            }
        }

        private async Task LoadMonthlyStatsAsync()
        {
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-29); // Last 30 days including today

            MonthlyStats.Clear();
            
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var stats = await _dataService.GetDailyStatsAsync(date);
                MonthlyStats.Add(stats);
            }
        }

        private async Task LoadRecentHistoryAsync()
        {
            var startDate = DateTime.Today.AddDays(-7);
            var records = await _dataService.GetIntakeHistoryAsync(startDate, DateTime.Today);
            
            RecentIntakeHistory.Clear();
            foreach (var record in records.OrderByDescending(r => r.Timestamp).Take(20))
            {
                RecentIntakeHistory.Add(record);
            }
        }

        private void CalculateSummaryStats()
        {
            if (WeeklyStats.Count == 0) return;

            // Weekly average intake
            WeeklyAverageIntake = WeeklyStats.Average(s => s.TotalIntakeMilliliters);

            // Weekly goal achievement rate
            var goalsAchieved = WeeklyStats.Count(s => s.GoalAchieved);
            WeeklyGoalAchievementRate = (double)goalsAchieved / WeeklyStats.Count * 100;

            // Calculate current streak
            CurrentStreak = CalculateCurrentStreak();

            // Calculate best streak from monthly data
            BestStreak = CalculateBestStreak();

            // Overall compliance rate
            if (WeeklyStats.Any(s => s.RemindersShown > 0))
            {
                var totalReminders = WeeklyStats.Sum(s => s.RemindersShown);
                var totalComplied = WeeklyStats.Sum(s => s.RemindersComplied);
                OverallComplianceRate = totalReminders > 0 ? (double)totalComplied / totalReminders * 100 : 0;
            }

            // Top performance day
            if (WeeklyStats.Count > 0)
            {
                var bestDay = WeeklyStats.OrderByDescending(s => s.QualityScore).First();
                TopPerformanceDay = bestDay.Date.DayOfWeek.ToString();
            }
        }

        private int CalculateCurrentStreak()
        {
            int streak = 0;
            for (int i = WeeklyStats.Count - 1; i >= 0; i--)
            {
                if (WeeklyStats[i].GoalAchieved)
                {
                    streak++;
                }
                else
                {
                    break;
                }
            }
            return streak;
        }

        private int CalculateBestStreak()
        {
            if (MonthlyStats.Count == 0) return 0;

            int bestStreak = 0;
            int currentStreak = 0;

            foreach (var stat in MonthlyStats)
            {
                if (stat.GoalAchieved)
                {
                    currentStreak++;
                    bestStreak = Math.Max(bestStreak, currentStreak);
                }
                else
                {
                    currentStreak = 0;
                }
            }

            return bestStreak;
        }

        // Helper properties for UI
        public string TodaysPerformanceText => TodaysStats?.Category.ToString() ?? "No data";
        
        public string TodaysQualityScoreText => TodaysStats != null ? $"{TodaysStats.QualityScore:P0}" : "N/A";

        public string WeeklyAverageText => $"{WeeklyAverageIntake:F0}ml";

        public string GoalAchievementText => $"{WeeklyGoalAchievementRate:F0}%";

        public string ComplianceRateText => $"{OverallComplianceRate:F0}%";

        public string CurrentStreakText => CurrentStreak == 1 ? "1 day" : $"{CurrentStreak} days";

        public string BestStreakText => BestStreak == 1 ? "1 day" : $"{BestStreak} days";
    }

    public enum StatsTimeframe
    {
        Week,
        Month,
        ThreeMonths
    }
} 