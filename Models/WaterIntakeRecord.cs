using System;
using System.ComponentModel.DataAnnotations;

namespace YAWDA.Models
{
    /// <summary>
    /// Represents a single water intake record with timestamp and metadata
    /// </summary>
    public class WaterIntakeRecord
    {
        /// <summary>
        /// Unique identifier for the intake record
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Amount of water consumed in milliliters
        /// </summary>
        [Range(1, 2000, ErrorMessage = "Water amount must be between 1ml and 2000ml")]
        public int AmountMilliliters { get; set; }

        /// <summary>
        /// Timestamp when the water was consumed
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Source of the intake entry (manual, reminder_200ml, reminder_300ml, etc.)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Optional notes about the intake
        /// </summary>
        [MaxLength(200)]
        public string? Notes { get; set; }

        /// <summary>
        /// Date portion of the timestamp for efficient querying
        /// </summary>
        public DateTime Date => Timestamp.Date;

        /// <summary>
        /// Time portion of the timestamp for display purposes
        /// </summary>
        public TimeSpan TimeOfDay => Timestamp.TimeOfDay;

        /// <summary>
        /// Creates a new water intake record with current timestamp
        /// </summary>
        /// <param name="amountMilliliters">Amount consumed in ml</param>
        /// <param name="source">Source of the entry</param>
        /// <param name="notes">Optional notes</param>
        public WaterIntakeRecord(int amountMilliliters, string source, string? notes = null)
        {
            AmountMilliliters = amountMilliliters;
            Source = source;
            Notes = notes;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Parameterless constructor for ORM/serialization
        /// </summary>
        public WaterIntakeRecord()
        {
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Creates a water intake record with specific timestamp
        /// </summary>
        /// <param name="amountMilliliters">Amount consumed in ml</param>
        /// <param name="source">Source of the entry</param>
        /// <param name="timestamp">Specific timestamp</param>
        /// <param name="notes">Optional notes</param>
        public WaterIntakeRecord(int amountMilliliters, string source, DateTime timestamp, string? notes = null)
        {
            AmountMilliliters = amountMilliliters;
            Source = source;
            Timestamp = timestamp;
            Notes = notes;
        }

        /// <summary>
        /// Validates the water intake record
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return AmountMilliliters > 0 && 
                   AmountMilliliters <= 2000 && 
                   !string.IsNullOrWhiteSpace(Source) &&
                   Source.Length <= 50 &&
                   (Notes == null || Notes.Length <= 200);
        }

        /// <summary>
        /// Gets a formatted display string for the intake record
        /// </summary>
        /// <returns>Human-readable string representation</returns>
        public override string ToString()
        {
            return $"{AmountMilliliters}ml at {Timestamp:HH:mm} from {Source}";
        }
    }
} 