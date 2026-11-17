using System.ComponentModel.DataAnnotations;

namespace SeatSync.API.Models
{
    public class Seat
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public Event? Event { get; set; }

        public string RowLabel { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = "Available"; // Available, Locked, Booked

        public int? LockedByUserId { get; set; }
        public DateTime? LockExpiresAt { get; set; }

        [ConcurrencyCheck]
        public Guid RowVersion { get; set; } = Guid.NewGuid();
    }
}
