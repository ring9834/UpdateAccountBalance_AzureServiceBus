using DataModels;
using Microsoft.Extensions.Logging;
using ServiceBusService;
using Microsoft.Data.SqlClient;
using Dapper;

namespace AccountService
{
    public class BankAccountService
    {
        private readonly BankServiceBusService _serviceBusService;
        private readonly string _dbConnectionString;
        private readonly ILogger<BankAccountService> _logger;

        public BankAccountService(BankServiceBusService serviceBusService, string dbConnectionString, ILogger<BankAccountService> logger)
        {
            _serviceBusService = serviceBusService;
            _dbConnectionString = dbConnectionString;
            _logger = logger;
        }

        public string GetConnectionString()
        {
            return this._dbConnectionString;
        }

        public async Task ProcessAccountTransactionsAsync(string accountNumber)
        {
            _logger.LogInformation("Processing transactions for account {AccountNumber}", accountNumber);

            await _serviceBusService.ProcessAccountSessionAsync(accountNumber, async message =>
            {
                using var connection = new SqlConnection(_dbConnectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Get current balance with rowlock to prevent concurrent updates
                    var currentBalance = await connection.QueryFirstOrDefaultAsync<decimal>(
                        "SELECT CurrentBalance FROM Accounts WITH (ROWLOCK) WHERE AccountNumber = @AccountNumber",
                        new { AccountNumber = accountNumber },
                        transaction
                    );

                    decimal newBalance = message switch
                    {
                        DepositMessage deposit => currentBalance + deposit.Amount,
                        WithdrawalMessage withdrawal => currentBalance - withdrawal.Amount,
                        InterestMessage interest => currentBalance + interest.Amount,
                        FeeMessage fee => currentBalance - fee.Amount,
                        _ => throw new InvalidOperationException($"Unknown message type: {message.GetType().Name}")
                    };

                    // Update account balance
                    await connection.ExecuteAsync(
                        @"UPDATE Accounts 
                      SET CurrentBalance = @NewBalance, 
                          LastUpdated = GETUTCDATE()
                      WHERE AccountNumber = @AccountNumber",
                        new { NewBalance = newBalance, AccountNumber = accountNumber },
                        transaction
                    );

                    // Record transaction
                    await connection.ExecuteAsync(
                        @"INSERT INTO Transactions 
                      (TransactionId, AccountNumber, Amount, Type, Description, Timestamp, BalanceAfterTransaction)
                      VALUES (@TransactionId, @AccountNumber, @Amount, @Type, @Description, @Timestamp, @BalanceAfterTransaction)",
                        new
                        {
                            TransactionId = message.MessageId,
                            AccountNumber = accountNumber,
                            message.Amount,
                            Type = message.GetType().Name.Replace("Message", "").ToUpper(),
                            message.Description,
                            Timestamp = DateTime.UtcNow,
                            BalanceAfterTransaction = newBalance
                        },
                        transaction
                    );

                    await transaction.CommitAsync();

                    _logger.LogInformation("Processed {TransactionType} of {Amount} for account {AccountNumber}. New balance: {NewBalance}",
                        message.GetType().Name, message.Amount, accountNumber, newBalance);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Failed to process transaction for account {AccountNumber}", accountNumber);
                    throw;
                }
            });
        }

        public async Task ProcessAllAccountsAsync()
        {
            var activeSessions = await _serviceBusService.GetActiveSessionsAsync();

            _logger.LogInformation("Found {Count} active account sessions to process", activeSessions.Count);

            var processingTasks = activeSessions.Select(sessionId =>
                ProcessAccountTransactionsAsync(sessionId)
            ).ToArray();

            await Task.WhenAll(processingTasks);
        }

        public async Task ScheduleDailyInterestAsync()
        {
            using var connection = new SqlConnection(_dbConnectionString);

            var savingsAccounts = await connection.QueryAsync<AccountBalance>(
                "SELECT AccountNumber, CurrentBalance FROM Accounts WHERE AccountType = 'SAVINGS'"
            );

            foreach (var account in savingsAccounts)
            {
                decimal dailyInterest = account.CurrentBalance * 0.0001m; // 0.01% daily interest

                var interestMessage = new InterestMessage
                {
                    AccountNumber = account.AccountNumber,
                    Amount = dailyInterest,
                    Description = "Daily interest accrual",
                    InterestRate = 0.0001m,
                    Period = "DAILY"
                };

                await _serviceBusService.SendMessageAsync(interestMessage);
            }
        }

        public async Task<bool> HasPendingTransactionsAsync(string accountNumber)
        {
            var activeSessions = await _serviceBusService.GetActiveSessionsAsync();
            return activeSessions.Contains(accountNumber);
        }
    }
}
