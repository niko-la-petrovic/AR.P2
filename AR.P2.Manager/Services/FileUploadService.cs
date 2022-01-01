using AR.P2.Manager.Configuration.Settings;
using AR.P2.Manager.Data;
using AR.P2.Manager.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AR.P2.Manager.Services
{
    public class FileUploadService : IFileUploadService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly FileUploadSettings _fileUploadSettings;
        private readonly string _webRootPath;
        private readonly ILogger _logger;

        public FileUploadService(
            ApplicationDbContext dbContext,
            FileUploadSettings fileUploadSettings,
            IWebHostEnvironment env,
            ILogger<FileUploadService> logger)
        {
            _dbContext = dbContext;
            _fileUploadSettings = fileUploadSettings;
            _logger = logger;
            _webRootPath = env.WebRootPath;
        }

        public async Task<IEnumerable<FileUploadResult>> UploadFiles(IEnumerable<IFormFile> formFiles)
        {
            var fileUploadResults = await Task.WhenAll(formFiles.Select(formFile => UploadFile(formFile)));

            return fileUploadResults.ToList();
        }

        public async Task<FileUploadResult> UploadFile(IFormFile formFile)
        {
            string destinationFileName = GetSanitizedFileName(formFile.FileName);
            string destinationFilePath = GetDestinationFilePath(destinationFileName);
            using (var destStream = File.OpenWrite(destinationFilePath))
            {
                _logger?.LogInformation($"Downloading '{formFile.FileName}' as '{destinationFileName}'.");
                
                await formFile.CopyToAsync(destStream);
            }

            string requestPath = string.Join("/", _fileUploadSettings.FileUploadRequestPath, destinationFileName);

            return new FileUploadResult
            {
                LocalPath = destinationFilePath,
                RequestPath = requestPath
            };
        }

        public string GetSanitizedFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedFileName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
            var timestampedFileName = Guid.NewGuid().ToString() + $"_{sanitizedFileName}";

            return timestampedFileName;
        }

        protected string GetDestinationFilePath(string fileName)
        {
            if (_fileUploadSettings.UseWebRoot)
                return Path.Join(_webRootPath, _fileUploadSettings.FileUploadDirectoryPath, fileName);

            return Path.Join(_fileUploadSettings.FileUploadDirectoryPath, fileName);
        }
    }
}
