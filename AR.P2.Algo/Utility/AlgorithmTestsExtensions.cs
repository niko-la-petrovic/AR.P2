using AR.P2.Algo;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AR.P2.Algo
{
    public static class AlgorithmTestsExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fftResults"></param>
        /// <remarks>Cooley Tukey works such that the second half of the resulting spectral components is mirror reflection of the first half. Thus, only half of the resulting spectral components contain all the information.</remarks>
        /// <returns></returns>
        public static IEnumerable<FftResult> UsefulFftResults(this IEnumerable<FftResult> fftResults)
        {
            var halvedResults = fftResults.Select(res =>
            {
                return new FftResult
                {
                    WindowSize = res.WindowSize,
                    SamplingRate = res.SamplingRate,
                    SpectralComponents = res.SpectralComponents
                        .Take(res.SpectralComponents.Count / 2).ToList()
                };
            });

            return halvedResults;
        }

        public static void TestFftResults(
            this IEnumerable<FftResult> fftResults,
            double maxRelativeError,
            double targetFrequency,
            out IEnumerable<FftResult> usefulFftResults,
            out IEnumerable<double> relativeErrors,
            out bool allWithinMargin)
        {
            usefulFftResults = fftResults.UsefulFftResults();
            relativeErrors = usefulFftResults.Select(RelativeError(targetFrequency));
            allWithinMargin = relativeErrors.Select(res => res <= maxRelativeError)
                .All(s => s);
        }

        public static Func<FftResult, double> RelativeError(double targetFrequency)
        {
            return res =>
            {
                var max = res.SpectralComponents.Max();
                var diff = targetFrequency - max.Frequency;
                var relativeError = Math.Abs(diff / targetFrequency);

                return relativeError;
            };
        }
    }
}
