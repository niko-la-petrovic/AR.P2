using AR.P2.Manager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AR.P2.Manager.Services
{
    public interface IFftService
    {
        Task ProcessFileUploadResults(IEnumerable<FileUploadResult> fileUploadResults, int windowSize, double samplingRate, bool saveResults, ProcessingType processingType);
    }
}