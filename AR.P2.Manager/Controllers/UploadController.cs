using AR.P2.Manager.Dtos;
using AR.P2.Manager.Models;
using AR.P2.Manager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Prometheus;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AR.P2.Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        // TODO exception handling
        private static readonly Histogram RequestProcessingDuration = Metrics.CreateHistogram("mgr_request_processing_duration_seconds", "Histogram of request processing durations.");

        [HttpPost]
        public async Task<IActionResult> PostUploadJobAsync(
            [FromForm] UploadJobDto uploadJobDto,
            List<IFormFile> files,
            [FromServices] IFileUploadService fileUploadService,
            [FromServices] IFftService fftService,
            [FromServices] ILogger<UploadController> logger)
        {
            if (!files.Any())
                return BadRequest("No files provided.");

            IEnumerable<FileUploadResult> fileUploadResults = null;
            using var requestTimer = RequestProcessingDuration.NewTimer();

            try
            {
                switch (uploadJobDto.ProcessingType)
                {
                    case ProcessingType.Sequential or ProcessingType.Simd:
                        fileUploadResults = await HandleFiles(uploadJobDto, files, fileUploadService, fftService);
                        break;
                    case ProcessingType.Parallel or ProcessingType.SimdParallel:
                        var handleFileTasks = files.Select(file => HandleFiles(uploadJobDto, new List<IFormFile> { file }, fileUploadService, fftService));
                        var resultLists = await Task.WhenAll(handleFileTasks);
                        fileUploadResults = resultLists.SelectMany(res => res);
                        break;
                    default:
                        throw new NotSupportedException(uploadJobDto.ProcessingType.ToString());
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.StackTrace);

                return StatusCode(StatusCodes.Status500InternalServerError, ex.StackTrace);
            }

            return Ok(fileUploadResults);
        }

        private static async Task<IEnumerable<FileUploadResult>> HandleFiles(UploadJobDto uploadJobDto, List<IFormFile> files, IFileUploadService fileUploadService, IFftService fftService)
        {
            var fileUploadResults = await fileUploadService.UploadFiles(files);

            int windowSize = uploadJobDto.WindowSize;
            double samplingRate = uploadJobDto.SamplingRate;
            bool saveResults = uploadJobDto.SaveResults;
            ProcessingType processingType = uploadJobDto.ProcessingType;

            await fftService.ProcessFileUploadResults(fileUploadResults, windowSize, samplingRate, saveResults, processingType);
            return fileUploadResults;
        }
    }
}

