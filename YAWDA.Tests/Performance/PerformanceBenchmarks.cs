using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using YAWDA.Models;
using YAWDA.Services;
using Xunit;
using FluentAssertions;

namespace YAWDA.Tests.Performance
{
    [Config(typeof(PerformanceConfig))]
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    public class PerformanceBenchmarks
    {
        private IServiceProvider? _serviceProvider;
        private Mock<IDataService>? _mockDataService;
        private Mock<ISmartFeaturesService>? _mockSmartFeaturesService;
        private ReminderService? _reminderService;
        private DataService? _dataService;
        private SmartFeaturesService? _smartFeaturesService;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Setup service collection for testing
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Error));
            
            // Setup mocks
            _mockDataService = new Mock<IDataService>();
            _mockSmartFeaturesService = new Mock<ISmartFeaturesService>();
            
            _mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(UserSettings.CreateDefault());
            _mockSmartFeaturesService.Setup(x => x.ShouldSmartPauseAsync())
                .ReturnsAsync(false);
            
            // Register services
            services.AddSingleton(_mockDataService.Object);
            services.AddSingleton(_mockSmartFeaturesService.Object);
            services.AddSingleton<ReminderService>();
            
            _serviceProvider = services.BuildServiceProvider();
            
            // Create services for individual tests
            var logger = _serviceProvider.GetRequiredService<ILogger<ReminderService>>();
            _reminderService = new ReminderService(logger, _mockDataService.Object, _mockSmartFeaturesService.Object);
            
            var dataLogger = _serviceProvider.GetRequiredService<ILogger<DataService>>();
            _dataService = new DataService(dataLogger);
            
            var smartLogger = _serviceProvider.GetRequiredService<ILogger<SmartFeaturesService>>();
            _smartFeaturesService = new SmartFeaturesService(smartLogger, _mockDataService.Object);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _reminderService?.Dispose();
            (_dataService as IDisposable)?.Dispose();
            _smartFeaturesService?.Dispose();
            _serviceProvider?.GetService<ServiceProvider>()?.Dispose();
        }

        [Benchmark]
        public async Task<DateTime> ReminderService_CalculateNextReminderTime()
        {
            return await _reminderService!.CalculateNextReminderTimeAsync();
        }

        [Benchmark]
        public async Task ReminderService_RecordWaterIntake()
        {
            await _reminderService!.RecordWaterIntakeAsync(250);
        }

        [Benchmark]
        public async Task ReminderService_StartStop()
        {
            await _reminderService!.StartAsync();
            await _reminderService!.StopAsync();
        }

        [Benchmark]
        public async Task DataService_LogWaterIntake()
        {
            await _dataService!.InitializeAsync();
            await _dataService.LogWaterIntakeAsync(250, "benchmark");
        }

        [Benchmark]
        public async Task<int> DataService_GetTodaysTotal()
        {
            await _dataService!.InitializeAsync();
            return await _dataService.GetTodaysTotalIntakeAsync();
        }

        [Benchmark]
        public async Task<UserSettings> DataService_SettingsOperations()
        {
            await _dataService!.InitializeAsync();
            var settings = UserSettings.CreateDefault();
            settings.BodyWeightKilograms = 75;
            await _dataService.SaveSettingsAsync(settings);
            return await _dataService.LoadSettingsAsync();
        }

        [Benchmark]
        public int SmartFeaturesService_GetSecondsSinceLastInput()
        {
            return _smartFeaturesService!.GetSecondsSinceLastUserInput();
        }

        [Benchmark]
        public bool SmartFeaturesService_IsSystemIdle()
        {
            return _smartFeaturesService!.IsSystemIdle(10);
        }

        [Benchmark]
        public bool SmartFeaturesService_IsCircadianNightMode()
        {
            return _smartFeaturesService!.IsCircadianNightMode();
        }

        [Benchmark]
        public async Task<WeatherData?> SmartFeaturesService_GetCurrentWeather()
        {
            return await _smartFeaturesService!.GetCurrentWeatherAsync();
        }

        [Benchmark]
        public ReminderState ReminderState_CreateDefault()
        {
            return ReminderState.CreateDefault();
        }

        [Benchmark]
        public void ReminderState_RecordIntake()
        {
            var state = ReminderState.CreateDefault();
            state.RecordIntake(250);
        }

        [Benchmark]
        public void ReminderState_RecordMissedReminder()
        {
            var state = ReminderState.CreateDefault();
            state.RecordMissedReminder();
        }

        [Benchmark]
        public UserSettings UserSettings_CreateDefault()
        {
            return UserSettings.CreateDefault();
        }

        [Benchmark]
        public WaterIntakeRecord WaterIntakeRecord_Create()
        {
            return new WaterIntakeRecord(250, "benchmark");
        }
    }

    public class PerformanceConfig : ManualConfig
    {
        public PerformanceConfig()
        {
            AddJob(Job.Default
                .WithPlatform(Platform.X64)
                .WithJit(Jit.RyuJit)
                .WithGcServer(false)
                .WithGcConcurrent(true));

            AddDiagnoser(MemoryDiagnoser.Default);
            AddDiagnoser(ThreadingDiagnoser.Default);
            
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(HtmlExporter.Default);
        }
    }

    /// <summary>
    /// Startup performance tests to verify <20ms startup requirement
    /// </summary>
    public class StartupPerformanceTests
    {
        [Fact]
        public void ServiceInitialization_ShouldMeetStartupTimeRequirement()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            var services = new ServiceCollection();
            
            // Add logging with minimal overhead
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Error));
            
            // Setup mocks for dependencies
            var mockDataService = new Mock<IDataService>();
            var mockSmartFeaturesService = new Mock<ISmartFeaturesService>();
            
            mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(UserSettings.CreateDefault());
            
            // Register services
            services.AddSingleton(mockDataService.Object);
            services.AddSingleton(mockSmartFeaturesService.Object);
            services.AddSingleton<ReminderService>();
            
            // Act
            var serviceProvider = services.BuildServiceProvider();
            var reminderService = serviceProvider.GetRequiredService<ReminderService>();
            
            stopwatch.Stop();
            
            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(20, "Service initialization should be under 20ms");
            
            // Cleanup
            reminderService.Dispose();
            serviceProvider.Dispose();
        }

        [Fact]
        public async Task ColdStartup_ShouldMeetPerformanceRequirements()
        {
            // Arrange
            var process = Process.GetCurrentProcess();
            var initialMemory = process.WorkingSet64;
            var stopwatch = Stopwatch.StartNew();
            
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Error));
            
            // Setup mocks
            var mockDataService = new Mock<IDataService>();
            var mockSmartFeaturesService = new Mock<ISmartFeaturesService>();
            
            mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(UserSettings.CreateDefault());
            mockSmartFeaturesService.Setup(x => x.ShouldSmartPauseAsync())
                .ReturnsAsync(false);
            
            services.AddSingleton(mockDataService.Object);
            services.AddSingleton(mockSmartFeaturesService.Object);
            services.AddSingleton<IDataService>(sp => new DataService(sp.GetRequiredService<ILogger<DataService>>()));
            services.AddSingleton<IReminderService, ReminderService>();
            
            var serviceProvider = services.BuildServiceProvider();
            var dataService = serviceProvider.GetRequiredService<IDataService>();
            var reminderService = serviceProvider.GetRequiredService<IReminderService>();
            
            await dataService.InitializeAsync();
            await reminderService.StartAsync();
            
            stopwatch.Stop();
            var finalMemory = process.WorkingSet64;
            var memoryIncrease = (finalMemory - initialMemory) / 1024 / 1024; // Convert to MB
            
            // Assert performance requirements
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(20, "Startup should be under 20ms");
            memoryIncrease.Should().BeLessThan(40, "Memory increase should be under 40MB");
            
            // Cleanup
            await reminderService.StopAsync();
            (dataService as IDisposable)?.Dispose();
            serviceProvider.Dispose();
        }
    }

    /// <summary>
    /// Memory usage tests to verify <40MB RAM requirement
    /// </summary>
    public class MemoryPerformanceTests
    {
        [Fact]
        public async Task LongRunningService_ShouldNotLeakMemory()
        {
            // Arrange
            var process = Process.GetCurrentProcess();
            var initialMemory = process.WorkingSet64;
            
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Error));
            
            // Setup mocks
            var mockDataService = new Mock<IDataService>();
            var mockSmartFeaturesService = new Mock<ISmartFeaturesService>();
            
            mockDataService.Setup(x => x.LoadSettingsAsync())
                .ReturnsAsync(UserSettings.CreateDefault());
            mockSmartFeaturesService.Setup(x => x.ShouldSmartPauseAsync())
                .ReturnsAsync(false);
            
            services.AddSingleton(mockDataService.Object);
            services.AddSingleton(mockSmartFeaturesService.Object);
            services.AddSingleton<IDataService>(sp => new DataService(sp.GetRequiredService<ILogger<DataService>>()));
            services.AddSingleton<IReminderService, ReminderService>();
            
            var serviceProvider = services.BuildServiceProvider();
            var dataService = serviceProvider.GetRequiredService<IDataService>();
            var reminderService = serviceProvider.GetRequiredService<IReminderService>();
            
            await dataService.InitializeAsync();
            await reminderService.StartAsync();
            
            // Act - Simulate extended operation
            for (int i = 0; i < 100; i++)
            {
                await reminderService.RecordWaterIntakeAsync(250);
                await reminderService.CalculateNextReminderTimeAsync();
                
                // Force garbage collection every 10 iterations
                if (i % 10 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
            
            var finalMemory = process.WorkingSet64;
            var memoryIncrease = (finalMemory - initialMemory) / 1024 / 1024; // Convert to MB
            
            // Assert
            memoryIncrease.Should().BeLessThan(40, "Memory usage should remain under 40MB even after extended operations");
            
            // Cleanup
            await reminderService.StopAsync();
            (dataService as IDisposable)?.Dispose();
            serviceProvider.Dispose();
        }

        [Fact]
        public async Task DatabaseOperations_ShouldHaveEfficientMemoryUsage()
        {
            // Arrange
            var process = Process.GetCurrentProcess();
            var initialMemory = process.WorkingSet64;
            
            var logger = Mock.Of<ILogger<DataService>>();
            var dataService = new DataService(logger);
            
            await dataService.InitializeAsync();
            
            // Act - Perform many database operations
            for (int i = 0; i < 1000; i++)
            {
                await dataService.LogWaterIntakeAsync(200 + (i % 100), $"test{i}");
                
                if (i % 100 == 0)
                {
                    await dataService.GetTodaysTotalIntakeAsync();
                    await dataService.GetDailyIntakeAsync(DateTime.Today);
                }
            }
            
            var finalMemory = process.WorkingSet64;
            var memoryIncrease = (finalMemory - initialMemory) / 1024 / 1024; // Convert to MB
            
            // Assert
            memoryIncrease.Should().BeLessThan(20, "Database operations should use minimal additional memory");
            
            // Cleanup
            (dataService as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Throughput tests to ensure the app can handle expected load
    /// </summary>
    public class ThroughputPerformanceTests
    {
        [Fact]
        public async Task ReminderCalculations_ShouldMeetThroughputRequirements()
        {
            // Arrange
            var mockDataService = new Mock<IDataService>();
            var mockSmartFeaturesService = new Mock<ISmartFeaturesService>();
            var logger = Mock.Of<ILogger<ReminderService>>();
            
            var settings = UserSettings.CreateDefault();
            settings.BodyWeightKilograms = 75;
            
            mockDataService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);
            mockSmartFeaturesService.Setup(x => x.ShouldSmartPauseAsync()).ReturnsAsync(false);
            
            var reminderService = new ReminderService(logger, mockDataService.Object, mockSmartFeaturesService.Object);
            
            await reminderService.StartAsync();
            
            // Act - Test throughput of reminder calculations
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task>();
            
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(reminderService.CalculateNextReminderTimeAsync());
            }
            
            await Task.WhenAll(tasks);
            stopwatch.Stop();
            
            // Assert - Should complete 100 calculations in reasonable time
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Should handle 100 reminder calculations in under 1 second");
            
            // Cleanup
            await reminderService.StopAsync();
            reminderService.Dispose();
        }

        [Fact]
        public async Task DatabaseThroughput_ShouldHandleConcurrentOperations()
        {
            // Arrange
            var logger = Mock.Of<ILogger<DataService>>();
            var dataService = new DataService(logger);
            
            await dataService.InitializeAsync();
            
            // Act - Test concurrent database operations
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task>();
            
            for (int i = 0; i < 50; i++)
            {
                int amount = 200 + (i % 100);
                tasks.Add(dataService.LogWaterIntakeAsync(amount, $"concurrent{i}"));
            }
            
            await Task.WhenAll(tasks);
            stopwatch.Stop();
            
            // Assert - Should handle concurrent operations efficiently
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Should handle 50 concurrent database operations in under 5 seconds");
            
            // Verify all records were saved
            var todaysRecords = await dataService.GetDailyIntakeAsync(DateTime.Today);
            todaysRecords.Should().HaveCountGreaterOrEqualTo(50);
            
            // Cleanup
            (dataService as IDisposable)?.Dispose();
        }
    }
} 