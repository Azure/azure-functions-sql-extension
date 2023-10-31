// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly Action<LogMessage> _logAction;
        private Dictionary<string, TestLogger> LoggerCache { get; } = new Dictionary<string, TestLogger>();

        public TestLoggerProvider(Action<LogMessage> logAction = null)
        {
            this._logAction = logAction;
        }

        public IList<TestLogger> CreatedLoggers => this.LoggerCache.Values.ToList();

        public ILogger CreateLogger(string categoryName)
        {
            if (!this.LoggerCache.TryGetValue(categoryName, out TestLogger logger))
            {
                logger = new TestLogger(categoryName, this._logAction);
                this.LoggerCache.Add(categoryName, logger);
            }

            return logger;
        }

        public IEnumerable<LogMessage> GetAllLogMessages()
        {
            return this.CreatedLoggers.SelectMany(l => l.GetLogMessages()).OrderBy(p => p.Timestamp);
        }

        public string GetLogString()
        {
            return string.Join(Environment.NewLine, this.GetAllLogMessages());
        }

        public void ClearAllLogMessages()
        {
            foreach (TestLogger logger in this.CreatedLoggers)
            {
                logger.ClearLogMessages();
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public class TestLogger : ILogger
    {
        private readonly Action<LogMessage> _logAction;
        private readonly IList<LogMessage> _logMessages = new List<LogMessage>();

        // protect against changes to logMessages while enumerating
        private readonly object _syncLock = new();

        public TestLogger(string category, Action<LogMessage> logAction = null)
        {
            this.Category = category;
            this._logAction = logAction;
        }

        public string Category { get; private set; }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IList<LogMessage> GetLogMessages()
        {
            lock (this._syncLock)
            {
                return this._logMessages.ToList();
            }
        }

        public void ClearLogMessages()
        {
            lock (this._syncLock)
            {
                this._logMessages.Clear();
            }
        }

        public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!this.IsEnabled(logLevel))
            {
                return;
            }

            var logMessage = new LogMessage
            {
                Level = logLevel,
                EventId = eventId,
                State = state as IEnumerable<KeyValuePair<string, object>>,
                Exception = exception,
                FormattedMessage = formatter(state, exception),
                Category = Category,
                Timestamp = DateTime.UtcNow
            };

            lock (this._syncLock)
            {
                this._logMessages.Add(logMessage);
            }

            this._logAction?.Invoke(logMessage);
        }

        public override string ToString()
        {
            return this.Category;
        }
    }

    public class LogMessage
    {
        public LogLevel Level { get; set; }

        public EventId EventId { get; set; }

        public IEnumerable<KeyValuePair<string, object>> State { get; set; }

        public Exception Exception { get; set; }

        public string FormattedMessage { get; set; }

        public string Category { get; set; }

        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            return $"[{this.Timestamp:HH:mm:ss.fff}] [{this.Category}] {this.FormattedMessage} {this.Exception}";
        }

        /// <summary>
        /// Returns the value for the key in State. Will throw an exception if there is not
        /// exactly one instance of this key in the dictionary.
        /// </summary>
        /// <typeparam name="T">The type to cast the value to.</typeparam>
        /// <param name="key">The key to look up.</param>
        /// <returns>The value.</returns>
        public T GetStateValue<T>(string key)
        {
            return (T)this.State.Single(p => p.Key == key).Value;
        }
    }
}
