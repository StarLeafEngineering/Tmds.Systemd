using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Tmds.Systemd.Logging
{
    class JournalLogger : ILogger
    {
        internal static readonly Action<Exception, JournalMessage> DefaultExceptionFormatter =
            (exception, message) => FormatException(exception, message);

        internal static readonly JournalFieldName Logger = "LOGGER";
        internal static readonly JournalFieldName EventId = "EVENTID";
        internal static readonly JournalFieldName Exception = "EXCEPTION";
        internal static readonly JournalFieldName ExceptionType = "EXCEPTION_TYPE";
        internal static readonly JournalFieldName ExceptionStackTrace = "EXCEPTION_STACKTRACE";
        internal static readonly JournalFieldName InnerException = "INNEREXCEPTION";
        internal static readonly JournalFieldName InnerExceptionType = "INNEREXCEPTION_TYPE";
        internal static readonly JournalFieldName InnerExceptionStackTrace = "INNEREXCEPTION_STACKTRACE";
        internal const string OriginalFormat = "{OriginalFormat}";

        private readonly LogFlags _additionalFlags;
        private readonly string   _syslogIdentifier;
        private readonly Action<Exception, JournalMessage> _exceptionFormatter;
        private readonly Dictionary<string, string> _extraFields;
        private readonly bool _includeScopesInMessage;
        private readonly bool _includeExceptionInfoInMessage;

        internal JournalLogger(string name, IExternalScopeProvider scopeProvider, JournalLoggerOptions options)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            ScopeProvider = scopeProvider;
            if (options.DropWhenBusy)
            {
                _additionalFlags |= LogFlags.DropWhenBusy;
            }
            _syslogIdentifier = options.SyslogIdentifier;
            _additionalFlags |= LogFlags.DontAppendSyslogIdentifier;
            _exceptionFormatter = options.ExceptionFormatter;
            _extraFields = options.ExtraFields;
            _includeScopesInMessage = options.IncludeScopesInMessage;
            _includeExceptionInfoInMessage = options.IncludeExceptionInfoInMessage;
        }

        internal IExternalScopeProvider ScopeProvider { get; set; }

        public string Name { get; }

        public IDisposable BeginScope<TState>(TState state) => ScopeProvider?.Push(state) ?? NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel == LogLevel.None)
            {
                return false;
            }

            return Journal.IsSupported;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            string message = formatter(state, exception);

            LogFlags flags = LogFlags.None;
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    flags = LogFlags.Debug; break;
                case LogLevel.Information:
                    flags = LogFlags.Information; break;
                case LogLevel.Warning:
                    flags = LogFlags.Warning; break;
                case LogLevel.Error:
                    flags = LogFlags.Error; break;
                case LogLevel.Critical:
                    flags = LogFlags.Critical; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel));
            }
            flags |= _additionalFlags;

            if (!string.IsNullOrEmpty(message) || exception != null)
            {
                using (var logMessage = Journal.GetMessage())
                {
                    if (_syslogIdentifier != null)
                    {
                        logMessage.Append(JournalFieldName.SyslogIdentifier, _syslogIdentifier);
                    }
                    logMessage.Append(Logger, Name);
                    foreach (var field in _extraFields)
                    {
                        logMessage.Append(field.Key, field.Value);
                    }
                    if (eventId.Id != 0 || eventId.Name != null)
                    {
                        logMessage.Append(EventId, eventId.Id);
                    }
                    if (exception != null)
                    {
                        _exceptionFormatter?.Invoke(exception, logMessage);
                    }
                    if (!string.IsNullOrEmpty(message))
                    {
                        if (_includeScopesInMessage || _includeExceptionInfoInMessage)
                        {
                            var writer = new StringWriter();
                            if (_includeScopesInMessage)
                            {
                                ScopeProvider.ForEachScope(
                                    (scope, writerState) =>
                                    {
                                        writerState.Write(scope);
                                        writerState.Write(" => ");
                                    },
                                    writer);
                            }
                            writer.Write(message);
                            if (_includeExceptionInfoInMessage && exception != null)
                            {
                                writer.Write("\n");
                                writer.Write(exception);
                            }
                            message = writer.GetStringBuilder().ToString();
                        }
                        logMessage.Append(JournalFieldName.Message, message);
                    }
                    var scopeProvider = ScopeProvider;
                    if (scopeProvider != null)
                    {
                        scopeProvider.ForEachScope((scope, msg) => AppendScope(scope, msg), logMessage);
                    }
                    if (state != null)
                    {
                        AppendState("STATE", state, logMessage);
                    }
                    Journal.Log(flags, logMessage);
                }
            }
        }

        private static void AppendScope(object scope, JournalMessage message)
            => AppendState("SCOPE", scope, message, formatState: true);

        private static void AppendState(string fieldName, object state, JournalMessage message, bool formatState = false)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object>> keyValuePairs)
            {
                for (int i = 0; i < keyValuePairs.Count; i++)
                {
                    var pair = keyValuePairs[i];
                    if (pair.Key == OriginalFormat)
                    {
                        if (formatState)
                        {
                            message.Append(fieldName, state.ToString());
                        }
                        continue;
                    }
                    message.Append(pair.Key, pair.Value);
                }
            }
            else
            {
                message.Append(fieldName, state);
            }
        }

        private static void FormatException(Exception exception, JournalMessage logMessage)
        {
            logMessage.Append(Exception, exception.Message);
            logMessage.Append(ExceptionType, exception.GetType().FullName);
            logMessage.Append(ExceptionStackTrace, exception.StackTrace);
            Exception innerException = exception.InnerException;
            if (innerException != null)
            {
                logMessage.Append(InnerException, innerException.Message);
                logMessage.Append(InnerExceptionType, innerException.GetType().FullName);
                logMessage.Append(InnerExceptionStackTrace, innerException.StackTrace);
            }
        }

        /// <summary>
        /// An empty scope without any logic
        /// </summary>
        class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();

            private NullScope()
            {
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }
}
