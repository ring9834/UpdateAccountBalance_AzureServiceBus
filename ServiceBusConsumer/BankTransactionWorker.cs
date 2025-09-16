using AccountService;
using Microsoft.Extensions.Logging;
using ServiceBusService;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceBusConsumer
{
    public class BankTransactionWorker : BackgroundService
    {
        private readonly BankAccountService _accountService;
        private readonly BankServiceBusService _serviceBusService;
        private readonly ILogger<BankTransactionWorker> _logger;
        private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(1);

        public BankTransactionWorker(
            BankAccountService accountService,
            BankServiceBusService serviceBusService,
            ILogger<BankTransactionWorker> logger)
        {
            _accountService = accountService;
            _serviceBusService = serviceBusService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Bank Transaction Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var activeSessions = await _serviceBusService.GetActiveSessionsAsync();

                    if (activeSessions.Count > 0)
                    {
                        _logger.LogInformation("Processing {Count} active account sessions", activeSessions.Count);

                        // Process accounts with the most messages first
                        foreach (var accountNumber in activeSessions)
                        {
                            if (stoppingToken.IsCancellationRequested) break;

                            try
                            {
                                await _accountService.ProcessAccountTransactionsAsync(accountNumber);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to process account {AccountNumber}", accountNumber);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No active sessions found");
                    }

                    await Task.Delay(_processingInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in transaction worker");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }
    }
}
