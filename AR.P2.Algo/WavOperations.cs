using AR.P2.Algo.Models;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AR.P2.Algo
{
    public static class WavOperations
    {
        public static WavHeader ParseWavHeader(this Stream fs)
        {
            int bitDepth;
            int channelCount;
            int samplingRate;
            int dataSectionByteCount;

            var headerBuffer = new byte[44];
            var headerSpan = headerBuffer.AsSpan();
            fs.Read(headerBuffer);

            samplingRate = BitConverter.ToInt32(headerSpan.Slice(24, 4));
            bitDepth = BitConverter.ToInt16(headerSpan.Slice(34, 2));
            dataSectionByteCount = BitConverter.ToInt32(headerSpan.Slice(40, 4));
            channelCount = BitConverter.ToInt16(headerSpan.Slice(22, 2));

            var wavHeader = new WavHeader
            {
                BitDepth = bitDepth,
                ChannelCount = channelCount,
                DataSectionByteCount = dataSectionByteCount,
                SamplingRate = samplingRate
            };

            return wavHeader;
        }

        public static double[] GetWavSignalMono(this Stream fs)
        {
            var header = fs.ParseWavHeader();
            if (header.BitDepth != 16
                || header.ChannelCount != 1
                || header.SamplingRate != 44100)
                throw new NotSupportedException(nameof(header));

            var dataBytesBuffer = new short[header.DataSectionByteCount / 2];
            fs.Read(MemoryMarshal.Cast<short, byte>(dataBytesBuffer));

            var result = Array.ConvertAll(dataBytesBuffer, s => (double)s);

            return result;
        }
    }
}
