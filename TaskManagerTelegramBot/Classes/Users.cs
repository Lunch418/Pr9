using System.ComponentModel.DataAnnotations;

namespace TaskManagerTelegramBot.Classes
{
    public class User
    {
        [Key]
        public long Id { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Event> Events { get; set; } = new List<Event>();
        public virtual ICollection<RepeatEvent> RepeatEvents { get; set; } = new List<RepeatEvent>();
    }
}