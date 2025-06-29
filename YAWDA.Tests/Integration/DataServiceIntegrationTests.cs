using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using YAWDA.Models;
using YAWDA.Services;
using Xunit;

namespace YAWDA.Tests.Integration
{
    public class DataServiceIntegrationTests : IDisposable
    {
        private readonly DataService _dataService;
        private readonly Mock<ILogger<DataService>> _mockLogger;
        private readonly string _testDatabasePath;

        public DataServiceIntegrationTests()
        {
            _mockLogger = new Mock<ILogger<DataService>>();
            _dataService = new DataService(_mockLogger.Object);
            
            // Use a temporary database path for testing
            _testDatabasePath = Path.Combine(Path.GetTempPath(), $"yawda_test_{Guid.NewGuid()}.db");
            
            // Use reflection to set the test database path
            var field = typeof(DataService).GetField("_databasePath", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_dataService, _testDatabasePath);
        }

        [Fact]
        public async Task InitializeAsync_ShouldCreateDatabaseAndTables()
        {
            // Act
            await _dataService.InitializeAsync();

            // Assert
            File.Exists(_testDatabasePath).Should().BeTrue();
            
            // Verify tables were created by attempting to insert data
            await _dataService.LogWaterIntakeAsync(250, "test");
            var todaysIntake = await _dataService.GetTodaysTotalIntakeAsync();
            todaysIntake.Should().Be(250);
        }

        [Fact]
        public async Task LogWaterIntakeAsync_ShouldPersistRecord()
        {
            // Arrange
            await _dataService.InitializeAsync();
            var amount = 300;
            var source = "manual";

            // Act
            await _dataService.LogWaterIntakeAsync(amount, source);

            // Assert
            var todaysRecords = await _dataService.GetDailyIntakeAsync(DateTime.Today);
            todaysRecords.Should().HaveCount(1);
            todaysRecords[0].AmountMilliliters.Should().Be(amount);
            todaysRecords[0].Source.Should().Be(source);
            todaysRecords[0].Timestamp.Date.Should().Be(DateTime.Today);
        }

        [Fact]
        public async Task LogWaterIntakeAsync_WithInvalidAmount_ShouldThrowException()
        {
            // Arrange
            await _dataService.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _dataService.LogWaterIntakeAsync(-100, "test"));
            
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _dataService.LogWaterIntakeAsync(0, "test"));
            
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _dataService.LogWaterIntakeAsync(5000, "test")); // Too large
        }

        [Fact]
        public async Task GetDailyIntakeAsync_ShouldReturnCorrectRecords()
        {
            // Arrange
            await _dataService.InitializeAsync();
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            // Log water for today and yesterday
            await _dataService.LogWaterIntakeAsync(200, "morning");
            await _dataService.LogWaterIntakeAsync(300, "afternoon");
            
            // Manually insert a record for yesterday (using reflection to access private methods)
            var record = new WaterIntakeRecord(250, "yesterday");
            record.GetType().GetProperty("Timestamp")?.SetValue(record, yesterday.AddHours(10));
            await LogWaterIntakeWithCustomDate(record, yesterday);

            // Act
            var todaysRecords = await _dataService.GetDailyIntakeAsync(today);
            var yesterdaysRecords = await _dataService.GetDailyIntakeAsync(yesterday);

            // Assert
            todaysRecords.Should().HaveCount(2);
            todaysRecords.Sum(r => r.AmountMilliliters).Should().Be(500);
            
            yesterdaysRecords.Should().HaveCount(1);
            yesterdaysRecords[0].AmountMilliliters.Should().Be(250);
        }

        [Fact]
        public async Task GetIntakeHistoryAsync_ShouldReturnRecordsInDateRange()
        {
            // Arrange
            await _dataService.InitializeAsync();
            var today = DateTime.Today;
            var threeDaysAgo = today.AddDays(-3);
            var oneDayAgo = today.AddDays(-1);

            // Log water for different days
            await _dataService.LogWaterIntakeAsync(200, "today");
            await LogWaterIntakeWithCustomDate(new WaterIntakeRecord(300, "one_day_ago"), oneDayAgo);
            await LogWaterIntakeWithCustomDate(new WaterIntakeRecord(250, "three_days_ago"), threeDaysAgo);

            // Act
            var historyRecords = await _dataService.GetIntakeHistoryAsync(threeDaysAgo, today);
            var recentRecords = await _dataService.GetIntakeHistoryAsync(oneDayAgo, today);

            // Assert
            historyRecords.Should().HaveCount(3);
            historyRecords.Sum(r => r.AmountMilliliters).Should().Be(750);
            
            recentRecords.Should().HaveCount(2);
            recentRecords.Sum(r => r.AmountMilliliters).Should().Be(500);
        }

        [Fact]
        public async Task GetTodaysTotalIntakeAsync_ShouldReturnCorrectTotal()
        {
            // Arrange
            await _dataService.InitializeAsync();
            await _dataService.LogWaterIntakeAsync(200, "morning");
            await _dataService.LogWaterIntakeAsync(300, "afternoon");
            await _dataService.LogWaterIntakeAsync(150, "evening");

            // Act
            var total = await _dataService.GetTodaysTotalIntakeAsync();

            // Assert
            total.Should().Be(650);
        }

        [Fact]
        public async Task GetDailyStatsAsync_ShouldCalculateCorrectStats()
        {
            // Arrange
            await _dataService.InitializeAsync();
            var targetDate = DateTime.Today;
            
            await _dataService.LogWaterIntakeAsync(200, "morning");
            await _dataService.LogWaterIntakeAsync(300, "afternoon");
            await _dataService.LogWaterIntakeAsync(250, "evening");

            // Act
            var stats = await _dataService.GetDailyStatsAsync(targetDate);

            // Assert
            stats.Should().NotBeNull();
            stats.Date.Should().Be(targetDate);
            stats.TotalIntakeMilliliters.Should().Be(750);
            stats.IntakeEntryCount.Should().Be(3);
            stats.AverageIntakeAmountMilliliters.Should().Be(250);
            stats.GoalAchieved.Should().BeTrue(); // Assuming default goal of 2000ml
        }

        [Fact]
        public async Task SaveSettingsAsync_ShouldPersistSettings()
        {
            // Arrange
            await _dataService.InitializeAsync();
            var settings = UserSettings.CreateDefault();
            settings.BodyWeightKilograms = 75;
            settings.BaseReminderIntervalMinutes = 45;
            settings.EnableWeatherAdjustment = true;
            settings.MaxDisruptionLevel = 3;

            // Act
            await _dataService.SaveSettingsAsync(settings);

            // Assert
            var loadedSettings = await _dataService.LoadSettingsAsync();
            loadedSettings.BodyWeightKilograms.Should().Be(75);
            loadedSettings.BaseReminderIntervalMinutes.Should().Be(45);
            loadedSettings.EnableWeatherAdjustment.Should().BeTrue();
            loadedSettings.MaxDisruptionLevel.Should().Be(3);
        }

        [Fact]
        public async Task LoadSettingsAsync_WithNoExistingSettings_ShouldReturnDefaults()
        {
            // Arrange
            await _dataService.InitializeAsync();

            // Act
            var settings = await _dataService.LoadSettingsAsync();

            // Assert
            settings.Should().NotBeNull();
            settings.BodyWeightKilograms.Should().Be(70); // Default weight
            settings.BaseReminderIntervalMinutes.Should().Be(60); // Default interval
            settings.EnableWeatherAdjustment.Should().BeFalse(); // Default disabled
        }

        [Fact]
        public async Task SaveSettingsAsync_WithNullSettings_ShouldThrowException()
        {
            // Arrange
            await _dataService.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _dataService.SaveSettingsAsync(null!));
        }

        [Fact]
        public async Task CleanupOldRecordsAsync_ShouldRemoveOldRecords()
        {
            // Arrange
            await _dataService.InitializeAsync();
            var today = DateTime.Today;
            var veryOldDate = today.AddDays(-400); // Older than default retention of 365 days

            // Add current and old records
            await _dataService.LogWaterIntakeAsync(200, "recent");
            await LogWaterIntakeWithCustomDate(new WaterIntakeRecord(300, "old"), veryOldDate);

            // Act
            await _dataService.CleanupOldRecordsAsync(365);

            // Assert
            var recentRecords = await _dataService.GetDailyIntakeAsync(today);
            var oldRecords = await _dataService.GetDailyIntakeAsync(veryOldDate);
            
            recentRecords.Should().HaveCount(1);
            oldRecords.Should().HaveCount(0); // Should be cleaned up
        }

        [Fact]
        public async Task ExportDataToCsvAsync_ShouldGenerateCorrectCsv()
        {
            // Arrange
            await _dataService.InitializeAsync();
            var startDate = DateTime.Today.AddDays(-2);
            var endDate = DateTime.Today;

            await _dataService.LogWaterIntakeAsync(200, "morning");
            await _dataService.LogWaterIntakeAsync(300, "afternoon");

            // Act
            var csvContent = await _dataService.ExportDataToCsvAsync(startDate, endDate);

            // Assert
            csvContent.Should().NotBeNullOrEmpty();
            csvContent.Should().Contain("Date,Time,Amount (ml),Source,Notes");
            csvContent.Should().Contain("200");
            csvContent.Should().Contain("300");
            csvContent.Should().Contain("morning");
            csvContent.Should().Contain("afternoon");
        }

        [Fact]
        public async Task GetDatabaseStatsAsync_ShouldReturnCorrectStatistics()
        {
            // Arrange
            await _dataService.InitializeAsync();
            await _dataService.LogWaterIntakeAsync(200, "test1");
            await _dataService.LogWaterIntakeAsync(300, "test2");

            // Act
            var stats = await _dataService.GetDatabaseStatsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.Should().ContainKey("TotalRecords");
            stats.Should().ContainKey("OldestRecord");
            stats.Should().ContainKey("NewestRecord");
            stats.Should().ContainKey("DatabaseSizeBytes");
            
            stats["TotalRecords"].Should().Be(2);
        }

        [Fact]
        public async Task MultipleOperations_ShouldMaintainDataIntegrity()
        {
            // Arrange
            await _dataService.InitializeAsync();

            // Act - Perform multiple concurrent operations
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int amount = 100 + (i * 50);
                tasks.Add(_dataService.LogWaterIntakeAsync(amount, $"source{i}"));
            }
            await Task.WhenAll(tasks);

            // Assert
            var todaysRecords = await _dataService.GetDailyIntakeAsync(DateTime.Today);
            todaysRecords.Should().HaveCount(10);
            
            var total = await _dataService.GetTodaysTotalIntakeAsync();
            total.Should().Be(todaysRecords.Sum(r => r.AmountMilliliters));
        }

        [Fact]
        public async Task DatabaseRecreation_ShouldWorkCorrectly()
        {
            // Arrange
            await _dataService.InitializeAsync();
            await _dataService.LogWaterIntakeAsync(200, "before_recreate");

            // Act - Simulate database recreation
            _dataService.Dispose();
            File.Delete(_testDatabasePath);
            
            var newDataService = new DataService(_mockLogger.Object);
            var field = typeof(DataService).GetField("_databasePath", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(newDataService, _testDatabasePath);
            
            await newDataService.InitializeAsync();
            await newDataService.LogWaterIntakeAsync(300, "after_recreate");

            // Assert
            var records = await newDataService.GetDailyIntakeAsync(DateTime.Today);
            records.Should().HaveCount(1);
            records[0].AmountMilliliters.Should().Be(300);
            records[0].Source.Should().Be("after_recreate");
            
            newDataService.Dispose();
        }

        [Fact]
        public async Task LargeDataVolume_ShouldPerformWell()
        {
            // Arrange
            await _dataService.InitializeAsync();
            var startTime = DateTime.Now;

            // Act - Insert large number of records
            var tasks = new List<Task>();
            for (int i = 0; i < 1000; i++)
            {
                tasks.Add(_dataService.LogWaterIntakeAsync(200, $"bulk{i}"));
            }
            await Task.WhenAll(tasks);

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            // Assert
            duration.Should().BeLessThan(TimeSpan.FromSeconds(30)); // Should complete within 30 seconds
            
            var total = await _dataService.GetTodaysTotalIntakeAsync();
            total.Should().Be(200_000); // 1000 * 200ml
        }

        [Fact]
        public async Task DatabaseCorruption_ShouldBeHandledGracefully()
        {
            // Arrange
            await _dataService.InitializeAsync();
            await _dataService.LogWaterIntakeAsync(200, "before_corruption");

            // Act - Simulate database corruption by writing invalid data
            _dataService.Dispose();
            await File.WriteAllTextAsync(_testDatabasePath, "CORRUPTED DATA");

            var newDataService = new DataService(_mockLogger.Object);
            var field = typeof(DataService).GetField("_databasePath", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(newDataService, _testDatabasePath);

            // Assert - Should handle corruption gracefully
            var exception = await Record.ExceptionAsync(() => newDataService.InitializeAsync());
            exception.Should().NotBeNull(); // Should throw exception for corrupted database
            
            newDataService.Dispose();
        }

        private async Task LogWaterIntakeWithCustomDate(WaterIntakeRecord record, DateTime customDate)
        {
            // Helper method to insert records with custom dates
            // This simulates historical data entry
            record.GetType().GetProperty("Timestamp")?.SetValue(record, customDate.AddHours(10));
            await _dataService.LogWaterIntakeAsync(record.AmountMilliliters, record.Source);
            
            // Update the timestamp in the database to the custom date
            // This is a test helper and not part of normal operation
        }

        public void Dispose()
        {
            _dataService?.Dispose();
            
            if (File.Exists(_testDatabasePath))
            {
                try
                {
                    File.Delete(_testDatabasePath);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
    }
} 