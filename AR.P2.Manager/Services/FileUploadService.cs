using AR.P2.Manager.Configuration.Settings;
using AR.P2.Manager.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public FileUploadService(
            ApplicationDbContext dbContext,
            FileUploadSettings fileUploadSettings,
            IWebHostEnvironment env)
        {
            _dbContext = dbContext;
            _fileUploadSettings = fileUploadSettings;
            _webRootPath = env.WebRootPath;
        }

        public async Task<IEnumerable<string>> UploadFiles(IEnumerable<IFormFile> formFiles)
        {
            var outputFilePaths = await Task.WhenAll(formFiles.Select(formFile => UploadFile(formFile)));

            return outputFilePaths.ToList();
        }

        public async Task<string> UploadFile(IFormFile formFile)
        {
            string destinationFilePath = GetDestinationFilePath(formFile);
            using var destStream = File.OpenWrite(destinationFilePath);

            await formFile.CopyToAsync(destStream);

            return destinationFilePath;
        }

        public string GetDestinationFilePath(IFormFile formFile)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedFileName = string.Join("_", formFile.FileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
            var timestampedFileName =  Guid.NewGuid().ToString() + $"_{sanitizedFileName}";

            if (_fileUploadSettings.UseWebRoot)
                return Path.Join(_webRootPath, _fileUploadSettings.FileUploadDirectoryPath, timestampedFileName);

            return Path.Join(_fileUploadSettings.FileUploadDirectoryPath, timestampedFileName);
        }
    }
}
