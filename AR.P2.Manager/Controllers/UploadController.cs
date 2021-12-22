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
using System.Threading.Tasks;

namespace AR.P2.Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        // TODO think of what to od with processing results - maybe export to csv or some binary form for each subtask?
        // TODO adjust buckets for nano-millisecond range
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

            // Upload the initial files
            var fileUploadResults = await fileUploadService.UploadFiles(files);

            var filePath = fileUploadResults.ToList()[0].LocalPath;
            using var fs = System.IO.File.OpenRead(filePath);

            using var buffIn = new BufferedStream(fs, uploadJobDto.WindowSize * sizeof(double));
            using var binIn = new BinaryReader(buffIn);

            int windowSize = uploadJobDto.WindowSize;
            int totalSignalCount = (int)(fs.Length / sizeof(double));
            int signalCount = totalSignalCount / windowSize * windowSize;

            using var fileProcessingTimer = FileProcessingDuration.NewTimer();

            List<FftResult> fftResults = null;
            switch (uploadJobDto.ProcessingType)
            {
                case Models.ProcessingType.Sequential:
                    fftResults = BasicProcessing(uploadJobDto, binIn, windowSize, signalCount);
                    break;
                case Models.ProcessingType.Parallel:
                    fftResults = await ParallelProcessing(uploadJobDto, binIn, windowSize, signalCount);
                    break;
                case Models.ProcessingType.Simd:
                    break;
                case Models.ProcessingType.SimdParallel:
                    break;
            }

            return Ok(fileUploadResults);
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

        private async static Task<List<FftResult>> ParallelProcessing(UploadJobDto uploadJobDto, BinaryReader binIn, int windowSize, int signalCount)
        {
            var cpuCount = Environment.ProcessorCount;
            var samplingRate = uploadJobDto.SamplingRate;

            var parallelSignalCount = signalCount / cpuCount / windowSize * cpuCount * windowSize;
            var parallelTaskCount = parallelSignalCount / cpuCount;
            var remainingSignalCount = signalCount - parallelSignalCount;
            var seqSignalCount = remainingSignalCount / windowSize * windowSize;

            var processingTasks = new Task<List<KeyValuePair<SubTaskInfo, FftResult>>>[cpuCount];
            for (int i = 0; i < cpuCount; i++)
            {
                var signal = new double[parallelTaskCount];
                // TODO optimize - maybe by reading an entire block and doing block copy or reinterpreting the byte array pointer as a double array pointer
                for (int j = 0; j < parallelTaskCount; j++)
                {
                    double d = binIn.ReadDouble();
                    signal[j] = d;
                }

                int taskIndex = i;
                processingTasks[i] = Task.Factory.StartNew(() => ParallelInner(taskIndex, signal, windowSize, parallelTaskCount, samplingRate));
            }

            Task.WaitAll(processingTasks);

            var dict = new SortedDictionary<SubTaskInfo, FftResult>(new SubTaskInfoComparer());
            var seqResults = new List<FftResult>(seqSignalCount);

            if (seqSignalCount > 0)
            {
                var signal = new double[seqSignalCount];
                for (int i = 0; i < seqSignalCount; i++)
                {
                    double d = binIn.ReadDouble();
                    signal[i] = d;
                }

                unsafe
                {
                    fixed (double* signalPtr = signal)
                    {
                        SequentialInner(signalPtr, uploadJobDto, windowSize, seqSignalCount, seqResults);
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

            return dict.Values.Concat(seqResults).ToList();
        }

        private static List<KeyValuePair<SubTaskInfo, FftResult>> ParallelInner(
            int taskIndex,
            double[] signal,
            int windowSize,
            int signalPartCount,
            double samplingRate)
        {
            var kvps = new List<KeyValuePair<SubTaskInfo, FftResult>>();

            unsafe
            {
                fixed (double* signalPtr = signal)
                {
                    int i = 0;
                    for (int k = 0; k + windowSize <= signalPartCount; k += windowSize)
                    {
                        using var fftDurationTimer = FftDuration.NewTimer();

                        var complexSpecComps = Operations.FftRecurse(signalPtr + k, windowSize);
                        var fftResult = Operations.GetFftResult(complexSpecComps, samplingRate, windowSize);
                        kvps.Add(new KeyValuePair<SubTaskInfo, FftResult>(new SubTaskInfo { TaskIndex = taskIndex, WindowIndex = i }, fftResult));
                        i++;
                    }
                }
            }

            return kvps;
        }

        private static List<FftResult> BasicProcessing(UploadJobDto uploadJobDto, BinaryReader binIn, int windowSize, int signalCount)
        {
            List<FftResult> fftResults = new();

            var signal = new double[signalCount];
            for (int i = 0; i < signalCount; i++)
            {
                double d = binIn.ReadDouble();
                signal[i] = d;
            }

            unsafe
            {
                fixed (double* signalPtr = signal)
                {
                    SequentialInner(signalPtr, uploadJobDto, windowSize, signalCount, fftResults);
                }
            }

            return fftResults;
        }

        private static unsafe void SequentialInner(double* signalPtr, UploadJobDto uploadJobDto, int windowSize, int signalCount, List<FftResult> fftResults)
        {
            for (int i = 0; i + windowSize <= signalCount; i += windowSize)
            {
                using var fftDurationTimer = FftDuration.NewTimer();

                var complexSpecComps = Operations.FftRecurse(signalPtr + i, windowSize);
                var fftResult = Operations.GetFftResult(complexSpecComps, uploadJobDto.SamplingRate, uploadJobDto.WindowSize);
                fftResults.Add(fftResult);
            }
        }
    }
}

