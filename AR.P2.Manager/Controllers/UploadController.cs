using AR.P2.Manager.Dtos;
using AR.P2.Manager.Models;
using AR.P2.Manager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Prometheus;
using System.Collections.Generic;
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
        // TODO add logging
        // TODO exception handling
        private static readonly Histogram RequestProcessingDuration = Metrics.CreateHistogram("mgr_request_processing_duration_seconds", "Histogram of request processing durations.");

        [HttpPost]
        public async Task<IActionResult> PostUploadJobAsync(
            [FromForm] UploadJobDto uploadJobDto,
            List<IFormFile> files,
            [FromServices] IFileUploadService fileUploadService,
            [FromServices] IFftService fftService)
        {
            if (!files.Any())
                return BadRequest("No files provided.");

            using var requestTimer = RequestProcessingDuration.NewTimer();

            var fileUploadResults = await fileUploadService.UploadFiles(files);

            int windowSize = uploadJobDto.WindowSize;
            double samplingRate = uploadJobDto.SamplingRate;
            bool saveResults = uploadJobDto.SaveResults;
            ProcessingType processingType = uploadJobDto.ProcessingType;

            await fftService.ProcessFileUploadResults(fileUploadResults, windowSize, samplingRate, saveResults, processingType);

            return Ok(fileUploadResults);
        }

    }
}

