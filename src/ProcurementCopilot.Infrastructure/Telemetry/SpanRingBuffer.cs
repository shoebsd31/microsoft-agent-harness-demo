using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

namespace ProcurementCopilot.Infrastructure.Telemetry;

/// <summary>A finished span as shown by the <c>/traces</c> console command.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Source">Activity source.</param>
/// <param name="Duration">Duration.</param>
/// <param name="Status">Status code.</param>
/// <param name="Operation">The <c>gen_ai.operation.name</c> tag when present.</param>
public sealed record SpanSummary(string Name, string Source, TimeSpan Duration, string Status, string? Operation);

/// <summary>Keeps the most recent spans in memory so the console can show telemetry without polluting the output.</summary>
public sealed class SpanRingBuffer
{
    private readonly ConcurrentQueue<SpanSummary> _spans = new();
    private readonly int _capacity;

    /// <summary>Initializes the buffer.</summary>
    public SpanRingBuffer(int capacity = 200) => _capacity = capacity;

    /// <summary>Gets the total number of spans recorded since start-up.</summary>
    public long TotalRecorded => Interlocked.Read(ref _total);

    private long _total;

    /// <summary>Adds a span, evicting the oldest when over capacity.</summary>
    public void Add(SpanSummary span)
    {
        _spans.Enqueue(span);
        Interlocked.Increment(ref _total);
        while (_spans.Count > _capacity && _spans.TryDequeue(out _))
        {
        }
    }

    /// <summary>Returns the newest spans first.</summary>
    public IReadOnlyList<SpanSummary> Recent(int count) => _spans.Reverse().Take(count).ToList();
}

/// <summary>Processor that feeds finished spans into a <see cref="SpanRingBuffer"/>.</summary>
public sealed class SpanRingBufferProcessor : BaseProcessor<Activity>
{
    private readonly SpanRingBuffer _buffer;

    /// <summary>Initializes the processor.</summary>
    public SpanRingBufferProcessor(SpanRingBuffer buffer) => _buffer = buffer;

    /// <inheritdoc />
    public override void OnEnd(Activity data) =>
        _buffer.Add(new SpanSummary(data.DisplayName, data.Source.Name, data.Duration, data.Status.ToString(), data.GetTagItem("gen_ai.operation.name")?.ToString()));
}
