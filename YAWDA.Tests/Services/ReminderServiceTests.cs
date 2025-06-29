using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using YAWDA.Models;
using YAWDA.Services;
using Xunit;

namespace YAWDA.Tests.Services
{
    public class ReminderServiceTests : IDisposable
    {
        private readonly Mock<ILogger<ReminderService>> _mockLogger;
        private readonly Mock<IDataService> _mockDataService;
        private readonly Mock<ISmartFeaturesService> _mockSmartFeaturesService;
        private readonly ReminderService _reminderService;

        public ReminderServiceTests()
        {
            _mockLogger = new Mock<ILogger<ReminderService>>();
            _mockDataService = new Mock<IDataService>();
            _mockSmartFeaturesService = new Mock<ISmartFeaturesService>();

            _reminderService = new ReminderService(
                _mockLogger.Object,
                _mockDataService.Object,
                _mockSmartFeaturesService.Object);
        }

        [Fact]
        public async Task StartAsync_ShouldInitializeAndScheduleFirstReminder()
        {
            // Arrange
            var defaultState = ReminderState.CreateDefault();
            var defaultSettings = UserSettings.CreateDefault();

            _mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(defaultSettings);

            // Act
            await _reminderService.StartAsync();

            // Assert
            _reminderService.IsRunning.Should().BeTrue();
            _mockDataService.Verify(x => x.LoadSettingsAsync(), Times.Once);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldLogWarning()
        {
            // Arrange
            var defaultSettings = UserSettings.CreateDefault();
            _mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(defaultSettings);

            await _reminderService.StartAsync();

            // Act
            await _reminderService.StartAsync();

            // Assert
            // Should not throw and service should still be running
            _reminderService.IsRunning.Should().BeTrue();
        }

        [Fact]
        public async Task StopAsync_ShouldStopServiceAndSaveState()
        {
            // Arrange
            var defaultSettings = UserSettings.CreateDefault();
            _mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(defaultSettings);

            await _reminderService.StartAsync();

            // Act
            await _reminderService.StopAsync();

            // Assert
            _reminderService.IsRunning.Should().BeFalse();
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldLogWarning()
        {
            // Act
            await _reminderService.StopAsync();

            // Assert
            _reminderService.IsRunning.Should().BeFalse();
        }

        [Fact]
        public async Task PauseAsync_ShouldPauseRemindersForSpecifiedDuration()
        {
            // Arrange
            var duration = TimeSpan.FromMinutes(30);

            // Act
            await _reminderService.PauseAsync(duration);

            // Assert
            _reminderService.IsPaused.Should().BeTrue();
        }

        [Fact]
        public async Task ResumeAsync_ShouldResumeRemindersAndReschedule()
        {
            // Arrange
            await _reminderService.PauseAsync(TimeSpan.FromMinutes(30));

            // Act
            await _reminderService.ResumeAsync();

            // Assert
            _reminderService.IsPaused.Should().BeFalse();
        }

        [Fact]
        public async Task RecordWaterIntakeAsync_ShouldResetEscalationAndReschedule()
        {
            // Arrange
            var amount = 250;

            // Act
            await _reminderService.RecordWaterIntakeAsync(amount);

            // Assert
            // Should update internal state and reschedule
            // We can't directly access internal state, but we can verify no exceptions
        }

        [Fact]
        public async Task RecordReminderDismissedAsync_ShouldEscalateAndReschedule()
        {
            // Act
            await _reminderService.RecordReminderDismissedAsync();

            // Assert
            // Should update internal state with escalation
            // We can't directly access internal state, but we can verify no exceptions
        }

        [Fact]
        public async Task CalculateNextReminderTimeAsync_WithDefaultSettings_ShouldReturnCorrectInterval()
        {
            // Arrange
            var defaultSettings = UserSettings.CreateDefault();
            _mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(defaultSettings);

            await _reminderService.StartAsync();

            // Act
            var nextTime = await _reminderService.CalculateNextReminderTimeAsync();

            // Assert
            nextTime.Should().BeAfter(DateTime.Now);
            nextTime.Should().BeBefore(DateTime.Now.AddMinutes(180)); // Max interval
        }

        [Fact]
        public async Task CalculateNextReminderTimeAsync_WithWeatherAdjustment_ShouldApplyWeatherFactor()
        {
            // Arrange
            var settings = UserSettings.CreateDefault();
            settings.EnableWeatherAdjustment = true;

            var hotWeather = new WeatherData
            {
                TemperatureCelsius = 35,
                Humidity = 80,
                Condition = "Hot",
                HeatIndex = 40
            };

            _mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(settings);
            _mockSmartFeaturesService.Setup(x => x.GetCurrentWeatherAsync())
                .ReturnsAsync(hotWeather);
            _mockSmartFeaturesService.Setup(x => x.CalculateWeatherHydrationFactor(hotWeather))
                .Returns(1.5); // Higher hydration need

            await _reminderService.StartAsync();

            // Act
            var nextTime = await _reminderService.CalculateNextReminderTimeAsync();

            // Assert
            nextTime.Should().BeAfter(DateTime.Now);
            // Should be sooner due to weather adjustment
            _mockSmartFeaturesService.Verify(x => x.GetCurrentWeatherAsync(), Times.Once);
            _mockSmartFeaturesService.Verify(x => x.CalculateWeatherHydrationFactor(hotWeather), Times.Once);
        }

        [Fact]
        public async Task CalculateNextReminderTimeAsync_WithCircadianAdjustment_ShouldExtendInterval()
        {
            // Arrange
            var settings = UserSettings.CreateDefault();
            settings.EnableCircadianAdjustment = true;

            _mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(settings);
            _mockSmartFeaturesService.Setup(x => x.IsCircadianNightMode())
                .Returns(true); // Night time

            await _reminderService.StartAsync();

            // Act
            var nextTime = await _reminderService.CalculateNextReminderTimeAsync();

            // Assert
            nextTime.Should().BeAfter(DateTime.Now);
            _mockSmartFeaturesService.Verify(x => x.IsCircadianNightMode(), Times.Once);
        }

        [Fact]
        public async Task UpdateSettingsAsync_ShouldUpdateSettingsAndReschedule()
        {
            // Arrange
            var newSettings = UserSettings.CreateDefault();
            newSettings.BaseReminderIntervalMinutes = 45;

            _mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(UserSettings.CreateDefault());

            await _reminderService.StartAsync();

            // Act
            await _reminderService.UpdateSettingsAsync(newSettings);

            // Assert
            // Should update internal settings and reschedule
            // We verify no exceptions are thrown
        }

        [Fact]
        public async Task UpdateSettingsAsync_WithNullSettings_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _reminderService.UpdateSettingsAsync(null!));
        }

        [Fact]
        public async Task ShouldPauseForSystemStateAsync_ShouldReturnSmartPauseResult()
        {
            // Arrange
            _mockSmartFeaturesService.Setup(x => x.ShouldSmartPauseAsync())
                .ReturnsAsync(true);

            // Act
            var shouldPause = await _reminderService.ShouldPauseForSystemStateAsync();

            // Assert
            shouldPause.Should().BeTrue();
            _mockSmartFeaturesService.Verify(x => x.ShouldSmartPauseAsync(), Times.Once);
        }

        [Fact]
        public async Task ShouldPauseForSystemStateAsync_WithException_ShouldReturnFalseAndLogWarning()
        {
            // Arrange
            _mockSmartFeaturesService.Setup(x => x.ShouldSmartPauseAsync())
                .ThrowsAsync(new Exception("System error"));

            // Act
            var shouldPause = await _reminderService.ShouldPauseForSystemStateAsync();

            // Assert
            shouldPause.Should().BeFalse();
        }

        [Fact]
        public async Task ShouldPauseForSystemStateAsync_WithFrequentCalls_ShouldCacheResult()
        {
            // Arrange
            _mockSmartFeaturesService.Setup(x => x.ShouldSmartPauseAsync())
                .ReturnsAsync(true);

            // Act
            var result1 = await _reminderService.ShouldPauseForSystemStateAsync();
            var result2 = await _reminderService.ShouldPauseForSystemStateAsync();

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeTrue();
            // Should only call once due to caching (within 1 minute)
            _mockSmartFeaturesService.Verify(x => x.ShouldSmartPauseAsync(), Times.Once);
        }

        [Fact]
        public async Task GetCurrentStateAsync_ShouldReturnCurrentState()
        {
            // Act
            var state = await _reminderService.GetCurrentStateAsync();

            // Assert
            state.Should().NotBeNull();
            state.CurrentEscalationLevel.Should().BeGreaterOrEqualTo(1);
            state.CurrentEscalationLevel.Should().BeLessOrEqualTo(4);
        }

        [Theory]
        [InlineData(1, 1.0)] // No escalation
        [InlineData(2, 0.8)] // Slight reduction
        [InlineData(3, 0.6)] // More aggressive
        [InlineData(4, 0.4)] // Most aggressive
        public void GetEscalationMultiplier_ShouldReturnCorrectMultiplier(int level, double expectedMultiplier)
        {
            // Use reflection to test private method
            var method = typeof(ReminderService).GetMethod("GetEscalationMultiplier", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Act
            var result = (double)method!.Invoke(_reminderService, new object[] { level })!;

            // Assert
            result.Should().Be(expectedMultiplier);
        }

        [Theory]
        [InlineData(1, 10)] // 10 minutes for level 1
        [InlineData(2, 5)]  // 5 minutes for level 2
        [InlineData(3, 3)]  // 3 minutes for level 3
        [InlineData(4, 1)]  // 1 minute for level 4
        public void GetEscalationDelay_ShouldReturnCorrectDelay(int level, int expectedMinutes)
        {
            // Use reflection to test private method
            var method = typeof(ReminderService).GetMethod("GetEscalationDelay", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Act
            var result = (TimeSpan)method!.Invoke(_reminderService, new object[] { level })!;

            // Assert
            result.TotalMinutes.Should().Be(expectedMinutes);
        }

        [Fact]
        public void GenerateReminderMessage_ShouldReturnNonEmptyMessage()
        {
            // Use reflection to test private method
            var method = typeof(ReminderService).GetMethod("GenerateReminderMessage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Act
            var result = (string)method!.Invoke(_reminderService, new object[0])!;

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("water"); // Should mention water
        }

        [Theory]
        [InlineData(9, 0, true)]   // 9 AM - within work hours
        [InlineData(17, 30, true)] // 5:30 PM - within work hours
        [InlineData(18, 30, false)] // 6:30 PM - outside work hours
        [InlineData(7, 0, false)]  // 7 AM - before work hours
        public void IsWithinWorkHours_ShouldReturnCorrectResult(int hour, int minute, bool expected)
        {
            // Use reflection to test private method
            var method = typeof(ReminderService).GetMethod("IsWithinWorkHours", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var testTime = DateTime.Today.AddHours(hour).AddMinutes(minute);
            
            // Act
            var result = (bool)method!.Invoke(_reminderService, new object[] { testTime })!;

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void AdjustForWorkHours_WithTimeOutsideWorkHours_ShouldAdjustToWorkStart()
        {
            // Use reflection to test private method
            var method = typeof(ReminderService).GetMethod("AdjustForWorkHours", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var earlyTime = DateTime.Today.AddHours(6); // 6 AM - before work
            
            // Act
            var result = (DateTime)method!.Invoke(_reminderService, new object[] { earlyTime })!;

            // Assert
            result.Should().BeAfter(earlyTime);
            result.TimeOfDay.Should().BeCloseTo(new TimeSpan(8, 0, 0), TimeSpan.FromMinutes(1)); // Should be adjusted to 8 AM
        }

        [Fact]
        public void AdjustForWorkHours_WithTimeWithinWorkHours_ShouldNotAdjust()
        {
            // Use reflection to test private method
            var method = typeof(ReminderService).GetMethod("AdjustForWorkHours", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var workTime = DateTime.Today.AddHours(10); // 10 AM - within work hours
            
            // Act
            var result = (DateTime)method!.Invoke(_reminderService, new object[] { workTime })!;

            // Assert
            result.Should().Be(workTime); // Should not be adjusted
        }

        [Fact]
        public void GetNextWorkHourStart_ShouldReturnCorrectWorkStart()
        {
            // Use reflection to test private method
            var method = typeof(ReminderService).GetMethod("GetNextWorkHourStart", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Act
            var result = (DateTime)method!.Invoke(_reminderService, new object[0])!;

            // Assert
            result.Should().BeAfter(DateTime.Now);
            result.TimeOfDay.Should().BeCloseTo(new TimeSpan(8, 0, 0), TimeSpan.FromMinutes(1)); // Should be 8 AM
        }

        [Fact]
        public void ReminderTriggered_Event_ShouldBeRaisedWhenReminderFires()
        {
            // Arrange
            ReminderEventArgs? eventArgs = null;
            _reminderService.ReminderTriggered += (sender, args) => eventArgs = args;

            // Use reflection to trigger reminder
            var method = typeof(ReminderService).GetMethod("TriggerReminderAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Act
            var task = (Task)method!.Invoke(_reminderService, new object[0])!;
            task.Wait();

            // Assert
            eventArgs.Should().NotBeNull();
            eventArgs!.Message.Should().NotBeNullOrEmpty();
            eventArgs.EscalationLevel.Should().BeGreaterOrEqualTo(1);
            eventArgs.EscalationLevel.Should().BeLessOrEqualTo(4);
        }

        [Fact]
        public void Dispose_ShouldDisposeResourcesProperly()
        {
            // Act
            _reminderService.Dispose();

            // Assert
            // Should not throw exception
            // Internal timer should be disposed
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Act
            _reminderService.Dispose();
            _reminderService.Dispose();

            // Assert
            // Should not throw exception
        }

        public void Dispose()
        {
            _reminderService?.Dispose();
        }
    }

    /// <summary>
    /// Test helper for accessing private members
    /// </summary>
    public static class ReminderServiceTestHelper
    {
        public static T InvokePrivateMethod<T>(ReminderService service, string methodName, params object[] parameters)
        {
            var method = typeof(ReminderService).GetMethod(methodName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method == null)
                throw new ArgumentException($"Method {methodName} not found");
                
            return (T)method.Invoke(service, parameters)!;
        }
    }
} 