using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Tmds.Systemd.Tests;
using Xunit;

namespace Tmds.Systemd.Logging.Tests
{
    public class JournalLoggerTests: IDisposable
    {
        private const string ExceptionMessage = "Exception message";

        private readonly FakeJournalSocket _socket;
        public JournalLoggerTests()
        {
            _socket = new FakeJournalSocket();
        }

        public void Dispose()
        {
            _socket.Dispose();
        }

        private Dictionary<string, string> ReadFields()
        {
            return ReadMessageFields.ReadFields(_socket.Socket);
        }

        private static JournalLogger Logger(
            string name="name",
            string syslogIdentifier="syslog",
            Dictionary<string, string>? extraFields=null,
            bool includeScopesInMessage=false,
            bool includeExceptionInfoInMessage=false)
        {
            return new JournalLogger(name, new FakeScopeProvider(), new JournalLoggerOptions()
            {
                SyslogIdentifier = syslogIdentifier,
                ExtraFields = extraFields ?? new Dictionary<string, string>(),
                IncludeScopesInMessage = includeScopesInMessage,
                IncludeExceptionInfoInMessage = includeExceptionInfoInMessage,
            });
        }

        [Fact]
        private void WriteSimpleMessage()
        {
            const string aMessage = "A message";

            Logger().LogInformation(aMessage);

            var fields = ReadFields();

            Assert.Equal(aMessage, fields[JournalFieldName.Message.ToString()]);
        }

        [Fact]
        private void WritesLogPriority()
        {
            Logger().LogInformation("A message");

            var fields = ReadFields();

            Assert.Equal(((int)LogFlags.Information - 1).ToString(), fields[JournalFieldName.Priority.ToString()]);
        }

        [Fact]
        private void WriteSyslogIdentifier()
        {
            const string syslogIdentifier = "example-syslog";
            var logger = Logger(syslogIdentifier: syslogIdentifier);

            logger.LogInformation("A message");

            var fields = ReadFields();

            Assert.Equal(syslogIdentifier, fields[JournalFieldName.SyslogIdentifier.ToString()]);
        }

        [Fact]
        private void WriteLoggerName()
        {
            const string loggerName = "TestLogger";

            Logger(name: loggerName).LogInformation("A message");

            var fields = ReadFields();

            Assert.Equal(loggerName, fields[JournalLogger.Logger.ToString()]);
        }

        [Fact]
        private void WriteFormatted()
        {
            const string format = "A message with args: {arg1}";
            const string arg1 = "arg 1 value";

            Logger().LogInformation(format, arg1);

            var fields = ReadFields();

            Assert.Equal("A message with args: arg 1 value", fields[JournalFieldName.Message.ToString()]);
            Assert.Equal(arg1, fields["ARG1"]);
        }

        [Fact]
        private void WriteScopes()
        {
            const string scopeKey = "SCOP1";
            const string scopeValue = "SCOPVAL1";

            var logger = Logger();

            using (logger.BeginScope(
                new[]
                {
                    new KeyValuePair<string, object>(scopeKey, scopeValue)
                }
            ))
            {
                logger.LogInformation("A message");
            }

            var fields = ReadFields();

            Assert.Equal(scopeValue, fields[scopeKey]);
        }


        private void ThrowException()
        {
            throw new Exception(ExceptionMessage);
        }

        private void ThrowExceptionWrapper()
        {
            ThrowException();
        }

        [Fact]
        private void WriteExceptionInfo()
        {
            const string logMessage = "Log message";
            var logger = Logger();
            string? exceptionType = null;
            string? exceptionStackTrace = null;

            try
            {
                ThrowExceptionWrapper();
            }
            catch (Exception e)
            {
                exceptionType = e.GetType().FullName;
                exceptionStackTrace = e.StackTrace;
                logger.LogError(e, logMessage);
            }

            var fields = ReadFields();

            Assert.Equal(logMessage, fields[JournalFieldName.Message.ToString()]);
            Assert.Equal(ExceptionMessage, fields[JournalLogger.Exception.ToString()]);
            Assert.Equal(exceptionType, fields[JournalLogger.ExceptionType.ToString()]);
            Assert.Equal(exceptionStackTrace, fields[JournalLogger.ExceptionStackTrace.ToString()]);
        }

        [Fact]
        private void WriteConfiguredExtraFields()
        {
            const string globalContextKey = "GLOBAL_CONTEXT_KEY";
            const string globalContextValue = "GLOBAL_CONTEXT_VALUE";

            var logger = Logger(
                extraFields: new Dictionary<string, string>
                {
                    [globalContextKey] = globalContextValue,
                }
            );

            logger.LogInformation("A message");

            var fields = ReadFields();
            Assert.Equal(globalContextValue, fields[globalContextKey]);
        }

        [Fact]
        private void WriteStringScopeInToMessage()
        {
            const string message = "A message";
            const string scope = "some_scope";
            var logger = Logger(
                includeScopesInMessage: true
            );

            using (logger.BeginScope(scope))
            {
                logger.LogInformation(message);
            }

            var fields = ReadFields();

            Assert.Equal(scope, fields["SCOPE"]);
            Assert.Equal(scope + " => " + message, fields[JournalFieldName.Message.ToString()]);
        }

        [Fact]
        private void WriteExceptionInfoInToMessage()
        {
            const string message = "A message";

            var logger = Logger(includeExceptionInfoInMessage: true);
            Exception? exception = null;

            try
            {
                ThrowExceptionWrapper();
            }
            catch (Exception e)
            {
                exception = e;
                logger.LogError(e, message);
            }

            var fields = ReadFields();

            Assert.Equal(message + "\n" + exception!, fields[JournalFieldName.Message.ToString()]);
        }

    }
}
