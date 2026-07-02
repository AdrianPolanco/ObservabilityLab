using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;
using System.Text;

namespace ObservabilityLab.Shared.Observability;

/// <summary>
/// Bridges W3C Trace Context across RabbitMQ.
/// <para>
/// Over HTTP, a downstream service picks up the caller's trace automatically because ASP.NET Core's
/// instrumentation reads the "traceparent" request header for you. RabbitMQ has no such built-in
/// behavior — a message is just bytes plus a header dictionary, and there's no ambient
/// <see cref="Activity.Current"/> on the consumer side. Without deliberately carrying the trace-id /
/// span-id across that hop, every consumer would start a brand new, disconnected trace per message,
/// and the Api → OrderProcessingWorker → InvoiceWorker → EmailWorker pipeline would show up in Tempo
/// as four unrelated traces instead of one end-to-end waterfall.
/// </para>
/// <para>
/// <see cref="RabbitMqPublisher"/> calls <see cref="Inject"/> right before publishing; <see cref="RabbitMqConsumer{TMessage}"/>
/// calls <see cref="Extract"/> right after receiving, then starts its span as a child of the extracted
/// context. This is the same Inject/Extract pattern OTel's own HTTP instrumentation uses under the hood
/// — we're just doing it by hand because RabbitMQ has no auto-instrumentation for it.
/// </para>
/// </summary>
public static class MessagingTraceContext
{
    // Propagators.DefaultTextMapPropagator is set once, at startup, in ObservabilityExtensions.AddObservability
    // via Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator()) — the W3C "traceparent" format.
    // Reading it from the same static accessor here (rather than constructing our own) guarantees the
    // publisher and consumer always agree on the wire format.
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    /// <summary>
    /// Serializes the current Activity's trace context into the outgoing message's headers.
    /// Call this after opening the Producer span (so <see cref="Activity.Current"/> is the span itself)
    /// and before <c>BasicPublishAsync</c>.
    /// </summary>
    public static void Inject(IDictionary<string, object?> headers)
    {
        var contextToInject = Activity.Current?.Context ?? default;

        Propagator.Inject(
            new PropagationContext(contextToInject, Baggage.Current),
            headers,
            static (carrier, key, value) => carrier[key] = value);
    }

    /// <summary>
    /// Reads a trace context back out of an inbound message's headers. Returns a default/empty
    /// <see cref="ActivityContext"/> — i.e. "no parent, start a fresh trace" — if the message carries no
    /// (valid) trace headers, e.g. because it was published before this instrumentation existed.
    /// </summary>
    public static ActivityContext Extract(IDictionary<string, object?>? headers)
    {
        if (headers is null)
            return default;

        var propagationContext = Propagator.Extract(
            default,
            headers,
            static (carrier, key) =>
            {
                if (!carrier.TryGetValue(key, out var value) || value is null)
                    return Enumerable.Empty<string>();

                // RabbitMQ.Client round-trips header values as byte[] on the wire (unlike HTTP headers,
                // which are already strings), so they need decoding before the propagator can parse them.
                var text = value switch
                {
                    byte[] bytes => Encoding.UTF8.GetString(bytes),
                    string s => s,
                    _ => value.ToString() ?? string.Empty
                };

                return [text];
            });

        return propagationContext.ActivityContext;
    }
}
