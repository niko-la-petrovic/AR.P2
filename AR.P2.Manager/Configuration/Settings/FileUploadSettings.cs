using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AR.P2.Manager.Configuration.Settings
{
    public class FileUploadSettings
    {
        public const string SectionName = "FileUploadSettings";
        public bool UseWebRoot { get; set; }
        public string FileUploadDirectoryPath { get; set; }
        public string FileUploadRequestPath { get; set; }
    }
}
