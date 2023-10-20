using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }
        public Guid SenderId { get; set; }
        public User Sender { get; set; }
        public Guid ReceiverId { get; set; }
        public User Receiver { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
