using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManagerTelegramBot.Classes
{
    public class RepeatEvent
    {
        [Key]
        public int Id { get; set; }

        public string DaysString { get; set; } = string.Empty;
        public TimeSpan Time { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSent { get; set; }

        [ForeignKey("User")]
        public long UserId { get; set; }
        public virtual User? User { get; set; }

        [NotMapped]
        public List<DayOfWeek> Days
        {
            get => DaysString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(d => (DayOfWeek)int.Parse(d))
                           .ToList();
            set => DaysString = string.Join(",", value.Select(d => ((int)d).ToString()));
        }

        public RepeatEvent() { }

        public RepeatEvent(List<DayOfWeek> days, TimeSpan time, string message)
        {
            Days = days;
            Time = time;
            Message = message;
        }
    }
}