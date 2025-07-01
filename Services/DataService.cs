using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using YAWDA.Models;

namespace YAWDA.Services
{
    /// <summary>
    /// SQLite-based implementation of data persistence and settings management
    /// </summary>
    public class DataService : IDataService, IDisposable
    {
        private readonly ILogger<DataService> _logger;
        private readonly string _databasePath;
        private readonly string _settingsPath;
        private SqliteConnection? _connection;
        private bool _disposed = false;

        private const int CurrentDatabaseVersion = 1;
        private const string DatabaseFileName = "yawda.db";
        private const string SettingsFileName = "settings.json";

        public DataService(ILogger<DataService> logger)
        {
            _logger = logger;
            
            // Store database in user's AppData folder
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YAWDA"
            );
            
            Directory.CreateDirectory(appDataPath);
            _databasePath = Path.Combine(appDataPath, DatabaseFileName);
            _settingsPath = Path.Combine(appDataPath, SettingsFileName);
            
            _logger.LogInformation("DataService initialized with database path: {DatabasePath}", _databasePath);
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            try
            {
                // Check if already initialized
                if (_connection != null && _connection.State == ConnectionState.Open)
                {
                    _logger.LogDebug("Database already initialized, skipping");
                    return;
                }

                _logger.LogInformation("DataService initialized with database path: {DatabasePath}", _databasePath);
                
                _connection = new SqliteConnection($"Data Source={_databasePath}");
                await _connection.OpenAsync();
                
                await CreateTablesAsync();
                await MigrateDatabaseAsync();
                
                _logger.LogInformation("Database initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize database");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LogWaterIntakeAsync(int amount, string source = "manual")
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");

            try
            {
                var record = new WaterIntakeRecord(amount, source);
                
                if (!record.IsValid())
                {
                    throw new ArgumentException("Invalid water intake record");
                }

                const string sql = @"
                    INSERT INTO WaterIntakeRecords (AmountMilliliters, Timestamp, Source, Notes, Date)
                    VALUES (@amount, @timestamp, @source, @notes, @date)";

                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@amount", record.AmountMilliliters);
                command.Parameters.AddWithValue("@timestamp", record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@source", record.Source);
                command.Parameters.AddWithValue("@notes", record.Notes ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@date", record.Date.ToString("yyyy-MM-dd"));

                await command.ExecuteNonQueryAsync();
                
                _logger.LogDebug("Logged water intake: {Amount}ml from {Source}", amount, source);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log water intake: {Amount}ml from {Source}", amount, source);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<WaterIntakeRecord>> GetDailyIntakeAsync(DateTime date)
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");

            try
            {
                const string sql = @"
                    SELECT Id, AmountMilliliters, Timestamp, Source, Notes
                    FROM WaterIntakeRecords
                    WHERE Date = @date
                    ORDER BY Timestamp";

                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                var records = new List<WaterIntakeRecord>();
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var record = new WaterIntakeRecord
                    {
                        Id = reader.GetInt32(0),
                        AmountMilliliters = reader.GetInt32(1),
                        Timestamp = DateTime.Parse(reader.GetString(2)),
                        Source = reader.GetString(3),
                        Notes = reader.IsDBNull(4) ? null : reader.GetString(4)
                    };
                    records.Add(record);
                }

                _logger.LogDebug("Retrieved {Count} intake records for {Date}", records.Count, date.ToString("yyyy-MM-dd"));
                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get daily intake for {Date}", date.ToString("yyyy-MM-dd"));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<WaterIntakeRecord>> GetIntakeHistoryAsync(DateTime startDate, DateTime endDate)
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");

            try
            {
                const string sql = @"
                    SELECT Id, AmountMilliliters, Timestamp, Source, Notes
                    FROM WaterIntakeRecords
                    WHERE Date >= @startDate AND Date <= @endDate
                    ORDER BY Timestamp";

                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

                var records = new List<WaterIntakeRecord>();
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var record = new WaterIntakeRecord
                    {
                        Id = reader.GetInt32(0),
                        AmountMilliliters = reader.GetInt32(1),
                        Timestamp = DateTime.Parse(reader.GetString(2)),
                        Source = reader.GetString(3),
                        Notes = reader.IsDBNull(4) ? null : reader.GetString(4)
                    };
                    records.Add(record);
                }

                _logger.LogDebug("Retrieved {Count} intake records from {StartDate} to {EndDate}", 
                    records.Count, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get intake history from {StartDate} to {EndDate}", 
                    startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> GetTodaysTotalIntakeAsync()
        {
            try
            {
                var todaysRecords = await GetDailyIntakeAsync(DateTime.Today);
                var total = todaysRecords.Sum(r => r.AmountMilliliters);
                
                _logger.LogDebug("Today's total intake: {Total}ml", total);
                return total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get today's total intake");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<DailyStats> GetDailyStatsAsync(DateTime date)
        {
            try
            {
                var records = await GetDailyIntakeAsync(date);
                var settings = await LoadSettingsAsync();
                
                // For now, we'll use placeholder values for reminder stats
                // These will be properly implemented when ReminderService is created
                var stats = DailyStats.CalculateFromRecords(records, settings.EffectiveDailyGoalMilliliters, 0, 0);
                
                _logger.LogDebug("Calculated daily stats for {Date}: {Total}ml, {Goal}% of goal", 
                    date.ToString("yyyy-MM-dd"), stats.TotalIntakeMilliliters, stats.GoalCompletionPercentage * 100);
                
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get daily stats for {Date}", date.ToString("yyyy-MM-dd"));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SaveSettingsAsync(UserSettings settings)
        {
            try
            {
                if (!settings.IsValid())
                {
                    throw new ArgumentException("Invalid user settings");
                }

                settings.UpdateLastModified();
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(_settingsPath, json);
                
                _logger.LogDebug("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<UserSettings> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    _logger.LogInformation("Settings file not found, creating default settings");
                    var defaultSettings = UserSettings.CreateDefault();
                    await SaveSettingsAsync(defaultSettings);
                    return defaultSettings;
                }

                var json = await File.ReadAllTextAsync(_settingsPath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (settings == null || !settings.IsValid())
                {
                    _logger.LogWarning("Invalid settings found, using defaults");
                    return UserSettings.CreateDefault();
                }

                _logger.LogDebug("Settings loaded successfully");
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings, using defaults");
                return UserSettings.CreateDefault();
            }
        }

        /// <inheritdoc />
        public async Task CleanupOldRecordsAsync(int retentionDays = 365)
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");

            try
            {
                var cutoffDate = DateTime.Today.AddDays(-retentionDays);
                
                const string sql = "DELETE FROM WaterIntakeRecords WHERE Date < @cutoffDate";
                
                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@cutoffDate", cutoffDate.ToString("yyyy-MM-dd"));
                
                var deletedRows = await command.ExecuteNonQueryAsync();
                
                _logger.LogInformation("Cleaned up {DeletedRows} old records older than {CutoffDate}", 
                    deletedRows, cutoffDate.ToString("yyyy-MM-dd"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old records");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<string> ExportDataToCsvAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var records = await GetIntakeHistoryAsync(startDate, endDate);
                
                var csv = new List<string>
                {
                    "Date,Time,Amount(ml),Source,Notes"
                };

                foreach (var record in records)
                {
                    var line = $"{record.Date:yyyy-MM-dd},{record.TimeOfDay:hh\\:mm\\:ss}," +
                              $"{record.AmountMilliliters},{record.Source}," +
                              $"\"{record.Notes?.Replace("\"", "\"\"") ?? ""}\"";
                    csv.Add(line);
                }

                var result = string.Join(Environment.NewLine, csv);
                
                _logger.LogInformation("Exported {RecordCount} records to CSV format", records.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export data to CSV");
                throw;
            }
        }

        /// <summary>
        /// Creates database tables if they don't exist
        /// </summary>
        private async Task CreateTablesAsync()
        {
            const string createWaterIntakeTable = @"
                CREATE TABLE IF NOT EXISTS WaterIntakeRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AmountMilliliters INTEGER NOT NULL,
                    Timestamp TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    Notes TEXT,
                    Date TEXT NOT NULL,
                    CONSTRAINT chk_amount CHECK (AmountMilliliters > 0 AND AmountMilliliters <= 2000),
                    CONSTRAINT chk_source_length CHECK (LENGTH(Source) <= 50),
                    CONSTRAINT chk_notes_length CHECK (Notes IS NULL OR LENGTH(Notes) <= 200)
                )";

            const string createDateIndex = @"
                CREATE INDEX IF NOT EXISTS idx_water_intake_date 
                ON WaterIntakeRecords(Date)";

            const string createTimestampIndex = @"
                CREATE INDEX IF NOT EXISTS idx_water_intake_timestamp 
                ON WaterIntakeRecords(Timestamp)";

            const string createVersionTable = @"
                CREATE TABLE IF NOT EXISTS DatabaseVersion (
                    Version INTEGER PRIMARY KEY,
                    AppliedDate TEXT NOT NULL
                )";

            if (_connection == null) return;

            using var command = new SqliteCommand(createWaterIntakeTable, _connection);
            await command.ExecuteNonQueryAsync();

            command.CommandText = createDateIndex;
            await command.ExecuteNonQueryAsync();

            command.CommandText = createTimestampIndex;
            await command.ExecuteNonQueryAsync();

            command.CommandText = createVersionTable;
            await command.ExecuteNonQueryAsync();

            _logger.LogDebug("Database tables created successfully");
        }

        /// <summary>
        /// Handles database migrations for schema changes
        /// </summary>
        private async Task MigrateDatabaseAsync()
        {
            if (_connection == null) return;

            try
            {
                // Check current database version
                const string getVersionSql = "SELECT MAX(Version) FROM DatabaseVersion";
                using var command = new SqliteCommand(getVersionSql, _connection);
                
                var currentVersion = 0;
                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    currentVersion = Convert.ToInt32(result);
                }

                if (currentVersion < CurrentDatabaseVersion)
                {
                    _logger.LogInformation("Migrating database from version {CurrentVersion} to {TargetVersion}", 
                        currentVersion, CurrentDatabaseVersion);

                    // Apply migrations
                    for (int version = currentVersion + 1; version <= CurrentDatabaseVersion; version++)
                    {
                        await ApplyMigrationAsync(version);
                    }

                    _logger.LogInformation("Database migration completed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database migration failed");
                throw;
            }
        }

        /// <summary>
        /// Applies a specific database migration
        /// </summary>
        private async Task ApplyMigrationAsync(int version)
        {
            if (_connection == null) return;

            _logger.LogInformation("Applying database migration version {Version}", version);

            switch (version)
            {
                case 1:
                    // Initial version - no migration needed as tables are already created
                    break;
                    
                // Future migrations can be added here
                // case 2:
                //     await ApplyMigrationV2();
                //     break;
            }

            // Record the applied migration
            const string insertVersionSql = @"
                INSERT INTO DatabaseVersion (Version, AppliedDate) 
                VALUES (@version, @appliedDate)";
            
            using var command = new SqliteCommand(insertVersionSql, _connection);
            command.Parameters.AddWithValue("@version", version);
            command.Parameters.AddWithValue("@appliedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            
            await command.ExecuteNonQueryAsync();
            
            _logger.LogDebug("Migration version {Version} applied successfully", version);
        }

        /// <summary>
        /// Gets database statistics for monitoring
        /// </summary>
        public async Task<Dictionary<string, object?>> GetDatabaseStatsAsync()
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");

            try
            {
                var stats = new Dictionary<string, object?>();

                // Total records
                const string countSql = "SELECT COUNT(*) FROM WaterIntakeRecords";
                using var countCommand = new SqliteCommand(countSql, _connection);
                stats["TotalRecords"] = await countCommand.ExecuteScalarAsync() ?? 0;

                // Database file size
                if (File.Exists(_databasePath))
                {
                    var fileInfo = new FileInfo(_databasePath);
                    stats["DatabaseSizeBytes"] = fileInfo.Length;
                }

                // Date range
                const string rangeSql = "SELECT MIN(Date), MAX(Date) FROM WaterIntakeRecords";
                using var rangeCommand = new SqliteCommand(rangeSql, _connection);
                using var reader = await rangeCommand.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    stats["EarliestRecord"] = reader.IsDBNull(0) ? (object?)null : reader.GetString(0);
                    stats["LatestRecord"] = reader.IsDBNull(1) ? (object?)null : reader.GetString(1);
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get database statistics");
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _connection?.Dispose();
                _disposed = true;
                _logger.LogDebug("DataService disposed");
            }
        }
    }
} 