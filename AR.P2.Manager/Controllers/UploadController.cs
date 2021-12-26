using AR.P2.Algo;
using AR.P2.Manager.Dtos;
using AR.P2.Manager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Prometheus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace AR.P2.Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        // TODO think of what to od with processing results - maybe export to csv or some binary form for each subtask?
        // TODO adjust buckets for nano-millisecond range
        // TODO add logging
        private static readonly Histogram FftDuration = Metrics.CreateHistogram("mgr_fft_duration_seconds", "Histogram of FFT durations.");
        private static readonly Histogram FileProcessingDuration = Metrics.CreateHistogram("mgr_file_processing_duration_seconds", "Histogram of file processing durations.");

        [HttpPost]
        public async Task<IActionResult> PostUploadJobAsync(
            [FromForm] UploadJobDto uploadJobDto,
            List<IFormFile> files,
            [FromServices] IFileUploadService fileUploadService)
        {
            if (!files.Any())
                return BadRequest("No files provided.");

            int windowSize = uploadJobDto.WindowSize;
            double samplingRate = uploadJobDto.SamplingRate;

            // Upload the files
            var fileUploadResults = await fileUploadService.UploadFiles(files);

            // TODO handle multiple files at once
            var filePath = fileUploadResults.ToList()[0].LocalPath;
            using var fs = System.IO.File.OpenRead(filePath);

            using var buffIn = new BufferedStream(fs, uploadJobDto.WindowSize * sizeof(double));
            using var binIn = new BinaryReader(buffIn);

            int totalSignalCount = (int)(fs.Length / sizeof(double));
            int signalCount = totalSignalCount / windowSize * windowSize;

            using var fileProcessingTimer = FileProcessingDuration.NewTimer();

            List<FftResult> fftResults = null;
            switch (uploadJobDto.ProcessingType)
            {
                case Models.ProcessingType.Sequential:
                    fftResults = SequentialProcessing(binIn, windowSize, signalCount, samplingRate);
                    break;
                case Models.ProcessingType.Parallel:
                    fftResults = await ParallelProcessing(binIn, windowSize, signalCount, samplingRate);
                    break;
                case Models.ProcessingType.Simd:
                    fftResults = SimdProcessing(binIn, windowSize, signalCount, samplingRate);
                    break;
                case Models.ProcessingType.SimdParallel:
                    fftResults = await ParallelProcessing(binIn, windowSize, signalCount, samplingRate, simd: true);
                    break;
                default:
                    throw new NotSupportedException(uploadJobDto.ProcessingType.ToString());
            }

            return Ok(fileUploadResults);
        }

        //

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

        private unsafe void SimdInner(
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
                    complexSpecComps = Operations.FftSimdRecurse(signalPtr + i, windowSize).ToList();
                }
                var fftResult = Operations.GetFftResult(complexSpecComps, samplingRate, windowSize);
                fftResults.Add(fftResult);
            }
        }

        public struct SubTaskInfo
        {
            public int TaskIndex { get; set; }
            public int WindowIndex { get; set; }

            public override string ToString()
            {
                return $"{nameof(TaskIndex)}: {TaskIndex}, {nameof(WindowIndex)}: {WindowIndex}";
            }

            public static int Compare(SubTaskInfo x, SubTaskInfo y)
            {
                if (x.TaskIndex > y.TaskIndex)
                    return 1;
                else if (x.TaskIndex < y.TaskIndex)
                    return -1;
                else
                    return x.WindowIndex - y.WindowIndex;
            }
        }

        public class SubTaskInfoComparer : IComparer<SubTaskInfo>
        {
            public int Compare(SubTaskInfo x, SubTaskInfo y)
            {
                return SubTaskInfo.Compare(x, y);
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
                processingTasks[i] = Task.Factory.StartNew(() => ParallelInner(taskIndex, signal, windowSize, parallelTaskCount, samplingRate, simd));
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

            var result = dict.Values.Concat(seqResults).ToList();

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

        private static List<KeyValuePair<SubTaskInfo, FftResult>> ParallelInner(
            int taskIndex,
            double[] signal,
            int windowSize,
            int signalPartCount,
            double samplingRate,
            bool simd = false)
        {
            var kvps = new List<KeyValuePair<SubTaskInfo, FftResult>>();

            unsafe
            {
                fixed (double* signalPtr = signal)
                {
                    int i = 0;
                    for (int k = 0; k + windowSize <= signalPartCount; k += windowSize)
                    {
                        List<Complex> complexSpecComps;
                        using (var fftDurationTimer = FftDuration.NewTimer())
                        {
                            if (simd)
                            {
                                complexSpecComps = Operations.FftSimdRecurse(signalPtr + k, windowSize).ToList();
                            }
                            else
                            {
                                complexSpecComps = Operations.FftRecurse(signalPtr + k, windowSize);
                            }
                        }
                        var fftResult = Operations.GetFftResult(complexSpecComps, samplingRate, windowSize);
                        kvps.Add(new KeyValuePair<SubTaskInfo, FftResult>(new SubTaskInfo { TaskIndex = taskIndex, WindowIndex = i }, fftResult));
                        i++;
                    }
                }
            }

            return kvps;
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

        private static unsafe void SequentialInner(
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

