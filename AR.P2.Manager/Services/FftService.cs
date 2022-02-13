using AR.P2.Algo;
using AR.P2.Manager.Models;
using AR.P2.Manager.Utility;
using Microsoft.Extensions.Logging;
using Prometheus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace AR.P2.Manager.Services
{
    public class FftService : IFftService
    {
        private static readonly Histogram FftDuration = Metrics.CreateHistogram("mgr_fft_duration_seconds", "Histogram of FFT durations.", new HistogramConfiguration
        {
            Buckets = new double[] { Math.Pow(10, -4), 0.001, 0.005, 0.010, 0.050, 0.1, 1, 1.5, 2.0 },
        });
        private static readonly Histogram FileProcessingDuration = Metrics.CreateHistogram("mgr_file_processing_duration_seconds", "Histogram of file processing durations.", new HistogramConfiguration
        {
            Buckets = new double[] { 0.005, 0.010, 0.050, 0.1, 0.5, 1, 2, 5, 7, 10, 15 },
        });

        private readonly ILogger _logger;

        public FftService(ILogger<FftService> logger)
        {
            _logger = logger;
        }

        public async Task ProcessFileUploadResults(
            IEnumerable<FileUploadResult> fileUploadResults,
            int windowSize,
            double samplingRate,
            bool saveResults,
            ProcessingType processingType)
        {
            switch (processingType)
            {
                case ProcessingType.Sequential or ProcessingType.Simd:
                    foreach (var fileUploadResult in fileUploadResults)
                    {
                        await ProcessFileUploadResult(windowSize, samplingRate, processingType, fileUploadResult, saveResults);
                    }
                    break;
                case ProcessingType.Parallel or ProcessingType.SimdParallel:
                    fileUploadResults.AsParallel().ForAll(async fileUploadResult =>
                    {
                        await ProcessFileUploadResult(windowSize, samplingRate, processingType, fileUploadResult, saveResults);
                    });
                    break;
                default:
                    _logger.LogDebug($"Unsupported processing type '{processingType}'.");
                    throw new NotSupportedException(processingType.ToString());
            }
        }

        private async Task ProcessFileUploadResult(int windowSize, double samplingRate, ProcessingType processingType, FileUploadResult fileUploadResult, bool saveResults)
        {
            var filePath = fileUploadResult.LocalPath;

            _logger?.LogInformation($"Processing '{fileUploadResult.LocalPath}'.");

            var fftResults = await ProcessFile(filePath, processingType, windowSize, samplingRate);

            _logger?.LogInformation($"Finished processing '{fileUploadResult.LocalPath}'.");
            if (saveResults)
                SaveFFtResults(filePath, fftResults);
        }

        public async Task<List<FftResult>> ProcessFile(string filePath, ProcessingType processingType, int windowSize, double samplingRate)
        {
            using var fs = File.OpenRead(filePath);

            using var buffIn = new BufferedStream(fs, windowSize * sizeof(double));
            using var binIn = new BinaryReader(buffIn);

            int totalSignalCount = (int)(fs.Length / sizeof(double));
            int signalCount = totalSignalCount / windowSize * windowSize;

            using (var fileProcessingTimer = FileProcessingDuration.NewTimer())
            {
                List<FftResult> fftResults = null;
                fftResults = processingType switch
                {
                    ProcessingType.Sequential => SequentialProcessing(binIn, windowSize, signalCount, samplingRate),
                    ProcessingType.Parallel => await ParallelProcessing(binIn, windowSize, signalCount, samplingRate),
                    ProcessingType.Simd => SimdProcessing(binIn, windowSize, signalCount, samplingRate),
                    ProcessingType.SimdParallel => await ParallelProcessing(binIn, windowSize, signalCount, samplingRate, simd: true),
                    _ => throw new NotSupportedException(processingType.ToString()),
                };
                return fftResults;
            }
        }

        public static void SaveFFtResults(
            string filePath,
            List<FftResult> fftResults)
        {
            string parentDir = Directory.GetParent(filePath).FullName;
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            var outFilePath = Path.Join(parentDir, $"{fileName}_out.csv");

            using var fs = new StreamWriter(outFilePath);

            int resultCount = fftResults.Count;
            for (int i = 0; i < resultCount; i++)
            {
                var fftResult = fftResults[i];
                for (int j = 0; j < fftResult?.SpectralComponents.Count; j++)
                {
                    var specComp = fftResult.SpectralComponents[j];
                    fs.WriteLine($"{specComp.Frequency},{specComp.Magnitude}");
                }
                fs.WriteLine($"{Environment.NewLine}");
            }
        }

        private List<FftResult> SimdProcessing(
            BinaryReader binIn,
            int windowSize,
            int signalCount,
            double samplingRate)
        {
            List<FftResult> fftResults = new();
            var signal = ReadInSignal(binIn, signalCount);

            unsafe
            {
                fixed (double* signalPtr = signal)
                {
                    SimdInner(signalPtr, windowSize, samplingRate, signalCount, fftResults);
                }
            }

            return fftResults;
        }

        private static unsafe void SimdInner(
            double* signalPtr,
            int windowSize,
            double samplingRate,
            int signalCount,
            List<FftResult> fftResults)
        {
            for (int i = 0; i + windowSize <= signalCount; i += windowSize)
            {
                Complex[] complexSpecCompsArr = null;
                using (var fftDurationTimer = FftDuration.NewTimer())
                {
                    complexSpecCompsArr = Operations.FftSimdRecurse(signalPtr + i, windowSize);
                }
                var fftResult = Operations.GetFftResult(complexSpecCompsArr, samplingRate, windowSize);
                fftResults.Add(fftResult);
            }
        }

        private static Task<List<FftResult>> ParallelProcessing(
            BinaryReader binIn,
            int windowSize,
            int signalCount,
            double samplingRate,
            bool simd = false)
        {
            var cpuCount = Environment.ProcessorCount;

            var parallelSignalCount = signalCount / cpuCount / windowSize * cpuCount * windowSize;
            var parallelTaskCount = parallelSignalCount / cpuCount;
            var remainingSignalCount = signalCount - parallelSignalCount;
            var seqSignalCount = remainingSignalCount / windowSize * windowSize;

            var processingTasks = new Task<List<KeyValuePair<SubTaskInfo, FftResult>>>[cpuCount];
            for (int i = 0; i < cpuCount; i++)
            {
                // TODO optimize - maybe by reading an entire block and doing block copy or reinterpreting the byte array pointer as a double array pointer
                var signal = ReadInSignal(binIn, parallelTaskCount);

                int taskIndex = i;
                processingTasks[i] = Task.Factory.StartNew((o) =>
                {
                    var signal = (double[])o;
                    var list = new List<KeyValuePair<SubTaskInfo, FftResult>>();
                    var resultList = new List<FftResult>();
                    unsafe
                    {
                        fixed (double* signalPtr = signal)
                        {
                            if (!simd)
                                SequentialInner(signalPtr, windowSize, samplingRate, parallelTaskCount, resultList);
                            else
                            {
                                SimdInner(signalPtr, windowSize, samplingRate, parallelTaskCount, resultList);
                            }
                        }
                    }

                    for (int i = 0; i < resultList.Count; i++)
                    {
                        list.Add(new KeyValuePair<SubTaskInfo, FftResult>(new SubTaskInfo { TaskIndex = taskIndex, WindowIndex = i }, resultList[i]));
                    }

                    return list;
                }, signal);
            }

            Task.WaitAll(processingTasks);

            var dict = new SortedDictionary<SubTaskInfo, FftResult>(new SubTaskInfoComparer());
            var seqResults = new List<FftResult>(seqSignalCount);

            if (seqSignalCount > 0)
            {
                var signal = ReadInSignal(binIn, seqSignalCount);

                unsafe
                {
                    fixed (double* signalPtr = signal)
                    {
                        SequentialInner(signalPtr, windowSize, samplingRate, seqSignalCount, seqResults);
                    }
                }
            }

            foreach (var t in processingTasks)
            {
                foreach (var res in t.Result)
                {
                    dict.Add(res.Key, res.Value);
                }
            }

            var result = dict.Values
                .Concat(seqResults)
                .ToList();

            return Task.FromResult(result);
        }

        private static double[] ReadInSignal(BinaryReader binIn, int sampleNumber)
        {
            var signal = new double[sampleNumber];
            for (int i = 0; i < sampleNumber; i++)
            {
                double d = binIn.ReadDouble();
                signal[i] = d;
            }

            return signal;
        }

        private static List<FftResult> SequentialProcessing(
            BinaryReader binIn,
            int windowSize,
            int signalCount,
            double samplingRate)
        {
            List<FftResult> fftResults = new();
            var signal = ReadInSignal(binIn, signalCount);

            unsafe
            {
                fixed (double* signalPtr = signal)
                {
                    SequentialInner(signalPtr, windowSize, samplingRate, signalCount, fftResults);
                }
            }

            return fftResults;
        }

        public static unsafe void SequentialInner(
            double* signalPtr,
            int windowSize,
            double samplingRate,
            int signalCount,
            List<FftResult> fftResults)
        {
            for (int i = 0; i + windowSize <= signalCount; i += windowSize)
            {
                List<Complex> complexSpecComps;
                using (var fftDurationTimer = FftDuration.NewTimer())
                {
                    complexSpecComps = Operations.FftRecurse(signalPtr + i, windowSize);
                }
                var fftResult = Operations.GetFftResult(complexSpecComps, samplingRate, windowSize);
                fftResults.Add(fftResult);
            }
        }

    }
}
