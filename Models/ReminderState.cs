using System;
using System.ComponentModel.DataAnnotations;

namespace YAWDA.Models
{
    /// <summary>
    /// Tracks the current state of the reminder system including escalation and adherence
    /// </summary>
    public class ReminderState
    {
        /// <summary>
        /// Timestamp of the last water intake recorded
        /// </summary>
        public DateTime LastIntakeTimestamp { get; set; }

        /// <summary>
        /// Timestamp when the next reminder is scheduled
        /// </summary>
        public DateTime NextReminderScheduled { get; set; }

        /// <summary>
        /// Current escalation level (1-4)
        /// </summary>
        [Range(1, 4)]
        public int CurrentEscalationLevel { get; set; } = 1;

        /// <summary>
        /// Number of consecutive reminders missed
        /// </summary>
        public int ConsecutiveMissedReminders { get; set; } = 0;

        /// <summary>
        /// Number of consecutive reminders complied with
        /// </summary>
        public int ConsecutiveComplianceStreak { get; set; } = 0;

        /// <summary>
        /// Current reminder interval in minutes (adaptive)
        /// </summary>
        [Range(15, 180)]
        public int CurrentIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Whether reminders are currently paused
        /// </summary>
        public bool IsPaused { get; set; } = false;

        /// <summary>
        /// Timestamp when reminders were paused (if applicable)
        /// </summary>
        public DateTime? PausedTimestamp { get; set; }

        /// <summary>
        /// Duration for which reminders are paused
        /// </summary>
        public TimeSpan? PauseDuration { get; set; }

        /// <summary>
        /// Reason for pausing (user request, system idle, focus assist, etc.)
        /// </summary>
        [MaxLength(100)]
        public string? PauseReason { get; set; }

        /// <summary>
        /// Total reminders shown today
        /// </summary>
        public int TodayRemindersShown { get; set; } = 0;

        /// <summary>
        /// Total reminders complied with today
        /// </summary>
        public int TodayRemindersComplied { get; set; } = 0;

        /// <summary>
        /// Last time the state was updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// Weekly adherence rate (0.0 to 1.0)
        /// </summary>
        [Range(0.0, 1.0)]
        public double WeeklyAdherenceRate { get; set; } = 0.5;

        /// <summary>
        /// Average daily compliance over the last 7 days
        /// </summary>
        [Range(0.0, 1.0)]
        public double SevenDayAverageCompliance { get; set; } = 0.5;

        /// <summary>
        /// Time since last intake in minutes
        /// </summary>
        public double MinutesSinceLastIntake => (DateTime.Now - LastIntakeTimestamp).TotalMinutes;

        /// <summary>
        /// Time until next reminder in minutes
        /// </summary>
        public double MinutesUntilNextReminder => (NextReminderScheduled - DateTime.Now).TotalMinutes;

        /// <summary>
        /// Today's compliance rate (reminders complied / reminders shown)
        /// </summary>
        public double TodayComplianceRate => TodayRemindersShown == 0 ? 1.0 : (double)TodayRemindersComplied / TodayRemindersShown;

        /// <summary>
        /// Whether the pause period has expired
        /// </summary>
        public bool IsPauseExpired
        {
            get
            {
                if (!IsPaused || PausedTimestamp == null || PauseDuration == null)
                    return false;
                
                return DateTime.Now >= PausedTimestamp.Value.Add(PauseDuration.Value);
            }
        }

        /// <summary>
        /// Whether a reminder is overdue based on current interval
        /// </summary>
        public bool IsReminderOverdue => DateTime.Now >= NextReminderScheduled;

        /// <summary>
        /// Records a successful water intake, resetting escalation
        /// </summary>
        /// <param name="amount">Amount of water consumed in ml</param>
        public void RecordIntake(int amount)
        {
            LastIntakeTimestamp = DateTime.Now;
            CurrentEscalationLevel = 1;
            ConsecutiveMissedReminders = 0;
            ConsecutiveComplianceStreak++;
            TodayRemindersComplied++;
            
            // Adaptive interval adjustment - reward compliance with longer intervals
            if (ConsecutiveComplianceStreak >= 3)
            {
                CurrentIntervalMinutes = Math.Min(CurrentIntervalMinutes + 5, 120);
            }
            
            ScheduleNextReminder();
            UpdateLastModified();
        }

        /// <summary>
        /// Records a missed or dismissed reminder, escalating if necessary
        /// </summary>
        public void RecordMissedReminder()
        {
            ConsecutiveMissedReminders++;
            ConsecutiveComplianceStreak = 0;
            TodayRemindersShown++;
            
            // Escalate disruption level
            if (ConsecutiveMissedReminders >= 2)
            {
                CurrentEscalationLevel = Math.Min(CurrentEscalationLevel + 1, 4);
            }
            
            // Adaptive interval adjustment - shorten interval for missed reminders
            if (ConsecutiveMissedReminders >= 2)
            {
                CurrentIntervalMinutes = Math.Max(CurrentIntervalMinutes - 10, 20);
            }
            
            ScheduleNextReminder();
            UpdateLastModified();
        }

        /// <summary>
        /// Pauses reminders for the specified duration
        /// </summary>
        /// <param name="duration">Duration to pause</param>
        /// <param name="reason">Reason for pausing</param>
        public void PauseReminders(TimeSpan duration, string reason = "User request")
        {
            IsPaused = true;
            PausedTimestamp = DateTime.Now;
            PauseDuration = duration;
            PauseReason = reason;
            UpdateLastModified();
        }

        /// <summary>
        /// Resumes reminders if paused
        /// </summary>
        public void ResumeReminders()
        {
            IsPaused = false;
            PausedTimestamp = null;
            PauseDuration = null;
            PauseReason = null;
            ScheduleNextReminder();
            UpdateLastModified();
        }

        /// <summary>
        /// Schedules the next reminder based on current interval and escalation
        /// </summary>
        private void ScheduleNextReminder()
        {
            var intervalMinutes = CurrentIntervalMinutes;
            
            // Adjust interval based on escalation level
            switch (CurrentEscalationLevel)
            {
                case 2:
                    intervalMinutes = (int)(intervalMinutes * 0.8); // 20% shorter
                    break;
                case 3:
                    intervalMinutes = (int)(intervalMinutes * 0.6); // 40% shorter
                    break;
                case 4:
                    intervalMinutes = (int)(intervalMinutes * 0.5); // 50% shorter
                    break;
            }
            
            NextReminderScheduled = DateTime.Now.AddMinutes(intervalMinutes);
        }

        /// <summary>
        /// Updates the weekly adherence rate based on recent performance
        /// </summary>
        /// <param name="recentComplianceRate">Recent compliance rate to factor in</param>
        public void UpdateWeeklyAdherence(double recentComplianceRate)
        {
            // Weighted average: 70% existing rate, 30% recent performance
            WeeklyAdherenceRate = (WeeklyAdherenceRate * 0.7) + (recentComplianceRate * 0.3);
            WeeklyAdherenceRate = Math.Max(0.0, Math.Min(1.0, WeeklyAdherenceRate));
            UpdateLastModified();
        }

        /// <summary>
        /// Resets daily counters for a new day
        /// </summary>
        public void ResetDailyCounters()
        {
            TodayRemindersShown = 0;
            TodayRemindersComplied = 0;
            UpdateLastModified();
        }

        /// <summary>
        /// Updates the last modified timestamp
        /// </summary>
        private void UpdateLastModified()
        {
            LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Creates a new reminder state with default values
        /// </summary>
        /// <returns>Default reminder state</returns>
        public static ReminderState CreateDefault()
        {
            var state = new ReminderState
            {
                LastIntakeTimestamp = DateTime.Now.AddHours(-1), // Assume last intake 1 hour ago
                CurrentIntervalMinutes = 60
            };
            
            state.ScheduleNextReminder();
            return state;
        }

        /// <summary>
        /// Checks if the reminder system needs attention (overdue or escalated)
        /// </summary>
        /// <returns>True if needs attention</returns>
        public bool NeedsAttention()
        {
            return IsReminderOverdue || CurrentEscalationLevel >= 3 || ConsecutiveMissedReminders >= 2;
        }
    }
} 