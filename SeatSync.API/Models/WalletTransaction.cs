namespace SeatSync.API.Models
{
    public class WalletTransaction
    {
        public int Id { get; set; }
        public int WalletId { get; set; }
        public Wallet? Wallet { get; set; }

        public decimal Amount { get; set; }
        public string Type { get; set; } = "Debit";
        public string ReferenceKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
