using CommandLine;
using System;
using System.IO;
using static AR.P2.FileCreator.Program;

namespace AR.P2.FileCreator
{
    public class Program
    {
        public class Options
        {
            [Option('r', "sampling-rate", Required = false, HelpText = "Set the sampling rate.", Default = 44100)]
            public int SamplingRate { get; set; }
            [Option('l', "length", Required = false, HelpText = "Set the length of output in seconds.", Default = 1)]
            public int SecondLength { get; set; }
            [Option('o', "output", Required = false, HelpText = "Set the output file.", Default = "output.bin")]
            public string OutputFilePath { get; set; }
            [Option('f', "frequency", Required = false, HelpText = "Set the frequency of signal.", Default = 800)]
            public double Frequency { get; set; }
            [Option('a', "Amplitude", Required = false, HelpText = "Set the amplitude.", Default = 1)]
            public int Amplitude { get; set; }
            [Option('c', "Csv", Required = false, HelpText = "Set whether the output data should be CSV.", Default = false)]
            public bool Csv { get; set; }
        }

        static void Main(string[] args)
        {
            var parser = Parser.Default;
            var parserResult = parser
                .ParseArguments<Options>(args);

            parserResult.WithParsed(o =>
            {
                WriteToFile(o.MapToOptions());
            });
            parserResult.WithNotParsed(o =>
            {
                Console.WriteLine("Use --help.");
            });
        }

        public static void WriteToFile(FileCreatorOptions fileCreatorOptions)
        {
            var basePath = Directory.GetParent(fileCreatorOptions.OutputFilePath).FullName;
            var baseFileName = Path.GetFileNameWithoutExtension(fileCreatorOptions.OutputFilePath);
            var uncheckedExtension = Path.GetExtension(fileCreatorOptions.OutputFilePath);
            var fileExtension = string.IsNullOrWhiteSpace(uncheckedExtension) ? "" : uncheckedExtension;

            var outputFilePath = Path.Join(basePath, $"{baseFileName}_{fileCreatorOptions.SamplingRate}_{fileCreatorOptions.SecondLength}{fileExtension}");
            using var fs = File.OpenWrite(outputFilePath);
            using var writer = new StreamWriter(fs);

            double t = 0;
            for (long i = 0; i < fileCreatorOptions.SampleCount; i++)
            {
                var displacement = Math.Sin(fileCreatorOptions.AngularFrequency * t);

                if (fileCreatorOptions.Csv)
                    writer.WriteLine($"{t},{displacement}");
                else
                {
                    var bytes = BitConverter.GetBytes(displacement);
                    fs.Write(bytes);
                }
                t += fileCreatorOptions.SampleLength;
            }
        }

        public class FileCreatorOptions
        {
            public int SamplingRate { get; set; }
            public int SecondLength { get; set; }
            public string OutputFilePath { get; set; }
            public double Frequency { get; set; }
            public int Amplitude { get; set; }

            public bool Csv { get; set; }

            public long SampleCount => SamplingRate * SecondLength;
            public double AngularFrequency => 2 * Math.PI * Frequency;
            public double SampleLength => 1.0 / SamplingRate;
        }
    }

    public static class FileCreatorExtensions
    {
        public static FileCreatorOptions MapToOptions(this Options options)
        {
            return new()
            {
                Amplitude = options.Amplitude,
                Frequency = options.Frequency,
                OutputFilePath = options.OutputFilePath,
                SamplingRate = options.SamplingRate,
                SecondLength = options.SecondLength,
                Csv = options.Csv,
            };
        }
    }
}
