using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using YAWDA.Models;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for managing Windows toast notifications and system tray interactions
    /// Implements progressive disruption with escalating notification levels
    /// </summary>
    public class NotificationService : INotificationService, IDisposable
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IDataService _dataService;
        private readonly ISystemTrayService _systemTrayService;
        private readonly IOverlayService _overlayService;
        private AppNotificationManager? _notificationManager;
        private readonly Queue<NotificationQueueItem> _notificationQueue = new();
        private bool _disposed = false;

        // Notification templates for different scenarios
        private const string WaterReminderTemplate = "water_reminder";
        private const string IntakeConfirmationTemplate = "intake_confirmation";
        private const string EscalatedReminderTemplate = "escalated_reminder";

        // Action identifiers for toast activation
        private const string DrinkActionPrefix = "drink_";
        private const string SnoozeAction = "snooze";
        private const string DismissAction = "dismiss";

        public event EventHandler<NotificationActionEventArgs>? NotificationActionReceived;

        public NotificationService(ILogger<NotificationService> logger, IDataService dataService, ISystemTrayService systemTrayService, IOverlayService overlayService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
            _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
        }

        /// <summary>
        /// Initializes the notification system and registers activation handlers
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing NotificationService...");

                // Initialize the AppNotificationManager
                _notificationManager = AppNotificationManager.Default;
                
                // Register for notification activation events
                _notificationManager.NotificationInvoked += OnNotificationInvoked;
                _notificationManager.Register();

                // Register notification templates
                await RegisterNotificationTemplatesAsync();

                // Subscribe to overlay events
                _overlayService.OverlayActionReceived += OnOverlayActionReceived;

                _logger.LogInformation("NotificationService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize NotificationService");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ShowWaterReminderAsync(string message, int escalationLevel = 1)
        {
            if (_notificationManager == null)
                throw new InvalidOperationException("NotificationService not initialized. Call InitializeAsync first.");

            try
            {
                var queueItem = new NotificationQueueItem
                {
                    Type = NotificationType.WaterReminder,
                    Message = message,
                    EscalationLevel = Math.Clamp(escalationLevel, 1, 4),
                    Timestamp = DateTime.Now
                };

                await QueueNotificationAsync(queueItem);
                _logger.LogDebug("Water reminder queued: Level {Level}, Message: {Message}", escalationLevel, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show water reminder: {Message}", message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ShowIntakeConfirmationAsync(int amount)
        {
            if (_notificationManager == null)
                throw new InvalidOperationException("NotificationService not initialized. Call InitializeAsync first.");

            try
            {
                var currentIntake = await _dataService.GetTodaysTotalIntakeAsync();
                var settings = await _dataService.LoadSettingsAsync();
                var progress = (double)currentIntake / settings.EffectiveDailyGoalMilliliters * 100;

                var message = $"Great! You've logged {amount}ml. Daily progress: {currentIntake}ml ({progress:F0}%)";

                var notification = new AppNotificationBuilder()
                    .AddText("Water Logged Successfully! 💧")
                    .AddText(message)
                    .AddText($"Keep it up! Goal: {settings.EffectiveDailyGoalMilliliters}ml")
                    .SetAudioUri(new Uri("ms-winsoundevent:Notification.Default"))
                    .BuildNotification();

                                 notification.Tag = $"confirmation_{DateTime.Now.Ticks}";
                 notification.Group = "YAWDA_Confirmations";
 
                 _notificationManager.Show(notification);
                _logger.LogDebug("Intake confirmation shown: {Amount}ml, Total: {Total}ml", amount, currentIntake);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show intake confirmation for {Amount}ml", amount);
                throw;
            }
        }

        /// <inheritdoc />
        public void UpdateTrayTooltip(int currentIntake, int dailyGoal)
        {
            try
            {
                var nextReminderTime = GetNextReminderTime();
                _systemTrayService.UpdateTooltip(currentIntake, dailyGoal, nextReminderTime);
                _logger.LogDebug("Tray tooltip updated: {CurrentIntake}ml / {DailyGoal}ml", currentIntake, dailyGoal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tray tooltip");
            }
        }

        /// <inheritdoc />
        public async Task ShowEscalatedReminderAsync(int level, string message)
        {
            if (_notificationManager == null)
                throw new InvalidOperationException("NotificationService not initialized. Call InitializeAsync first.");

            try
            {
                // Get current progress for overlay displays
                var currentIntake = await _dataService.GetTodaysTotalIntakeAsync();
                var settings = await _dataService.LoadSettingsAsync();
                var dailyGoal = settings.EffectiveDailyGoalMilliliters;

                switch (level)
                {
                    case 1:
                        // Standard toast notification
                        await ShowStandardReminderToastAsync(message, level);
                        break;
                    case 2:
                        // Banner overlay with enhanced toast fallback
                        try
                        {
                            await _overlayService.ShowBannerOverlayAsync(message, currentIntake, dailyGoal);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Banner overlay failed, falling back to enhanced toast");
                            await ShowEnhancedReminderToastAsync(message, level);
                        }
                        break;
                    case 3:
                        // Full-screen overlay with persistent toast fallback
                        try
                        {
                            await _overlayService.ShowFullScreenOverlayAsync(message, currentIntake, dailyGoal);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Full-screen overlay failed, falling back to persistent toast");
                            await ShowPersistentReminderToastAsync(message, level);
                        }
                        break;
                    case 4:
                        // High-priority toast with system attention
                        await ShowHighPriorityReminderToastAsync(message, level);
                        break;
                    default:
                        await ShowStandardReminderToastAsync(message, 1);
                        break;
                }

                _logger.LogDebug("Escalated reminder shown: Level {Level}, Message: {Message}", level, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show escalated reminder: Level {Level}", level);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task InitializeTrayIconAsync()
        {
            try
            {
                _logger.LogInformation("Initializing tray icon integration...");
                await _systemTrayService.InitializeAsync();
                _logger.LogInformation("Tray icon integration initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize tray icon integration");
                throw;
            }
        }

        /// <summary>
        /// Queues a notification for processing
        /// </summary>
        private async Task QueueNotificationAsync(NotificationQueueItem item)
        {
            _notificationQueue.Enqueue(item);
            await ProcessNotificationQueueAsync();
        }

        /// <summary>
        /// Processes the notification queue
        /// </summary>
        private async Task ProcessNotificationQueueAsync()
        {
            while (_notificationQueue.Count > 0)
            {
                var item = _notificationQueue.Dequeue();
                await ShowNotificationFromQueueItemAsync(item);
            }
        }

        /// <summary>
        /// Shows a notification from a queue item
        /// </summary>
        private async Task ShowNotificationFromQueueItemAsync(NotificationQueueItem item)
        {
            switch (item.Type)
            {
                case NotificationType.WaterReminder:
                    await ShowEscalatedReminderAsync(item.EscalationLevel, item.Message);
                    break;
                case NotificationType.IntakeConfirmation:
                    await ShowIntakeConfirmationAsync(item.Amount);
                    break;
                default:
                    _logger.LogWarning("Unknown notification type: {Type}", item.Type);
                    break;
            }
        }

        /// <summary>
        /// Shows a standard level 1 reminder toast
        /// </summary>
        private Task ShowStandardReminderToastAsync(string message, int level)
        {
            var notification = new AppNotificationBuilder()
                .AddText("💧 Time for Water!")
                .AddText(message)
                .AddText("Stay hydrated, stay productive!")
                .AddButton(new AppNotificationButton("250ml")
                    .AddArgument("action", $"{DrinkActionPrefix}250"))
                .AddButton(new AppNotificationButton("500ml")
                    .AddArgument("action", $"{DrinkActionPrefix}500"))
                .AddButton(new AppNotificationButton("Snooze 15 min")
                    .AddArgument("action", SnoozeAction))
                .SetAudioUri(new Uri("ms-winsoundevent:Notification.Default"))
                .BuildNotification();

                         notification.Tag = $"water_reminder_{DateTime.Now.Ticks}";
             notification.Group = "YAWDA_Reminders";
 
             _notificationManager!.Show(notification);
             return Task.CompletedTask;
        }

        /// <summary>
        /// Shows an enhanced level 2 reminder toast
        /// </summary>
        private Task ShowEnhancedReminderToastAsync(string message, int level)
        {
            var notification = new AppNotificationBuilder()
                .AddText("💧 WATER REMINDER - Enhanced")
                .AddText(message)
                .AddText("You've missed a few reminders. Time to hydrate!")
                .AddButton(new AppNotificationButton("Quick 200ml")
                    .AddArgument("action", $"{DrinkActionPrefix}200"))
                .AddButton(new AppNotificationButton("Standard 350ml")
                    .AddArgument("action", $"{DrinkActionPrefix}350"))
                .AddButton(new AppNotificationButton("Large 500ml")
                    .AddArgument("action", $"{DrinkActionPrefix}500"))
                .AddButton(new AppNotificationButton("Snooze 10 min")
                    .AddArgument("action", SnoozeAction))
                .SetAudioUri(new Uri("ms-winsoundevent:Notification.Reminder"))
                .BuildNotification();

                         notification.Tag = $"enhanced_reminder_{DateTime.Now.Ticks}";
             notification.Group = "YAWDA_Reminders";
             notification.Priority = AppNotificationPriority.High;
 
             _notificationManager!.Show(notification);
             return Task.CompletedTask;
        }

        /// <summary>
        /// Shows a persistent level 3 reminder toast
        /// </summary>
        private Task ShowPersistentReminderToastAsync(string message, int level)
        {
            var notification = new AppNotificationBuilder()
                .AddText("💧⚠️ URGENT WATER REMINDER")
                .AddText(message)
                .AddText("Dehydration affects your performance. Please drink water now!")
                .AddButton(new AppNotificationButton("Drink 250ml NOW")
                    .AddArgument("action", $"{DrinkActionPrefix}250"))
                .AddButton(new AppNotificationButton("Drink 400ml")
                    .AddArgument("action", $"{DrinkActionPrefix}400"))
                .AddButton(new AppNotificationButton("Large Glass 600ml")
                    .AddArgument("action", $"{DrinkActionPrefix}600"))
                                 .AddButton(new AppNotificationButton("Snooze 5 min")
                     .AddArgument("action", SnoozeAction))
                 .SetAudioUri(new Uri("ms-winsoundevent:Notification.IM"))
                 .BuildNotification();
 
             notification.Tag = $"persistent_reminder_{DateTime.Now.Ticks}";
             notification.Group = "YAWDA_Reminders";
             notification.Priority = AppNotificationPriority.High;
 
             _notificationManager!.Show(notification);
             return Task.CompletedTask;
        }

        /// <summary>
        /// Shows a high-priority level 4 reminder toast
        /// </summary>
        private Task ShowHighPriorityReminderToastAsync(string message, int level)
        {
            var notification = new AppNotificationBuilder()
                .AddText("🚨💧 CRITICAL HYDRATION ALERT")
                .AddText(message)
                .AddText("Immediate action required! Your health depends on hydration.")
                .AddButton(new AppNotificationButton("🚨 DRINK 300ml NOW")
                    .AddArgument("action", $"{DrinkActionPrefix}300"))
                .AddButton(new AppNotificationButton("🚨 DRINK 500ml")
                    .AddArgument("action", $"{DrinkActionPrefix}500"))
                                 .AddButton(new AppNotificationButton("🚨 LARGE 750ml")
                     .AddArgument("action", $"{DrinkActionPrefix}750"))
                 .SetAudioUri(new Uri("ms-winsoundevent:Notification.Looping.Alarm"))
                 .BuildNotification();

            notification.Tag = $"critical_reminder_{DateTime.Now.Ticks}";
            notification.Group = "YAWDA_Reminders";
            notification.Priority = AppNotificationPriority.High;

            _notificationManager!.Show(notification);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Registers notification templates with the system
        /// </summary>
        private Task RegisterNotificationTemplatesAsync()
        {
            try
            {
                // Templates are defined in the notification building methods
                // This method is reserved for future template registration if needed
                _logger.LogDebug("Notification templates registered");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register notification templates");
                throw;
            }
        }

        /// <summary>
        /// Handles notification activation events (user clicks on buttons)
        /// </summary>
        private void OnNotificationInvoked(object? sender, AppNotificationActivatedEventArgs e)
        {
            try
            {
                var arguments = e.Arguments;
                
                if (arguments.TryGetValue("action", out var action))
                {
                    _logger.LogDebug("Notification action received: {Action}", action);

                    if (action.StartsWith(DrinkActionPrefix))
                    {
                        // Extract amount from action (e.g., "drink_250" -> 250)
                        var amountStr = action.Substring(DrinkActionPrefix.Length);
                        if (int.TryParse(amountStr, out var amount))
                        {
                            HandleDrinkAction(amount);
                        }
                    }
                    else if (action == SnoozeAction)
                    {
                        HandleSnoozeAction();
                    }
                    else if (action == DismissAction)
                    {
                        HandleDismissAction();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notification activation");
            }
        }

        /// <summary>
        /// Handles the drink water action from notifications
        /// </summary>
        private void HandleDrinkAction(int amount)
        {
            try
            {
                var eventArgs = new NotificationActionEventArgs
                {
                    Action = "drink",
                    Amount = amount,
                    Timestamp = DateTime.Now
                };

                NotificationActionReceived?.Invoke(this, eventArgs);
                _logger.LogInformation("Drink action handled: {Amount}ml", amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling drink action: {Amount}ml", amount);
            }
        }

        /// <summary>
        /// Handles the snooze action from notifications
        /// </summary>
        private void HandleSnoozeAction()
        {
            try
            {
                var eventArgs = new NotificationActionEventArgs
                {
                    Action = "snooze",
                    Amount = 0,
                    Timestamp = DateTime.Now
                };

                NotificationActionReceived?.Invoke(this, eventArgs);
                _logger.LogInformation("Snooze action handled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling snooze action");
            }
        }

        /// <summary>
        /// Handles the dismiss action from notifications
        /// </summary>
        private void HandleDismissAction()
        {
            try
            {
                var eventArgs = new NotificationActionEventArgs
                {
                    Action = "dismiss",
                    Amount = 0,
                    Timestamp = DateTime.Now
                };

                NotificationActionReceived?.Invoke(this, eventArgs);
                _logger.LogInformation("Dismiss action handled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling dismiss action");
            }
        }

        /// <summary>
        /// Handles overlay action events and forwards them as notification actions
        /// </summary>
        private async void OnOverlayActionReceived(object? sender, OverlayActionEventArgs e)
        {
            try
            {
                _logger.LogDebug("Overlay action received: {ActionType}, Level: {Level}", e.ActionType, e.DisruptionLevel);

                switch (e.ActionType)
                {
                    case OverlayActionType.Drink:
                        // Log water intake and notify
                        await _dataService.LogWaterIntakeAsync(e.Amount, $"reminder_level_{e.DisruptionLevel}");
                        NotificationActionReceived?.Invoke(this, new NotificationActionEventArgs
                        {
                            Action = "drink",
                            Amount = e.Amount,
                            Timestamp = DateTime.Now
                        });
                        break;

                    case OverlayActionType.Snooze:
                        NotificationActionReceived?.Invoke(this, new NotificationActionEventArgs
                        {
                            Action = "snooze",
                            Timestamp = DateTime.Now
                        });
                        break;

                    case OverlayActionType.Dismiss:
                    case OverlayActionType.Timeout:
                        NotificationActionReceived?.Invoke(this, new NotificationActionEventArgs
                        {
                            Action = "dismiss",
                            Timestamp = DateTime.Now
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle overlay action: {ActionType}", e.ActionType);
            }
        }

        /// <summary>
        /// Gets the next reminder time for tray tooltip
        /// </summary>
        private string GetNextReminderTime()
        {
            try
            {
                // For now, return a placeholder - this will be enhanced when we integrate with ReminderService
                return "calculating...";
            }
            catch
            {
                return "Unknown";
            }
        }

                 /// <summary>
         /// Clears all notifications from the specified group
         /// </summary>
         public async Task ClearNotificationGroupAsync(string group)
         {
             try
             {
                 if (_notificationManager != null)
                 {
                     await _notificationManager.RemoveByGroupAsync(group);
                     _logger.LogDebug("Cleared notification group: {Group}", group);
                 }
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Failed to clear notification group: {Group}", group);
             }
         }

        /// <summary>
        /// Clears all YAWDA notifications
        /// </summary>
        public async Task ClearAllNotificationsAsync()
        {
            try
            {
                if (_notificationManager != null)
                {
                    await _notificationManager.RemoveAllAsync();
                    _logger.LogDebug("Cleared all notifications");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear all notifications");
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
                try
                {
                    if (_notificationManager != null)
                    {
                        _notificationManager.NotificationInvoked -= OnNotificationInvoked;
                        _notificationManager.Unregister();
                        _notificationManager = null;
                    }

                    // Unsubscribe from overlay events
                    _overlayService.OverlayActionReceived -= OnOverlayActionReceived;

                    _notificationQueue.Clear();
                    _logger.LogDebug("NotificationService disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during NotificationService disposal");
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents a queued notification item
    /// </summary>
    internal class NotificationQueueItem
    {
        public NotificationType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public int EscalationLevel { get; set; } = 1;
        public int Amount { get; set; } = 0;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Types of notifications
    /// </summary>
    internal enum NotificationType
    {
        WaterReminder,
        IntakeConfirmation,
        EscalatedReminder
    }
} 