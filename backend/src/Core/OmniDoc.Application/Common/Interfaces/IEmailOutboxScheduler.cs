namespace OmniDoc.Application.Common.Interfaces;

public interface IEmailOutboxScheduler
{
    void Enqueue(Guid outboxMessageId);
}
