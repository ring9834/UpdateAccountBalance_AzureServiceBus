# UpdateAccountBalance_AzureServiceBus
Here we implement users' bank account updates by using Azure Service bus's queque messaging functionalities (Non-Pub/Sub) with the use of sessions, dead letter, duplicate detection.

Azure Service Bus is a more robust, enterprise-grade messaging service supporting advanced messaging patterns like publish/subscribe, topics, subscriptions, and dead-letter queues. It offers features like guaranteed message delivery, message ordering, duplicate detection, and session-based processing, making it suitable for complex, reliable, and scalable messaging scenarios in distributed systems.

As opposed to Azure Storage Queue, Service Bus's added latency is a worthwhile trade-off for scenarios needing guaranteed ordering, at-least-once delivery, or integration with other Azure services.
