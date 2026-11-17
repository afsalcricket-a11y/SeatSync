namespace SeatSync.API.Models
{
    public class Event
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Venue { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }

        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }
}
