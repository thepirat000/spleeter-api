using System;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace SpleeterAPI
{
    public class EphemeralLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var msg = formatter(state, exception);
            var json = JsonConvert.SerializeObject(new
            {
                logLevel = logLevel,
                eventId = eventId,
                logDateTimeUtc = DateTime.UtcNow,
                details = msg,
                exception = exception
            });
            Startup.EphemeralLog(json);
        }
    }
}
