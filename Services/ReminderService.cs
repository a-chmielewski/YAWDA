using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YAWDA.Models;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for managing adaptive water reminder scheduling with progressive escalation
    /// Implements behavioral science principles for optimal compliance
    /// </summary>
    public class ReminderService : IReminderService, IDisposable
    {
        private readonly ILogger<ReminderService> _logger;
        private readonly IDataService _dataService;
        private Timer? _reminderTimer;
        private ReminderState _currentState;
        private UserSettings _currentSettings;
        private bool _disposed = false;
        private readonly object _stateLock = new object();

        // Performance optimization: Cache frequently accessed values
        private DateTime _lastSystemIdleCheck = DateTime.MinValue;
        private bool _lastSystemIdleState = false;

        public event EventHandler<ReminderEventArgs>? ReminderTriggered;

        public bool IsRunning { get; private set; } = false;
        public bool IsPaused => _currentState?.IsPaused ?? false;

        public ReminderService(ILogger<ReminderService> logger, IDataService dataService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            
            // Initialize with defaults
            _currentState = ReminderState.CreateDefault();
            _currentSettings = UserSettings.CreateDefault();
        }

        /// <summary>
        /// Starts the reminder service with adaptive scheduling
        /// </summary>
        public async Task StartAsync()
        {
            if (IsRunning)
            {
                _logger.LogWarning("ReminderService is already running");
                return;
            }

            try
            {
                // Load current state and settings
                await LoadCurrentStateAsync();
                await LoadCurrentSettingsAsync();

                // Reset daily counters if it's a new day
                CheckAndResetDailyCounters();

                // Calculate next reminder time
                var nextReminderTime = await CalculateNextReminderTimeAsync();
                
                // Schedule the first reminder
                ScheduleReminder(nextReminderTime);
                
                IsRunning = true;
                _logger.LogInformation("ReminderService started successfully. Next reminder at {NextReminder}", 
                    nextReminderTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start ReminderService");
                throw;
            }
        }

        /// <summary>
        /// Stops the reminder service
        /// </summary>
        public async Task StopAsync()
        {
            if (!IsRunning)
            {
                _logger.LogWarning("ReminderService is not running");
                return;
            }

            try
            {
                // Stop the timer
                _reminderTimer?.Dispose();
                _reminderTimer = null;
                
                // Save current state
                await SaveCurrentStateAsync();
                
                IsRunning = false;
                _logger.LogInformation("ReminderService stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping ReminderService");
                throw;
            }
        }

        /// <summary>
        /// Pauses reminders for a specified duration
        /// </summary>
        public async Task PauseAsync(TimeSpan duration)
        {
            lock (_stateLock)
            {
                _currentState.PauseReminders(duration, "User request");
            }

            await SaveCurrentStateAsync();
            _logger.LogInformation("Reminders paused for {Duration} minutes", duration.TotalMinutes);
        }

        /// <summary>
        /// Resumes reminders if paused
        /// </summary>
        public async Task ResumeAsync()
        {
            lock (_stateLock)
            {
                if (_currentState.IsPaused)
                {
                    _currentState.ResumeReminders();
                }
            }

            if (IsRunning)
            {
                var nextReminderTime = await CalculateNextReminderTimeAsync();
                ScheduleReminder(nextReminderTime);
            }

            await SaveCurrentStateAsync();
            _logger.LogInformation("Reminders resumed");
        }

        /// <summary>
        /// Records that user drank water (resets escalation)
        /// </summary>
        public async Task RecordWaterIntakeAsync(int amount)
        {
            lock (_stateLock)
            {
                _currentState.RecordIntake(amount);
            }

            // Schedule next reminder with updated state
            if (IsRunning)
            {
                var nextReminderTime = await CalculateNextReminderTimeAsync();
                ScheduleReminder(nextReminderTime);
            }

            await SaveCurrentStateAsync();
            _logger.LogDebug("Water intake recorded: {Amount}ml. Next reminder at {NextReminder}", 
                amount, _currentState.NextReminderScheduled);
        }

        /// <summary>
        /// Records that user dismissed/ignored a reminder
        /// </summary>
        public async Task RecordReminderDismissedAsync()
        {
            lock (_stateLock)
            {
                _currentState.RecordMissedReminder();
            }

            // Reschedule with escalated urgency
            if (IsRunning)
            {
                var nextReminderTime = await CalculateNextReminderTimeAsync();
                ScheduleReminder(nextReminderTime);
            }

            await SaveCurrentStateAsync();
            _logger.LogDebug("Reminder dismissed. Escalation level: {Level}, Next reminder at {NextReminder}", 
                _currentState.CurrentEscalationLevel, _currentState.NextReminderScheduled);
        }

        /// <summary>
        /// Gets the current reminder state
        /// </summary>
        public async Task<ReminderState> GetCurrentStateAsync()
        {
            await LoadCurrentStateAsync();
            return _currentState;
        }

        /// <summary>
        /// Calculates the next reminder time based on current adherence and settings
        /// </summary>
        public Task<DateTime> CalculateNextReminderTimeAsync()
        {
            var now = DateTime.Now;
            var baseInterval = TimeSpan.FromMinutes(_currentState.CurrentIntervalMinutes);

            // Apply escalation multiplier
            var escalationMultiplier = GetEscalationMultiplier(_currentState.CurrentEscalationLevel);
            var adjustedInterval = TimeSpan.FromMinutes(baseInterval.TotalMinutes * escalationMultiplier);

            // Respect work hours if configured
            var nextTime = now.Add(adjustedInterval);
            nextTime = AdjustForWorkHours(nextTime);

            // Ensure minimum interval of 15 minutes
            if (nextTime <= now.AddMinutes(15))
            {
                nextTime = now.AddMinutes(15);
            }

            return Task.FromResult(nextTime);
        }

        /// <summary>
        /// Updates reminder frequency based on user settings
        /// </summary>
        public async Task UpdateSettingsAsync(UserSettings settings)
        {
            _currentSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            
            // Update current interval based on settings
            lock (_stateLock)
            {
                _currentState.CurrentIntervalMinutes = settings.BaseReminderIntervalMinutes;
            }

            // Reschedule if running
            if (IsRunning)
            {
                var nextReminderTime = await CalculateNextReminderTimeAsync();
                ScheduleReminder(nextReminderTime);
            }

            await SaveCurrentStateAsync();
            _logger.LogInformation("Reminder settings updated. New interval: {Interval} minutes", 
                settings.BaseReminderIntervalMinutes);
        }

        /// <summary>
        /// Checks if reminders should be paused based on system state
        /// </summary>
        public async Task<bool> ShouldPauseForSystemStateAsync()
        {
            var now = DateTime.Now;
            
            // Cache system idle checks for performance (check max once per minute)
            if ((now - _lastSystemIdleCheck).TotalMinutes < 1.0)
            {
                return _lastSystemIdleState;
            }

            try
            {
                _lastSystemIdleCheck = now;
                
                // Check if system is in focus assist mode (Do Not Disturb)
                var focusAssistEnabled = await IsFocusAssistEnabledAsync();
                
                // Check if user is in a meeting or presentation
                var inPresentationMode = await IsInPresentationModeAsync();
                
                // Check if system has been idle for extended period
                var systemIdle = await IsSystemIdleAsync();
                
                _lastSystemIdleState = focusAssistEnabled || inPresentationMode || systemIdle;
                return _lastSystemIdleState;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking system state for reminder pausing");
                return false;
            }
        }

        /// <summary>
        /// Timer callback for reminder processing
        /// </summary>
        private async void OnReminderTimer(object? state)
        {
            try
            {
                // Check if reminders should be paused due to system state
                if (await ShouldPauseForSystemStateAsync())
                {
                    // Reschedule for later
                    var nextCheck = DateTime.Now.AddMinutes(10);
                    ScheduleReminder(nextCheck);
                    _logger.LogDebug("Reminder postponed due to system state");
                    return;
                }

                // Check if manually paused and expired
                if (_currentState.IsPaused)
                {
                    if (_currentState.IsPauseExpired)
                    {
                        await ResumeAsync();
                    }
                    else
                    {
                        // Still paused, check again later
                        var resumeTime = _currentState.PausedTimestamp!.Value.Add(_currentState.PauseDuration!.Value);
                        ScheduleReminder(resumeTime);
                        return;
                    }
                }

                // Check if within work hours
                if (!IsWithinWorkHours(DateTime.Now))
                {
                    var nextWorkStart = GetNextWorkHourStart();
                    ScheduleReminder(nextWorkStart);
                    _logger.LogDebug("Reminder postponed until work hours: {NextWorkStart}", nextWorkStart);
                    return;
                }

                // Trigger the reminder
                await TriggerReminderAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in reminder timer callback");
                
                // Reschedule after error
                var nextRetry = DateTime.Now.AddMinutes(5);
                ScheduleReminder(nextRetry);
            }
        }

        /// <summary>
        /// Triggers a reminder event with appropriate escalation
        /// </summary>
        private async Task TriggerReminderAsync()
        {
            var reminderArgs = new ReminderEventArgs
            {
                Message = GenerateReminderMessage(),
                EscalationLevel = _currentState.CurrentEscalationLevel,
                ScheduledTime = _currentState.NextReminderScheduled,
                TimeSinceLastIntake = TimeSpan.FromMinutes(_currentState.MinutesSinceLastIntake)
            };

            // Fire the event
            ReminderTriggered?.Invoke(this, reminderArgs);

            // Update state
            lock (_stateLock)
            {
                _currentState.TodayRemindersShown++;
            }

            // Schedule auto-escalation if user doesn't respond
            ScheduleAutoEscalation();

            await SaveCurrentStateAsync();
            _logger.LogInformation("Reminder triggered. Level: {Level}, Message: {Message}", 
                reminderArgs.EscalationLevel, reminderArgs.Message);
        }

        /// <summary>
        /// Schedules automatic escalation if user doesn't respond
        /// </summary>
        private void ScheduleAutoEscalation()
        {
            var escalationDelay = GetEscalationDelay(_currentState.CurrentEscalationLevel);
            var escalationTime = DateTime.Now.Add(escalationDelay);
            
            // Use a separate timer for escalation
            var escalationTimer = new Timer(async _ =>
            {
                try
                {
                    await RecordReminderDismissedAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in auto-escalation");
                }
            }, null, escalationDelay, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Generates context-appropriate reminder message
        /// </summary>
        private string GenerateReminderMessage()
        {
            var timeSinceLastIntake = _currentState.MinutesSinceLastIntake;
            var escalationLevel = _currentState.CurrentEscalationLevel;

            return escalationLevel switch
            {
                1 when timeSinceLastIntake < 90 => "Time for a hydration break! 💧",
                1 => "Remember to drink some water 🚰",
                2 when timeSinceLastIntake < 120 => "Your body needs water - take a quick sip! 💙",
                2 => "Hydration reminder: Please drink water soon 🥤",
                3 when timeSinceLastIntake < 150 => "Important: You haven't had water in a while! ⚠️💧",
                3 => "Health Alert: Please prioritize hydration now! 🚨💙",
                4 => "URGENT: Extended dehydration detected - drink water immediately! 🆘🚰",
                _ => "Time to hydrate! 💧"
            };
        }

        /// <summary>
        /// Gets escalation multiplier for timing adjustment
        /// </summary>
        private double GetEscalationMultiplier(int escalationLevel)
        {
            return escalationLevel switch
            {
                1 => 1.0,    // Normal interval
                2 => 0.75,   // 25% faster
                3 => 0.5,    // 50% faster
                4 => 0.25,   // 75% faster (most urgent)
                _ => 1.0
            };
        }

        /// <summary>
        /// Gets delay before auto-escalation
        /// </summary>
        private TimeSpan GetEscalationDelay(int escalationLevel)
        {
            return escalationLevel switch
            {
                1 => TimeSpan.FromMinutes(10), // Give user 10 minutes to respond
                2 => TimeSpan.FromMinutes(7),  // 7 minutes
                3 => TimeSpan.FromMinutes(5),  // 5 minutes
                4 => TimeSpan.FromMinutes(3),  // 3 minutes (most urgent)
                _ => TimeSpan.FromMinutes(10)
            };
        }

        /// <summary>
        /// Schedules the reminder timer
        /// </summary>
        private void ScheduleReminder(DateTime nextReminderTime)
        {
            // Dispose existing timer
            _reminderTimer?.Dispose();

            var delay = nextReminderTime - DateTime.Now;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.FromMinutes(1); // Minimum 1 minute delay
            }

            // Update state
            lock (_stateLock)
            {
                _currentState.NextReminderScheduled = nextReminderTime;
            }

            // Create new timer
            _reminderTimer = new Timer(OnReminderTimer, null, delay, Timeout.InfiniteTimeSpan);
            
            _logger.LogDebug("Next reminder scheduled for {NextReminder} (in {Delay} minutes)", 
                nextReminderTime, delay.TotalMinutes);
        }

        /// <summary>
        /// Adjusts reminder time to respect work hours
        /// </summary>
        private DateTime AdjustForWorkHours(DateTime proposedTime)
        {
            if (IsWithinWorkHours(proposedTime))
                return proposedTime;

            // If outside work hours, schedule for next work day start
            return GetNextWorkHourStart();
        }

        /// <summary>
        /// Checks if the given time is within configured work hours
        /// </summary>
        private bool IsWithinWorkHours(DateTime time)
        {
            var timeOfDay = time.TimeOfDay;
            return timeOfDay >= _currentSettings.WorkHoursStart && timeOfDay <= _currentSettings.WorkHoursEnd;
        }

        /// <summary>
        /// Gets the next work hour start time
        /// </summary>
        private DateTime GetNextWorkHourStart()
        {
            var now = DateTime.Now;
            var today = now.Date;
            var todayWorkStart = today.Add(_currentSettings.WorkHoursStart);

            if (now < todayWorkStart)
                return todayWorkStart;

            // Next work day (assuming tomorrow, could be enhanced for weekends)
            return today.AddDays(1).Add(_currentSettings.WorkHoursStart);
        }

        /// <summary>
        /// Checks and resets daily counters if it's a new day
        /// </summary>
        private void CheckAndResetDailyCounters()
        {
            var lastUpdateDate = _currentState.LastUpdated.Date;
            var today = DateTime.Now.Date;

            if (lastUpdateDate < today)
            {
                lock (_stateLock)
                {
                    _currentState.ResetDailyCounters();
                }
                _logger.LogDebug("Daily counters reset for new day");
            }
        }

        /// <summary>
        /// Loads current reminder state from persistence
        /// </summary>
        private Task LoadCurrentStateAsync()
        {
            try
            {
                // For now, create default state. In future iterations, we could persist this
                _currentState = ReminderState.CreateDefault();
                _logger.LogDebug("Reminder state loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load reminder state, using defaults");
                _currentState = ReminderState.CreateDefault();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Loads current user settings
        /// </summary>
        private async Task LoadCurrentSettingsAsync()
        {
            try
            {
                _currentSettings = await _dataService.LoadSettingsAsync();
                _logger.LogDebug("User settings loaded for reminder service");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load user settings, using defaults");
                _currentSettings = UserSettings.CreateDefault();
            }
        }

        /// <summary>
        /// Saves current reminder state to persistence
        /// </summary>
        private Task SaveCurrentStateAsync()
        {
            try
            {
                // For now, we'll just log. In future iterations, we could persist reminder state
                _logger.LogDebug("Reminder state saved");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save reminder state");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks if Focus Assist (Do Not Disturb) is enabled
        /// </summary>
        private async Task<bool> IsFocusAssistEnabledAsync()
        {
            // Placeholder for Windows Focus Assist detection
            // Implementation would use Windows APIs to check notification settings
            await Task.CompletedTask;
            return false;
        }

        /// <summary>
        /// Checks if system is in presentation mode
        /// </summary>
        private async Task<bool> IsInPresentationModeAsync()
        {
            // Placeholder for presentation mode detection
            // Implementation would check for fullscreen apps, presentation mode registry, etc.
            await Task.CompletedTask;
            return false;
        }

        /// <summary>
        /// Checks if system has been idle for extended period
        /// </summary>
        private async Task<bool> IsSystemIdleAsync()
        {
            // Placeholder for system idle detection
            // Implementation would use GetLastInputInfo or similar Windows APIs
            await Task.CompletedTask;
            return false;
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
                _reminderTimer?.Dispose();
                _disposed = true;
                _logger.LogDebug("ReminderService disposed");
            }
        }
    }
} 