namespace AR.P2.Manager.Dtos
{
    public class UploadJobDto
    {
        public string Name { get; set; }
        public int SamplingRate { get; set; } = 44100;
        public int WindowSize { get; set; } = 4096;
    }
}