using System;
using System.Collections.Generic;

namespace Tmds.Systemd.Logging
{
    /// <summary>
    /// Options for the journal logger.
    /// </summary>
    public class JournalLoggerOptions
    {
        /// <summary>
        /// Default formatter for exceptions.
        /// </summary>
        public static Action<Exception, JournalMessage> DefaultExceptionFormatter => JournalLogger.DefaultExceptionFormatter;

        /// <summary>
        /// Gets or sets a value indicating whether messages are dropped when busy instead of blocking.
        /// </summary>
        public bool DropWhenBusy { get; set; }

        /// <summary>
        /// Gets or sets the syslog identifier added to each log message.
        /// </summary>
        public string SyslogIdentifier { get; set; } = Journal.SyslogIdentifier;

        /// <summary>
        /// Gets or sets a delegate that is used to format exceptions.
        /// </summary>
        public Action<Exception, JournalMessage> ExceptionFormatter { get; set; } = DefaultExceptionFormatter;

        /// <summary>
        /// Gets or sets a dictionary of extra fields that are added to each log message
        /// </summary>
        public Dictionary<string, string> ExtraFields { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets of sets a value indicating whether the message text should be include the scopes
        /// </summary>
        ///
        /// If set the message text in the journal MESSAGE field will include a rendering of the scopes.
        /// Irrespective of this setting scopes are always included in the log message as dedicated fields.
        public bool IncludeScopesInMessage { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to message text should include exception details
        /// </summary>
        ///
        /// If set the message text in the journal MESSAGE field will include
        public bool IncludeExceptionInfoInMessage { get; set; } = false;
    }
}
