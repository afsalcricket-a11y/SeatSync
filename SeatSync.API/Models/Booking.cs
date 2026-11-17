namespace SeatSync.API.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }

        public int SeatId { get; set; }
        public Seat? Seat { get; set; }

        public string IdempotencyKey { get; set; } = string.Empty;
        public string Status { get; set; } = "Confirmed";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
