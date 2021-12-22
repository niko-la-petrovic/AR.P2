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

            //await ParallelProcessing(uploadJobDto, binIn, windowSize, signalCount);

            BasicProcessing(uploadJobDto, binIn, windowSize, signalCount);

            return Ok(fileUploadResults);
        }

        //TODO use TPL to multithread
        //after reading enough for one thread, start the fft in one task and keep reading, then await all tasks
        private async static Task ParallelProcessing(UploadJobDto uploadJobDto, BinaryReader binIn, int windowSize, int signalCount)
        {
            var cpuCount = Environment.ProcessorCount;
            var signalPartCount = signalCount / cpuCount;

            Dictionary<int, FftResult> fftResults = new();
            int id;

            Task[] processingTasks = new Task[cpuCount];
            for (int i = 0; i < cpuCount; i++)
            {
                var signal = new double[signalPartCount];
                for (int j = 0; j < signalPartCount; j++)
                {
                    double d = binIn.ReadDouble();
                    signal[j] = d;
                }

                processingTasks[i] = Task.Run(() =>
                {
                    unsafe
                    {
                        fixed (double* signalPtr = signal)
                        {
                            for (int k = 0; k + windowSize <= signalPartCount; k++)
                            {
                                using var fftDurationTimer = FftDuration.NewTimer();

                                var complexSpecComps = Algo.Operations.FftRecurse(signalPtr + i, windowSize);
                                var fftResult = Operations.GetFftResult(complexSpecComps, uploadJobDto.SamplingRate, windowSize);
                            }
                        }
                    }
                });
            }

            Task.WaitAll(processingTasks);
        }

        private static void BasicProcessing(UploadJobDto uploadJobDto, BinaryReader binIn, int windowSize, int signalCount)
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
                    for (int i = 0; i + windowSize <= signalCount; i += windowSize)
                    {
                        using var fftDurationTimer = FftDuration.NewTimer();

                        var complexSpecComps = Algo.Operations.FftRecurse(signalPtr + i, windowSize);
                        var fftResult = Operations.GetFftResult(complexSpecComps, uploadJobDto.SamplingRate, uploadJobDto.WindowSize);
                        fftResults.Add(fftResult);
                    }
                }
            }


        }
    }
}

