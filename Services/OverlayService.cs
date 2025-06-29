using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YAWDA.Views;

namespace YAWDA.Services
{
    /// <summary>
    /// Service for managing progressive disruption overlays
    /// </summary>
    public class OverlayService : IOverlayService, IDisposable
    {
        private readonly ILogger<OverlayService> _logger;
        private readonly IDataService _dataService;
        
        // Overlay windows
        private Window? _bannerWindow;
        private Window? _fullScreenWindow;
        
        // Overlay controls
        private BannerOverlay? _bannerOverlay;
        private FullScreenOverlay? _fullScreenOverlay;
        
        private bool _disposed = false;

        public event EventHandler<OverlayActionEventArgs>? OverlayActionReceived;

        public bool IsOverlayVisible =>
            (_bannerWindow?.AppWindow.IsVisible == true) ||
            (_fullScreenWindow?.AppWindow.IsVisible == true);

        public OverlayService(ILogger<OverlayService> logger, IDataService dataService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        }

        /// <inheritdoc />
        public async Task ShowBannerOverlayAsync(string message, int currentIntake = 0, int dailyGoal = 2310)
        {
            try
            {
                _logger.LogDebug("Showing banner overlay: {Message}", message);

                // Hide any existing overlays first
                await HideAllOverlaysAsync();

                // Create banner window if needed
                if (_bannerWindow == null)
                {
                    _bannerWindow = CreateBannerWindow();
                }

                if (_bannerOverlay == null)
                {
                    _bannerOverlay = new BannerOverlay();
                    _bannerOverlay.ActionRequested += OnBannerActionRequested;
                    _bannerWindow.Content = _bannerOverlay;
                }

                // Update progress information
                var progressText = $"Daily progress: {currentIntake}ml / {dailyGoal}ml ({(int)((double)currentIntake / dailyGoal * 100)}%)";

                // Position window at top of screen
                PositionBannerWindow();

                // Show window and banner
                _bannerWindow.AppWindow.Show();
                _bannerWindow.Activate();
                await _bannerOverlay.ShowBannerAsync(message, progressText);

                _logger.LogInformation("Banner overlay displayed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show banner overlay");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ShowFullScreenOverlayAsync(string message, int currentIntake = 0, int dailyGoal = 2310)
        {
            try
            {
                _logger.LogDebug("Showing full-screen overlay: {Message}", message);

                // Hide any existing overlays first
                await HideAllOverlaysAsync();

                // Create full-screen window if needed
                if (_fullScreenWindow == null)
                {
                    _fullScreenWindow = CreateFullScreenWindow();
                }

                if (_fullScreenOverlay == null)
                {
                    _fullScreenOverlay = new FullScreenOverlay();
                    _fullScreenOverlay.ActionRequested += OnFullScreenActionRequested;
                    _fullScreenWindow.Content = _fullScreenOverlay;
                }

                // Position window to cover entire screen
                PositionFullScreenWindow();

                // Show window and overlay
                _fullScreenWindow.AppWindow.Show();
                _fullScreenWindow.Activate();
                await _fullScreenOverlay.ShowOverlayAsync(message, currentIntake, dailyGoal);

                _logger.LogInformation("Full-screen overlay displayed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show full-screen overlay");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task HideAllOverlaysAsync()
        {
            try
            {
                var tasks = new List<Task>();

                // Hide banner overlay
                if (_bannerOverlay != null)
                {
                    tasks.Add(_bannerOverlay.HideBannerAsync());
                }

                // Hide full-screen overlay
                if (_fullScreenOverlay != null)
                {
                    tasks.Add(_fullScreenOverlay.HideOverlayAsync());
                }

                // Wait for all hide animations to complete
                await Task.WhenAll(tasks);

                // Hide windows
                if (_bannerWindow != null)
                {
                    _bannerWindow.AppWindow.Hide();
                }

                if (_fullScreenWindow != null)
                {
                    _fullScreenWindow.AppWindow.Hide();
                }

                _logger.LogDebug("All overlays hidden");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to hide overlays");
            }
        }

        private Window CreateBannerWindow()
        {
            var window = new Window
            {
                Title = "YAWDA - Water Reminder"
            };

            // Configure window for banner overlay
            var presenter = window.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            return window;
        }

        private Window CreateFullScreenWindow()
        {
            var window = new Window
            {
                Title = "YAWDA - Hydration Break"
            };

            // Configure window for full-screen overlay
            var presenter = window.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            return window;
        }

        private void PositionBannerWindow()
        {
            if (_bannerWindow == null) return;

            try
            {
                var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
                var workArea = displayArea.WorkArea;

                // Position at top center of screen
                var windowWidth = 800;
                var windowHeight = 100;

                _bannerWindow.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32
                {
                    X = (workArea.Width - windowWidth) / 2,
                    Y = 0,
                    Width = windowWidth,
                    Height = windowHeight
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to position banner window");
            }
        }

        private void PositionFullScreenWindow()
        {
            if (_fullScreenWindow == null) return;

            try
            {
                var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
                var workArea = displayArea.WorkArea;

                // Cover entire screen
                _fullScreenWindow.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32
                {
                    X = workArea.X,
                    Y = workArea.Y,
                    Width = workArea.Width,
                    Height = workArea.Height
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to position full-screen window");
            }
        }

        private void OnBannerActionRequested(object? sender, BannerActionEventArgs e)
        {
            var overlayAction = new OverlayActionEventArgs
            {
                ActionType = e.Action switch
                {
                    BannerAction.Drink => OverlayActionType.Drink,
                    BannerAction.Snooze => OverlayActionType.Snooze,
                    BannerAction.Dismiss => OverlayActionType.Dismiss,
                    BannerAction.Timeout => OverlayActionType.Timeout,
                    _ => OverlayActionType.Dismiss
                },
                Amount = e.Amount,
                SnoozeDuration = e.SnoozeDuration,
                DisruptionLevel = 2
            };

            OverlayActionReceived?.Invoke(this, overlayAction);
        }

        private void OnFullScreenActionRequested(object? sender, FullScreenActionEventArgs e)
        {
            var overlayAction = new OverlayActionEventArgs
            {
                ActionType = e.Action switch
                {
                    FullScreenAction.Drink => OverlayActionType.Drink,
                    FullScreenAction.Snooze => OverlayActionType.Snooze,
                    FullScreenAction.Dismiss => OverlayActionType.Dismiss,
                    FullScreenAction.Timeout => OverlayActionType.Timeout,
                    _ => OverlayActionType.Dismiss
                },
                Amount = e.Amount,
                SnoozeDuration = e.SnoozeDuration,
                DisruptionLevel = 3
            };

            OverlayActionReceived?.Invoke(this, overlayAction);
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
                // Unsubscribe from events
                if (_bannerOverlay != null)
                {
                    _bannerOverlay.ActionRequested -= OnBannerActionRequested;
                }

                if (_fullScreenOverlay != null)
                {
                    _fullScreenOverlay.ActionRequested -= OnFullScreenActionRequested;
                }

                // Hide and dispose overlays
                Task.Run(async () => await HideAllOverlaysAsync());

                // Close windows
                _bannerWindow?.Close();
                _fullScreenWindow?.Close();

                _disposed = true;
            }
        }
    }
} 