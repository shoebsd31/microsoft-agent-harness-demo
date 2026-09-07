using ProcurementCopilot.Application.Security;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace ProcurementCopilot.Infrastructure.Logging;

/// <summary>Serilog sink decorator that runs every property, message template and exception through <see cref="SecretRedactor"/>.</summary>
public sealed class RedactingSink : ILogEventSink, IDisposable
{
    private static readonly MessageTemplateParser Parser = new();
    private readonly ILogEventSink _inner;
    private readonly SecretRedactor _redactor;

    /// <summary>Initializes the sink.</summary>
    public RedactingSink(ILogEventSink inner, SecretRedactor redactor)
    {
        _inner = inner;
        _redactor = redactor;
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        var properties = logEvent.Properties.Select(p => new LogEventProperty(p.Key, Redact(p.Value))).ToList();
        MessageTemplate template = _redactor.ContainsSensitiveData(logEvent.MessageTemplate.Text)
            ? Parser.Parse(_redactor.Redact(logEvent.MessageTemplate.Text))
            : logEvent.MessageTemplate;
        Exception? exception = logEvent.Exception is null ? null : new RedactedException(_redactor.Redact(logEvent.Exception.ToString()));
        _inner.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, exception, template, properties));
    }

    /// <inheritdoc />
    public void Dispose() => (_inner as IDisposable)?.Dispose();

    private LogEventPropertyValue Redact(LogEventPropertyValue value) => value switch
    {
        ScalarValue { Value: string s } when _redactor.ContainsSensitiveData(s) => new ScalarValue(_redactor.Redact(s)),
        SequenceValue seq => new SequenceValue(seq.Elements.Select(Redact)),
        StructureValue st => new StructureValue(st.Properties.Select(p => new LogEventProperty(p.Name, Redact(p.Value))), st.TypeTag),
        DictionaryValue d => new DictionaryValue(d.Elements.Select(e => new KeyValuePair<ScalarValue, LogEventPropertyValue>(e.Key, Redact(e.Value)))),
        _ => value,
    };
}

/// <summary>Carries a redacted rendering of an original exception.</summary>
public sealed class RedactedException : Exception
{
    /// <summary>Initializes the exception with the redacted text.</summary>
    public RedactedException(string message) : base(message)
    {
    }

    /// <summary>Initializes an empty exception.</summary>
    public RedactedException()
    {
    }

    /// <summary>Initializes the exception with a message and inner exception.</summary>
    public RedactedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <inheritdoc />
    public override string ToString() => Message;
}
