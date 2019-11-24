using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SpleeterAPI
{
    public class EphemeralLoggerProvider : ILoggerProvider
    {
        private ILogger _logger;
        private bool _disposed = false; // To detect redundant calls

        public ILogger CreateLogger(string categoryName)
        {
            if (null == _logger)
            {
                _logger = new EphemeralLogger();
            }
            return _logger;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logger = null;
                }

                _disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
    }
}
