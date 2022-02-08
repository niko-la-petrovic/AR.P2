using AR.P2.Manager.Models;
using AR.P2.Manager.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using AR.P2.Algo;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AR.P2.Test
{
    public class MockLogger : ILogger<FftService>
    {
        private readonly ITestOutputHelper outputHelper;
        public MockLogger(ITestOutputHelper helper)
        {
            outputHelper = helper;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            outputHelper.WriteLine($"{formatter(state, exception)}");
        }
    }

    public class UnitTests
    {
        public const string InputFilePath = "output_44100_10.bin";
        public const int WindowSize = 4096;
        public const int SamplingRate = 44100;
        public const int BitDepth = 16;
        public const int ChannelCount = 1;
        public const double Frequency = 800;
        public const double MaxRelativeError = 0.05;

        private readonly ITestOutputHelper outputHelper;
        private readonly ILogger<FftService> logger;
        public UnitTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;
            logger = new MockLogger(outputHelper);
        }

        [Fact]
        public void CanFftSequential()
        {
            var processingType = ProcessingType.Sequential;
            var fftService = new FftService(logger);

            var results = fftService.ProcessFile(InputFilePath, processingType, WindowSize, SamplingRate)
                .GetAwaiter().GetResult();

            results.TestFftResults(MaxRelativeError, Frequency, out var usefulFftResults, out var relativeErrors, out var allWithinMargin);
            LogRelativeErrors(relativeErrors, processingType);

            Assert.True(allWithinMargin);
        }

        [Fact]
        public void CanFftParallel()
        {
            var processingType = ProcessingType.Parallel;
            var fftService = new FftService(logger);

            var results = fftService.ProcessFile(InputFilePath, processingType, WindowSize, SamplingRate)
                .GetAwaiter().GetResult();

            results.TestFftResults(MaxRelativeError, Frequency, out var usefulFftResults, out var relativeErrors, out var allWithinMargin);
            LogRelativeErrors(relativeErrors, processingType);

            Assert.True(allWithinMargin);
        }

        [Fact]
        public void CanFftSimd()
        {
            var processingType = ProcessingType.Simd;
            var fftService = new FftService(logger);

            var results = fftService.ProcessFile(InputFilePath, processingType, WindowSize, SamplingRate)
                .GetAwaiter().GetResult();

            results.TestFftResults(MaxRelativeError, Frequency, out var usefulFftResults, out var relativeErrors, out var allWithinMargin);
            LogRelativeErrors(relativeErrors, processingType);

            Assert.True(allWithinMargin);
        }

        [Fact]
        public void CanFftSimdParallel()
        {
            var processingType = ProcessingType.SimdParallel;
            var fftService = new FftService(logger);

            var results = fftService.ProcessFile(InputFilePath, processingType, WindowSize, SamplingRate)
                .GetAwaiter().GetResult();

            results.TestFftResults(MaxRelativeError, Frequency, out var usefulFftResults, out var relativeErrors, out var allWithinMargin);
            LogRelativeErrors(relativeErrors, processingType);

            Assert.True(allWithinMargin);
        }

        [InlineData(-0.001)]
        [InlineData(0.001)]
        [InlineData(Math.PI / 2.0)]
        [InlineData(-Math.PI / 2.0)]
        [InlineData(3 * Math.PI / 4.0)]
        [InlineData(-3 * Math.PI / 4.0)]
        [InlineData(Math.PI + 0.001)]
        [InlineData(-Math.PI - 0.001)]
        [Theory]
        public void AccurateSinAprx(double x)
        {
            var aprxSin = Operations.AprxSin(x);
            var realSin = Math.Sin(x);

            var relError = Math.Abs(aprxSin - realSin) / realSin;
            outputHelper.WriteLine(relError.ToString());
            Assert.True(Math.Abs(relError) <= MaxRelativeError);
        }

        [InlineData(-Math.PI - 0.001)]
        [InlineData(-Math.PI / 2.0 + 0.001)] // min cos
        [InlineData(-3 * Math.PI / 4.0 + 0.001)]
        [InlineData(-0.001)]
        [InlineData(0.001)]
        [InlineData(3 * Math.PI / 4.0 + 0.001)]
        [InlineData(Math.PI / 2.0 + 0.001)]
        [InlineData(Math.PI + 0.001)]
        [InlineData(3 * Math.PI / 2.0 + 0.001)] // max cos
        [Theory]
        public void AccurateCosAprx(double x)
        {
            var aprxCos = Operations.AprxCos(x);
            var realCos = Math.Cos(x);

            var relError = Math.Abs(aprxCos - realCos) / realCos;
            outputHelper.WriteLine(relError.ToString());
            Assert.True(Math.Abs(relError) <= MaxRelativeError);
        }

        [InlineData(-Math.PI - 0.001)]
        [InlineData(-Math.PI / 2.0 + 0.001)] // min cos
        [InlineData(-3 * Math.PI / 4.0 + 0.001)]
        [InlineData(-0.001)]
        [InlineData(0.001)]
        [InlineData(3 * Math.PI / 4.0 + 0.001)]
        [InlineData(Math.PI / 2.0 + 0.001)]
        [InlineData(Math.PI + 0.001)]
        [InlineData(3 * Math.PI / 2.0 + 0.001)] // max cos
        [InlineData(-0.7853981633974483)]
        [Theory]
        public void AccurateCos128(double x)
        {
            var aprxCos = Operations.Cos128(Vector128.Create(x));
            var realCos = Vector128.Create(Math.Cos(x));

            var diff = Avx.Subtract(aprxCos, realCos);
            var absDiff = Operations.Abs(diff);
            var relError = Avx.Divide(absDiff, realCos);

            outputHelper.WriteLine(relError.ToString());

            var absRelError = Operations.Abs(relError);
            var maxRelError = Vector128.Create(MaxRelativeError);
            var comparisonResult = Avx.CompareLessThanOrEqual(absRelError, maxRelError);
            var convertedToInt = comparisonResult.AsInt16();
            Assert.Equal(convertedToInt, Vector128.Create((short)-1));
        }

        [InlineData(-0.001)]
        [InlineData(0.001)]
        [InlineData(Math.PI / 2.0)]
        [InlineData(-Math.PI / 2.0)]
        [InlineData(3 * Math.PI / 4.0)]
        [InlineData(-3 * Math.PI / 4.0)]
        [InlineData(Math.PI + 0.001)]
        [InlineData(-Math.PI - 0.001)]
        [InlineData(-0.7853981633974483)]
        [Theory]
        public void AccurateSin128(double x)
        {
            var aprxSin = Operations.Sin128(Vector128.Create(x));
            var realSin = Vector128.Create(Math.Sin(x));

            var diff = Avx.Subtract(aprxSin, realSin);
            var absDiff = Operations.Abs(diff);
            var relError = Avx.Divide(absDiff, realSin);

            outputHelper.WriteLine(relError.ToString());

            var absRelError = Operations.Abs(relError);
            var maxRelError = Vector128.Create(MaxRelativeError);
            var comparisonResult = Avx.CompareLessThanOrEqual(absRelError, maxRelError);
            var convertedToInt = comparisonResult.AsInt16();
            Assert.Equal(convertedToInt, Vector128.Create((short)-1));
        }

        // Returns -1 if true or 0 if false
        [Fact]
        public void AvxGreaterThanValues()
        {
            var l = Vector128.Create(1.0);
            var r = Vector128.Create(0.0);

            var greaterResult = Avx.CompareGreaterThanOrEqual(l, r);
            var lesserResult = Avx.CompareGreaterThanOrEqual(r, l);

            Assert.Equal(greaterResult.AsInt64(), Vector128.Create((long)-1));
            Assert.Equal(lesserResult.AsInt64(), Vector128.Create((long)0));
        }

        private void LogRelativeErrors(IEnumerable<double> relativeErrors, ProcessingType processingType)
        {
            logger.LogInformation($"[{processingType}] Relative errors:");
            foreach (var error in relativeErrors)
            {
                logger.LogInformation(error.ToString());
            }
        }
    }
}
