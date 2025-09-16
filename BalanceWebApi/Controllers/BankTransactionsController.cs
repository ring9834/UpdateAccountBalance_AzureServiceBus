using AccountService;
using DataModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ServiceBusService;
using System.Runtime.InteropServices;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BalanceWebApi
{
    [ApiController]
    [Route("api/[controller]")]
    public class BankTransactionsController : ControllerBase
    {
        private readonly BankAccountService _accountService;
        private readonly BankServiceBusService _serviceBusService;
        private readonly ILogger<BankTransactionsController> _logger;

        public BankTransactionsController(
            BankAccountService accountService,
            BankServiceBusService serviceBusService,
            ILogger<BankTransactionsController> logger)
        {
            _accountService = accountService;
            _serviceBusService = serviceBusService;
            _logger = logger;
        }

        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
        {
            var message = new DepositMessage
            {
                AccountNumber = request.AccountNumber,
                Amount = request.Amount,
                Description = request.Description,
                Source = request.Source
            };

            var messageId = await _serviceBusService.SendMessageAsync(message);

            return Accepted(new
            {
                MessageId = messageId,
                Status = "Queued",
                EstimatedProcessingTime = DateTime.UtcNow.AddMinutes(5)
            });
        }

        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest request)
        {
            // Check available balance first
            using var connection = new SqlConnection(_accountService.GetConnectionString());
            var currentBalance = await connection.QueryFirstOrDefaultAsync<decimal>(
                "SELECT CurrentBalance FROM Accounts WHERE AccountNumber = @AccountNumber",
                new { request.AccountNumber }
            );

            if (currentBalance < request.Amount)
            {
                return BadRequest(new { Error = "Insufficient funds", CurrentBalance = currentBalance });
            }

            var message = new WithdrawalMessage
            {
                AccountNumber = request.AccountNumber,
                Amount = request.Amount,
                Description = request.Description,
                Destination = request.Destination
            };

            var messageId = await _serviceBusService.SendMessageAsync(message);

            return Accepted(new
            {
                MessageId = messageId,
                Status = "Queued",
                EstimatedProcessingTime = DateTime.UtcNow.AddMinutes(5)
            });
        }

        [HttpPost("process/{accountNumber}")]
        public async Task<IActionResult> ProcessAccount(string accountNumber)
        {
            await _accountService.ProcessAccountTransactionsAsync(accountNumber);
            return Ok(new { Status = "Processing completed", AccountNumber = accountNumber });
        }

        [HttpPost("process-all")]
        public async Task<IActionResult> ProcessAllAccounts()
        {
            await _accountService.ProcessAllAccountsAsync();
            return Ok(new { Status = "Batch processing completed" });
        }

        [HttpGet("queue-status")]
        public async Task<IActionResult> GetQueueStatus()
        {
            var messageCount = await _serviceBusService.GetQueueMessageCountAsync();
            var activeSessions = await _serviceBusService.GetActiveSessionsAsync();

            return Ok(new
            {
                PendingMessages = messageCount,
                ActiveAccounts = activeSessions,
                ActiveAccountCount = activeSessions.Count
            });
        }

        [HttpGet("account/{accountNumber}/status")]
        public async Task<IActionResult> GetAccountStatus(string accountNumber)
        {
            var hasPending = await _accountService.HasPendingTransactionsAsync(accountNumber);
            return Ok(new
            {
                AccountNumber = accountNumber,
                HasPendingTransactions = hasPending
            });
        }
    }
}
