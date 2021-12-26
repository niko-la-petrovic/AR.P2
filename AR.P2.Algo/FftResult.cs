using System;
using System.Collections.Generic;
using System.Text;

namespace AR.P2.Algo
{
    public class FftResult
    {
        public double SamplingRate { get; set; }
        public int WindowSize { get; set; }
        public List<SpectralComponent> SpectralComponents { get; set; }

        public override string ToString()
        {
            return $"{nameof(SamplingRate)}: {SamplingRate}, {nameof(WindowSize)}: {WindowSize}";
        }
    }

    public class SpectralComponent : IComparable
    {
        public double Frequency { get; set; }
        public double Magnitude { get; set; }

        public int CompareTo(object obj)
        {
            if (obj is SpectralComponent specComp)
                return Magnitude.CompareTo(specComp.Magnitude);

            return 1;
        }

        public override string ToString()
        {
            return $"{nameof(Frequency)}: {Frequency}, {nameof(Magnitude)}: {Magnitude}";
        }
    }
}
