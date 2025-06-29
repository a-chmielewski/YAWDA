using FluentAssertions;
using YAWDA.Models;
using Xunit;

namespace YAWDA.Tests.Models
{
    public class ReminderStateTests
    {
        [Fact]
        public void CreateDefault_ShouldInitializeWithCorrectDefaults()
        {
            // Act
            var state = ReminderState.CreateDefault();

            // Assert
            state.CurrentEscalationLevel.Should().Be(1);
            state.ConsecutiveMissedReminders.Should().Be(0);
            state.ConsecutiveComplianceStreak.Should().Be(0);
            state.CurrentIntervalMinutes.Should().Be(60);
            state.IsPaused.Should().BeFalse();
            state.TodayRemindersShown.Should().Be(0);
            state.TodayRemindersComplied.Should().Be(0);
            state.WeeklyAdherenceRate.Should().Be(0.5);
            state.SevenDayAverageCompliance.Should().Be(0.5);
            state.LastIntakeTimestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void RecordIntake_ShouldResetEscalationAndUpdateStreaks()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.CurrentEscalationLevel = 3;
            state.ConsecutiveMissedReminders = 2;
            state.ConsecutiveComplianceStreak = 0;

            // Act
            state.RecordIntake(250);

            // Assert
            state.CurrentEscalationLevel.Should().Be(1);
            state.ConsecutiveMissedReminders.Should().Be(0);
            state.ConsecutiveComplianceStreak.Should().Be(1);
            state.TodayRemindersComplied.Should().Be(1);
            state.LastIntakeTimestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void RecordIntake_WithMultipleCompliance_ShouldIncreaseInterval()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.CurrentIntervalMinutes = 60;
            state.ConsecutiveComplianceStreak = 2;

            // Act
            state.RecordIntake(250);

            // Assert
            state.ConsecutiveComplianceStreak.Should().Be(3);
            state.CurrentIntervalMinutes.Should().Be(65); // Increased by 5 minutes
        }

        [Fact]
        public void RecordIntake_ShouldNotExceedMaxInterval()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.CurrentIntervalMinutes = 115;
            state.ConsecutiveComplianceStreak = 2;

            // Act
            state.RecordIntake(250);

            // Assert
            state.CurrentIntervalMinutes.Should().Be(120); // Capped at 120
        }

        [Fact]
        public void RecordMissedReminder_ShouldEscalateAndUpdateCounters()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.CurrentEscalationLevel = 1;
            state.ConsecutiveMissedReminders = 0;
            state.ConsecutiveComplianceStreak = 3;

            // Act
            state.RecordMissedReminder();

            // Assert
            state.ConsecutiveMissedReminders.Should().Be(1);
            state.ConsecutiveComplianceStreak.Should().Be(0);
            state.TodayRemindersShown.Should().Be(1);
            state.CurrentEscalationLevel.Should().Be(1); // Not escalated yet
        }

        [Fact]
        public void RecordMissedReminder_WithMultipleMisses_ShouldEscalateLevel()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.ConsecutiveMissedReminders = 1;

            // Act
            state.RecordMissedReminder();

            // Assert
            state.ConsecutiveMissedReminders.Should().Be(2);
            state.CurrentEscalationLevel.Should().Be(2); // Escalated
        }

        [Fact]
        public void RecordMissedReminder_ShouldNotExceedMaxEscalationLevel()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.CurrentEscalationLevel = 4;
            state.ConsecutiveMissedReminders = 1;

            // Act
            state.RecordMissedReminder();

            // Assert
            state.CurrentEscalationLevel.Should().Be(4); // Capped at 4
        }

        [Fact]
        public void RecordMissedReminder_WithMultipleMisses_ShouldDecreaseInterval()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.CurrentIntervalMinutes = 60;
            state.ConsecutiveMissedReminders = 1;

            // Act
            state.RecordMissedReminder();

            // Assert
            state.CurrentIntervalMinutes.Should().Be(50); // Decreased by 10 minutes
        }

        [Fact]
        public void RecordMissedReminder_ShouldNotGobelowMinInterval()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.CurrentIntervalMinutes = 25;
            state.ConsecutiveMissedReminders = 1;

            // Act
            state.RecordMissedReminder();

            // Assert
            state.CurrentIntervalMinutes.Should().Be(20); // Capped at minimum 20
        }

        [Fact]
        public void PauseReminders_ShouldSetPauseState()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            var duration = TimeSpan.FromMinutes(30);
            var reason = "Meeting";

            // Act
            state.PauseReminders(duration, reason);

            // Assert
            state.IsPaused.Should().BeTrue();
            state.PauseDuration.Should().Be(duration);
            state.PauseReason.Should().Be(reason);
            state.PausedTimestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void ResumeReminders_ShouldClearPauseState()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.PauseReminders(TimeSpan.FromMinutes(30), "Test");

            // Act
            state.ResumeReminders();

            // Assert
            state.IsPaused.Should().BeFalse();
            state.PauseDuration.Should().BeNull();
            state.PauseReason.Should().BeNull();
            state.PausedTimestamp.Should().BeNull();
        }

        [Fact]
        public void IsPauseExpired_WhenNotPaused_ShouldReturnFalse()
        {
            // Arrange
            var state = ReminderState.CreateDefault();

            // Act & Assert
            state.IsPauseExpired.Should().BeFalse();
        }

        [Fact]
        public void IsPauseExpired_WhenPausedButNotEmpty_ShouldReturnFalse()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.PauseReminders(TimeSpan.FromMinutes(30), "Test");

            // Act & Assert
            state.IsPauseExpired.Should().BeFalse();
        }

        [Fact]
        public void IsPauseExpired_WhenPauseTimeExpired_ShouldReturnTrue()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.IsPaused = true;
            state.PausedTimestamp = DateTime.Now.AddMinutes(-60);
            state.PauseDuration = TimeSpan.FromMinutes(30);

            // Act & Assert
            state.IsPauseExpired.Should().BeTrue();
        }

        [Fact]
        public void TodayComplianceRate_WithNoReminders_ShouldReturn100Percent()
        {
            // Arrange
            var state = ReminderState.CreateDefault();

            // Act & Assert
            state.TodayComplianceRate.Should().Be(1.0);
        }

        [Fact]
        public void TodayComplianceRate_WithPartialCompliance_ShouldCalculateCorrectly()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.TodayRemindersShown = 4;
            state.TodayRemindersComplied = 3;

            // Act & Assert
            state.TodayComplianceRate.Should().Be(0.75);
        }

        [Fact]
        public void MinutesSinceLastIntake_ShouldCalculateCorrectly()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.LastIntakeTimestamp = DateTime.Now.AddMinutes(-45);

            // Act & Assert
            state.MinutesSinceLastIntake.Should().BeApproximately(45, 1);
        }

        [Fact]
        public void MinutesUntilNextReminder_ShouldCalculateCorrectly()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.NextReminderScheduled = DateTime.Now.AddMinutes(30);

            // Act & Assert
            state.MinutesUntilNextReminder.Should().BeApproximately(30, 1);
        }

        [Fact]
        public void IsReminderOverdue_WhenOverdue_ShouldReturnTrue()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.NextReminderScheduled = DateTime.Now.AddMinutes(-5);

            // Act & Assert
            state.IsReminderOverdue.Should().BeTrue();
        }

        [Fact]
        public void IsReminderOverdue_WhenNotOverdue_ShouldReturnFalse()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.NextReminderScheduled = DateTime.Now.AddMinutes(5);

            // Act & Assert
            state.IsReminderOverdue.Should().BeFalse();
        }

        [Fact]
        public void UpdateWeeklyAdherence_ShouldUpdateCorrectly()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            var newComplianceRate = 0.8;

            // Act
            state.UpdateWeeklyAdherence(newComplianceRate);

            // Assert
            state.WeeklyAdherenceRate.Should().BeApproximately(0.65, 0.01); // Weighted average
        }

        [Fact]
        public void ResetDailyCounters_ShouldResetCounters()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.TodayRemindersShown = 5;
            state.TodayRemindersComplied = 3;

            // Act
            state.ResetDailyCounters();

            // Assert
            state.TodayRemindersShown.Should().Be(0);
            state.TodayRemindersComplied.Should().Be(0);
        }

        [Fact]
        public void NeedsAttention_WithHighMissedReminders_ShouldReturnTrue()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.ConsecutiveMissedReminders = 3;

            // Act & Assert
            state.NeedsAttention().Should().BeTrue();
        }

        [Fact]
        public void NeedsAttention_WithLowCompliance_ShouldReturnTrue()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.WeeklyAdherenceRate = 0.2;

            // Act & Assert
            state.NeedsAttention().Should().BeTrue();
        }

        [Fact]
        public void NeedsAttention_WithGoodStatus_ShouldReturnFalse()
        {
            // Arrange
            var state = ReminderState.CreateDefault();
            state.ConsecutiveMissedReminders = 1;
            state.WeeklyAdherenceRate = 0.8;

            // Act & Assert
            state.NeedsAttention().Should().BeFalse();
        }
    }
} 