namespace BalanceWebApi
{
    public class DepositRequest
    {
        public string? AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? Source { get; set; }
    }
}
