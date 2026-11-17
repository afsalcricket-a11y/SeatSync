using System.ComponentModel.DataAnnotations;

namespace SeatSync.API.Models
{
    public class Wallet
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }

        public decimal Balance { get; set; } = 5000m;

        [ConcurrencyCheck]
        public Guid RowVersion { get; set; } = Guid.NewGuid();

        public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
    }
}
