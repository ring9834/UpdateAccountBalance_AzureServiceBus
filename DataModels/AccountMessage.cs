using System.Text.Json.Serialization;

namespace DataModels
{
    [JsonDerivedType(typeof(DepositMessage), "deposit")]
    [JsonDerivedType(typeof(WithdrawalMessage), "withdrawal")]
    [JsonDerivedType(typeof(InterestMessage), "interest")]
    [JsonDerivedType(typeof(FeeMessage), "fee")]
    public abstract class AccountMessage
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        public string? AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? SessionId => AccountNumber; // Use account number as session ID
    }
}
