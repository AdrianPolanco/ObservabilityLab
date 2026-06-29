using ObservabilityLab.Shared.Results;

namespace ObservabilityLab.Shared.Messaging;

/// <summary>
/// Processes a single deserialized message of type <typeparamref name="TMessage"/>.
/// Return <see cref="Result{TMessage}.Success"/> to ACK the delivery, or
/// <see cref="Result{TMessage}.Failure(Error)"/> to NACK it (the broker will requeue).
/// Unhandled exceptions thrown by the implementation are treated as a NACK with requeue.
/// </summary>
public interface IMessageHandler<TMessage> where TMessage : class
{
    Task<Result<TMessage>> HandleAsync(TMessage message, CancellationToken cancellationToken);
}
