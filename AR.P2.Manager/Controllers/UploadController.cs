using AR.P2.Manager.Dtos;
using AR.P2.Manager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AR.P2.Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> PostUploadJobAsync(
            [FromForm] UploadJobDto uploadJobDto,
            List<IFormFile> files,
            [FromServices] IFileUploadService fileUploadService)
        {
            // TODO 
            if (!files.Any())
                return BadRequest("No files provided.");

            var outputFilePaths = await fileUploadService.UploadFiles(files);

            return Ok(outputFilePaths);
        }
    }
}

