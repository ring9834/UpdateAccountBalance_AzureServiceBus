namespace BalanceWebApi
{
    public class WithdrawRequest
    {
        public string? AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? Destination { get; set; }
    }
}
