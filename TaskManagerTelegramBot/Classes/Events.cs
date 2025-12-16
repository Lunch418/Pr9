using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManagerTelegramBot.Classes
{
    public class Event
    {
        [Key]
        public int Id { get; set; }

        public DateTime Time { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsCompleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("User")]
        public long UserId { get; set; }
        public virtual User? User { get; set; } 

        public Event() { } 

        public Event(DateTime time, string message)
        {
            Time = time;
            Message = message;
        }
    }
}