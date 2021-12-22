using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace AR.P2.Algo
{
    public class Operations
    {
        public unsafe static List<Complex> Fft(double[] signal)
        {
            if (!IsPowerTwo(signal?.Length ?? 0))
                throw new InvalidOperationException("Signal length must be a power of 2.");

            GCHandle pinnedSignal = GCHandle.Alloc(signal, GCHandleType.Pinned);
            IntPtr signalPtr;
            List<Complex> spectralComponents;
            try
            {
                signalPtr = pinnedSignal.AddrOfPinnedObject();

                spectralComponents = FftRecurse((double*)signalPtr.ToPointer(), signal.Length);
            }
            finally
            {
                pinnedSignal.Free();
            }

            return spectralComponents;
        }

        public static unsafe List<Complex> FftRecurse(double* signal, int signalLength)
        {
            List<Complex> spectralComponents = new List<Complex>(new Complex[signalLength]);

            if (signalLength == 1)
            {
                spectralComponents[0] = new Complex(signal[0], 0);
                return spectralComponents;
            }

            int halfSignalLength = signalLength / 2;

            double[] evenSignal = new double[halfSignalLength];
            double[] oddSignal = new double[halfSignalLength];

            for (int i = 0; i < halfSignalLength; i++)
            {
                evenSignal[i] = signal[i * 2];
                oddSignal[i] = signal[i * 2 + 1];
            }

            List<Complex> evenSpectralComponents;
            List<Complex> oddSpectralComponents;
            fixed (double* evenSignalPtr = evenSignal)
            {
                evenSpectralComponents = FftRecurse(evenSignalPtr, halfSignalLength);
            }
            fixed (double* oddSignalPtr = oddSignal)
            {
                oddSpectralComponents = FftRecurse(oddSignalPtr, halfSignalLength);
            }

            for (int i = 0; i < halfSignalLength; i++)
            {
                Complex oddOffsetSpectralComponent = Complex.FromPolarCoordinates(1, -2 * Math.PI * i / signalLength) * oddSpectralComponents[i];

                spectralComponents[i] = evenSpectralComponents[i] + oddOffsetSpectralComponent;
                spectralComponents[halfSignalLength + i] = evenSpectralComponents[i] - oddOffsetSpectralComponent;
            }


            return spectralComponents;
        }

        public unsafe static FftResult GetFftResult(List<Complex> complexSpecComps, double samplingRate, int windowSize)
        {
            if (!IsPowerTwo(windowSize))
                throw new InvalidOperationException("Window size must be a power of 2.");

            var specComps = new List<SpectralComponent>(new SpectralComponent[complexSpecComps.Count]);

            for (int i = 0; i < complexSpecComps.Count; i++)
            {
                specComps[i] = new SpectralComponent
                {
                    Frequency = i * samplingRate / windowSize,
                    Magnitude = complexSpecComps[i].Magnitude
                };
            }

            return new FftResult
            {
                SamplingRate = samplingRate,
                WindowSize = windowSize,
                SpectralComponents = specComps
            };
        }

        public SpectralComponent GetMaxComponent(IEnumerable<SpectralComponent> spectralComponents)
        {
            return spectralComponents.Max();
        }

        public static bool IsPowerTwo(int n)
        {
            return (n & (n - 1)) == 0;
        }
    }
}
