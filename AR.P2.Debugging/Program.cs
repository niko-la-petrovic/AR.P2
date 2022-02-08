using AR.P2.Manager.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace AR.P2.Debugging
{
    internal class Program
    {

        class FakeLogger : ILogger<FftService>
        {
            public IDisposable BeginScope<TState>(TState state) => default!;

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
            }
        }

        static void Main(string[] args)
        {
            var logger = new FakeLogger();
            var fftService = new FftService(logger);

            var results = fftService.ProcessFile(@"C:\Users\Blue-Glass\source\repos\AR.P2\AR.P2.Test\output_44100_10.bin", Manager.Models.ProcessingType.Sequential, 4096, 44100).GetAwaiter().GetResult();
        }
    }
}
