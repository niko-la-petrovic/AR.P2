using System;
using System.Collections.Generic;

namespace AR.P2.Algo
{
    public class FftResult
    {
        public double SamplingRate { get; set; }
        public int WindowSize { get; set; }
        public List<SpectralComponent> SpectralComponents { get; set; }

        public override bool Equals(object obj)
        {
            return obj is FftResult result &&
                   SamplingRate == result.SamplingRate &&
                   WindowSize == result.WindowSize &&
                   EqualityComparer<List<SpectralComponent>>.Default.Equals(SpectralComponents, result.SpectralComponents);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SamplingRate, WindowSize, SpectralComponents);
        }

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

        public override bool Equals(object obj)
        {
            return obj is SpectralComponent component &&
                   Frequency == component.Frequency &&
                   Magnitude == component.Magnitude;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Frequency, Magnitude);
        }

        public override string ToString()
        {
            return $"{nameof(Frequency)}: {Frequency}, {nameof(Magnitude)}: {Magnitude}";
        }
    }
}
