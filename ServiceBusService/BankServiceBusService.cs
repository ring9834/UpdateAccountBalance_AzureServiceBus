using System.Collections.Immutable;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using DataModels;
using Microsoft.Extensions.Logging;

namespace ServiceBusService
{
    public class BankServiceBusService
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusAdministrationClient _adminClient;
        private readonly ServiceBusSender _sender;
        private readonly string _queueName = "bank-transactions";
        private readonly ILogger<BankServiceBusService> _logger;

        public BankServiceBusService(string connectionString, ILogger<BankServiceBusService> logger)
        {
            _logger = logger;
            _serviceBusClient = new ServiceBusClient(connectionString);
            _adminClient = new ServiceBusAdministrationClient(connectionString);

            // Create sender
            _sender = _serviceBusClient.CreateSender(_queueName);

            InitializeQueueAsync().Wait();
        }

        private async Task InitializeQueueAsync()
        {
            try
            {
                if (!await _adminClient.QueueExistsAsync(_queueName))
                {
                    var options = new CreateQueueOptions(_queueName)
                    {
                        RequiresDuplicateDetection = true,
                        DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10),
                        RequiresSession = true,
                        DeadLetteringOnMessageExpiration = true,
                        MaxDeliveryCount = 5
                    };

                    await _adminClient.CreateQueueAsync(options);
                    _logger.LogInformation("Service Bus queue created: {QueueName}", _queueName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Service Bus queue");
                throw;
            }
        }

        public async Task<string> SendMessageAsync(AccountMessage message)
        {
            try
            {
                var messageBody = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var serviceBusMessage = new ServiceBusMessage(messageBody)
                {
                    MessageId = message.MessageId,
                    SessionId = message.SessionId,
                    ContentType = "application/json",
                    ApplicationProperties =
                {
                    ["MessageType"] = message.GetType().Name,
                    ["AccountNumber"] = message.AccountNumber,
                    ["Timestamp"] = message.Timestamp
                }
                };

                await _sender.SendMessageAsync(serviceBusMessage);

                _logger.LogInformation("Message sent for account {AccountNumber}, MessageId: {MessageId}",
                    message.AccountNumber, message.MessageId);

                return message.MessageId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message for account {AccountNumber}", message.AccountNumber);
                throw;
            }
        }

        public async Task<List<AccountMessage>> ReceiveMessagesAsync(string sessionId, int maxMessages = 10, TimeSpan? maxWaitTime = null)
        {
            var messages = new List<AccountMessage>();
            maxWaitTime ??= TimeSpan.FromSeconds(30);

             using var receiver = _serviceBusClient.AcceptSessionAsync(
                _queueName,
                sessionId,
                new ServiceBusSessionReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }
            );

            try
            {
                var receivedMessages = await receiver.Result.ReceiveMessagesAsync(maxMessages, maxWaitTime.Value);

                foreach (var message in receivedMessages)
                {
                    try
                    {
                        var messageBody = message.Body.ToString();
                        var accountMessage = JsonSerializer.Deserialize<AccountMessage>(messageBody,
                            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                        messages.Add(accountMessage);

                        // Complete message to remove from queue
                        await receiver.Result.CompleteMessageAsync(message);

                        _logger.LogDebug("Processed message {MessageId} for session {SessionId}",
                            message.MessageId, sessionId);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Invalid message format for message {MessageId}", message.MessageId);
                        await receiver.Result.DeadLetterMessageAsync(message, "Invalid format", ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
                        await receiver.Result.AbandonMessageAsync(message);
                        throw;
                    }
                }
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.SessionCannotBeLocked)
            {
                _logger.LogWarning("Session {SessionId} is locked by another receiver", sessionId);
            }

            return messages;
        }

        public async Task ProcessAccountSessionAsync(string accountNumber, Func<AccountMessage, Task> processor)
        {
            await using var receiver = await _serviceBusClient.AcceptSessionAsync(
                _queueName,
                accountNumber,
                new ServiceBusSessionReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }
            );

            var messages = await receiver.ReceiveMessagesAsync(maxMessages: 20, maxWaitTime: TimeSpan.FromSeconds(5));

            foreach (var message in messages)
            {
                try
                {
                    var messageBody = message.Body.ToString();
                    var accountMessage = JsonSerializer.Deserialize<AccountMessage>(messageBody);

                    await processor(accountMessage);
                    await receiver.CompleteMessageAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
                    await receiver.AbandonMessageAsync(message);
                }
            }
        }

        public async Task<int> GetQueueMessageCountAsync()
        {
            var queueProperties = await _adminClient.GetQueueRuntimePropertiesAsync(_queueName);
            return (int)queueProperties.Value.ActiveMessageCount;
        }

        public Task<List<string>> GetActiveSessionsAsync()
        {
            var sessions = new List<string>();
            //var sessionPageable = _adminClient.GetQueueSessionsAsync(_queueName);

            //await foreach (var session in sessionPageable)
            //{
            //    sessions.Add(session.SessionId);
            //}

            return null;
        }

        public void Dispose()
        {
            _sender?.DisposeAsync().AsTask().Wait();
            _serviceBusClient?.DisposeAsync().AsTask().Wait();
        }
    }
}
