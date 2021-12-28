using AR.P2.Manager.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AR.P2.Manager.Services
{
    public interface IFileUploadService
    {
        Task<IEnumerable<FileUploadResult>> UploadFiles(IEnumerable<IFormFile> formFiles);
    }
}
