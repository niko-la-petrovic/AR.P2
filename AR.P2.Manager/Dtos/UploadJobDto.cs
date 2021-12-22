using AR.P2.Manager.Models;

namespace AR.P2.Manager.Dtos
{
    public class UploadJobDto
    {
        // TODO add null checks with assignment
        public string Name { get; set; }
        public ProcessingType ProcessingType { get; set; }
        public int SamplingRate { get; set; } = 44100;
        public int WindowSize { get; set; } = 4096;
    }
}