using System.Transactions;

namespace DataModels
{
    public class AccountBalance
    {
        public string? AccountNumber { get; set; }
        public decimal CurrentBalance { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<Transaction> RecentTransactions { get; set; } = new List<Transaction>();
    }
}
