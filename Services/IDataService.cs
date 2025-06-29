using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YAWDA.Models;

namespace YAWDA.Services
{
    /// <summary>
    /// Interface for data persistence and settings management
    /// </summary>
    public interface IDataService
    {
        /// <summary>
        /// Initializes the database and creates tables if needed
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Logs a water intake record
        /// </summary>
        /// <param name="amount">Amount in ml</param>
        /// <param name="source">Source of the intake (manual, reminder, etc.)</param>
        Task LogWaterIntakeAsync(int amount, string source = "manual");

        /// <summary>
        /// Gets all water intake records for a specific date
        /// </summary>
        /// <param name="date">The date to query</param>
        Task<List<WaterIntakeRecord>> GetDailyIntakeAsync(DateTime date);

        /// <summary>
        /// Gets daily intake records for a date range
        /// </summary>
        /// <param name="startDate">Start date (inclusive)</param>
        /// <param name="endDate">End date (inclusive)</param>
        Task<List<WaterIntakeRecord>> GetIntakeHistoryAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Gets the total water intake for today
        /// </summary>
        Task<int> GetTodaysTotalIntakeAsync();

        /// <summary>
        /// Gets daily statistics for a specific date
        /// </summary>
        /// <param name="date">The date to analyze</param>
        Task<DailyStats> GetDailyStatsAsync(DateTime date);

        /// <summary>
        /// Saves user settings to persistent storage
        /// </summary>
        /// <param name="settings">Settings to save</param>
        Task SaveSettingsAsync(UserSettings settings);

        /// <summary>
        /// Loads user settings from persistent storage
        /// </summary>
        Task<UserSettings> LoadSettingsAsync();

        /// <summary>
        /// Cleans up old records beyond the retention period
        /// </summary>
        /// <param name="retentionDays">Number of days to keep</param>
        Task CleanupOldRecordsAsync(int retentionDays = 365);

        /// <summary>
        /// Exports intake data to CSV format
        /// </summary>
        /// <param name="startDate">Start date for export</param>
        /// <param name="endDate">End date for export</param>
        Task<string> ExportDataToCsvAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Gets database statistics for monitoring and debugging
        /// </summary>
        Task<Dictionary<string, object?>> GetDatabaseStatsAsync();
    }
} 