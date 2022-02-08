using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AR.P2.Algo.Models
{
    public class WavHeader
    {
        public int BitDepth { get; set; }
        public int ChannelCount { get; set; }
        public int SamplingRate { get; set; }
        public int DataSectionByteCount { get; set; }
    }
}
