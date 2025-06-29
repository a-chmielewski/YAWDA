using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YAWDA.Models;

namespace YAWDA.Services
{
    /// <summary>
    /// Implementation of smart water reminder features
    /// </summary>
    public class SmartFeaturesService : ISmartFeaturesService, IDisposable
    {
        private readonly ILogger<SmartFeaturesService> _logger;
        private readonly IDataService _dataService;
        private readonly HttpClient _httpClient;
        private WeatherData? _cachedWeatherData;
        private DateTime _lastWeatherUpdate = DateTime.MinValue;
        private readonly TimeSpan _weatherCacheDuration = TimeSpan.FromMinutes(30);

        // Windows API imports for system detection
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public SmartFeaturesService(ILogger<SmartFeaturesService> logger, IDataService dataService)
        {
            _logger = logger;
            _dataService = dataService;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public int GetSecondsSinceLastUserInput()
        {
            try
            {
                var lastInputInfo = new LASTINPUTINFO
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO))
                };

                if (GetLastInputInfo(ref lastInputInfo))
                {
                    var currentTickCount = GetTickCount();
                    var idleTime = (currentTickCount - lastInputInfo.dwTime) / 1000;
                    return (int)idleTime;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get last user input time");
            }

            return -1;
        }

        public bool IsSystemIdle(int idleThresholdMinutes = 10)
        {
            var idleSeconds = GetSecondsSinceLastUserInput();
            if (idleSeconds < 0) return false;

            return idleSeconds >= (idleThresholdMinutes * 60);
        }

        public async Task<WeatherData?> GetCurrentWeatherAsync()
        {
            try
            {
                // Check if cached data is still valid
                if (_cachedWeatherData != null && 
                    DateTime.Now - _lastWeatherUpdate < _weatherCacheDuration)
                {
                    return _cachedWeatherData;
                }

                // For demo purposes, we'll use a free weather API (OpenWeatherMap)
                // In production, you'd want to get user's location and API key from settings
                var settings = await _dataService.LoadSettingsAsync();
                if (!settings.EnableWeatherAdjustment)
                {
                    return null;
                }

                // Using a mock weather response for now
                // In real implementation, you'd call: http://api.openweathermap.org/data/2.5/weather
                var mockWeather = GenerateMockWeatherData();
                
                _cachedWeatherData = mockWeather;
                _lastWeatherUpdate = DateTime.Now;
                
                _logger.LogInformation("Weather data updated: {Temperature}°C, {Humidity}% humidity", 
                    mockWeather.TemperatureCelsius, mockWeather.Humidity);
                
                return mockWeather;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get weather data");
                return null;
            }
        }

        private WeatherData GenerateMockWeatherData()
        {
            // Generate realistic weather data based on season and time
            var now = DateTime.Now;
            var temp = GetSeasonalTemperature(now);
            var humidity = GetSeasonalHumidity(now);

            return new WeatherData
            {
                TemperatureCelsius = temp,
                Humidity = humidity,
                Condition = temp > 25 ? "Hot" : temp < 10 ? "Cold" : "Moderate",
                HeatIndex = CalculateHeatIndex(temp, humidity),
                LastUpdated = now
            };
        }

        private double GetSeasonalTemperature(DateTime date)
        {
            // Simple seasonal temperature model
            var dayOfYear = date.DayOfYear;
            var seasonalVariation = Math.Sin((dayOfYear - 81) * 2 * Math.PI / 365) * 15; // +/- 15°C seasonal swing
            var baseTemp = 15; // Base temperature
            var hourlyVariation = Math.Sin((date.Hour - 14) * Math.PI / 12) * 8; // Peak at 2 PM
            
            return Math.Round(baseTemp + seasonalVariation + hourlyVariation + (Random.Shared.NextDouble() - 0.5) * 4, 1);
        }

        private double GetSeasonalHumidity(DateTime date)
        {
            // Simple humidity model (inverse relationship with temperature in many climates)
            var baseHumidity = 50 + (Random.Shared.NextDouble() - 0.5) * 30;
            return Math.Max(20, Math.Min(90, Math.Round(baseHumidity, 0)));
        }

        private double CalculateHeatIndex(double tempC, double humidity)
        {
            // Convert to Fahrenheit for heat index calculation
            var tempF = tempC * 9.0 / 5.0 + 32;
            
            if (tempF < 80) return tempC;

            // Heat index formula (simplified)
            var hi = -42.379 + 2.04901523 * tempF + 10.14333127 * humidity
                   - 0.22475541 * tempF * humidity - 6.83783e-3 * tempF * tempF
                   - 5.481717e-2 * humidity * humidity + 1.22874e-3 * tempF * tempF * humidity
                   + 8.5282e-4 * tempF * humidity * humidity - 1.99e-6 * tempF * tempF * humidity * humidity;

            // Convert back to Celsius
            return Math.Round((hi - 32) * 5.0 / 9.0, 1);
        }

        public bool IsCircadianNightMode()
        {
            var now = DateTime.Now.TimeOfDay;
            
            // Reduce reminders between 6 PM and 6 AM
            var eveningStart = new TimeSpan(18, 0, 0);  // 6:00 PM
            var morningEnd = new TimeSpan(6, 0, 0);     // 6:00 AM
            
            return now >= eveningStart || now <= morningEnd;
        }

        public bool IsFocusAssistEnabled()
        {
            try
            {
                // Check Windows Focus Assist status via registry or WMI
                // For now, we'll use a heuristic based on fullscreen applications
                return IsPresentationModeActive();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check Focus Assist status");
                return false;
            }
        }

        public bool IsPresentationModeActive()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero || !IsWindow(foregroundWindow))
                    return false;

                // Get window title
                var windowText = new System.Text.StringBuilder(256);
                var length = GetWindowText(foregroundWindow, windowText, windowText.Capacity);
                
                if (length > 0)
                {
                    var title = windowText.ToString().ToLowerInvariant();
                    
                    // Check for common presentation/meeting applications
                    var presentationKeywords = new[]
                    {
                        "powerpoint", "keynote", "prezi", "slides",
                        "zoom", "teams", "skype", "meet", "webex",
                        "discord", "slack", "gotomeeting",
                        "presentation", "slideshow", "projector"
                    };

                    foreach (var keyword in presentationKeywords)
                    {
                        if (title.Contains(keyword))
                        {
                            _logger.LogDebug("Presentation mode detected: {WindowTitle}", title);
                            return true;
                        }
                    }
                }

                // Check if any fullscreen application is running
                return IsFullscreenApplicationActive();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect presentation mode");
                return false;
            }
        }

        private bool IsFullscreenApplicationActive()
        {
            try
            {
                // Simple heuristic: check if there are processes that typically run fullscreen
                var processes = Process.GetProcesses();
                var fullscreenApps = new[] { "powerpnt", "keynote", "zoom", "teams", "discord" };

                foreach (var process in processes)
                {
                    try
                    {
                        var processName = process.ProcessName.ToLowerInvariant();
                        foreach (var app in fullscreenApps)
                        {
                            if (processName.Contains(app) && !process.HasExited)
                            {
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Skip processes we can't access
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check fullscreen applications");
            }

            return false;
        }

        public double CalculateWeatherHydrationFactor(WeatherData weather)
        {
            if (weather == null) return 1.0;

            double factor = 1.0;

            // Temperature adjustments
            if (weather.TemperatureCelsius > 25) // Hot weather
            {
                factor += (weather.TemperatureCelsius - 25) * 0.02; // +2% per degree above 25°C
            }
            else if (weather.TemperatureCelsius < 15) // Cold weather (less thirst)
            {
                factor -= (15 - weather.TemperatureCelsius) * 0.01; // -1% per degree below 15°C
            }

            // Heat index adjustments (considers both temperature and humidity)
            if (weather.HeatIndex > weather.TemperatureCelsius + 3)
            {
                factor += 0.1; // +10% for high heat index
            }

            // Humidity adjustments
            if (weather.Humidity < 30) // Dry air
            {
                factor += 0.05; // +5% for dry conditions
            }

            // Ensure reasonable bounds
            return Math.Max(0.7, Math.Min(2.0, factor));
        }

        public async Task<bool> ShouldSmartPauseAsync()
        {
            try
            {
                var settings = await _dataService.LoadSettingsAsync();
                if (!settings.EnableSmartPause)
                {
                    return false;
                }

                // Check various conditions for smart pause
                
                // 1. System idle detection
                if (IsSystemIdle(15)) // 15 minutes idle
                {
                    _logger.LogDebug("Smart pause: System is idle");
                    return true;
                }

                // 2. Presentation or meeting mode
                if (IsPresentationModeActive())
                {
                    _logger.LogDebug("Smart pause: Presentation mode active");
                    return true;
                }

                // 3. Focus Assist enabled
                if (IsFocusAssistEnabled())
                {
                    _logger.LogDebug("Smart pause: Focus Assist enabled");
                    return true;
                }

                // 4. Circadian night mode (if enabled)
                if (settings.EnableCircadianAdjustment && IsCircadianNightMode())
                {
                    _logger.LogDebug("Smart pause: Circadian night mode");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to determine smart pause status");
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 