using System.Transactions;

namespace DataModels
{
    public class Transaction
    {
        public string? TransactionId { get; set; }
        public string? AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal BalanceAfterTransaction { get; set; }
    }
}
