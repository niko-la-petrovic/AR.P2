using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AR.P2.Algo
{
    public partial class Operations
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
                FftCalc(signalLength, spectralComponents, halfSignalLength, evenSpectralComponents, oddSpectralComponents, i);
            }

            return spectralComponents;
        }

        private static unsafe void FftCalc(
            int signalLength,
            List<Complex> spectralComponents,
            int halfSignalLength,
            List<Complex> evenSpectralComponents,
            List<Complex> oddSpectralComponents,
            int i)
        {
            var cosSin = Complex.FromPolarCoordinates(1, -2 * Math.PI * i / signalLength);
            Complex oddOffsetSpectralComponent = cosSin * oddSpectralComponents[i];

            spectralComponents[i] = evenSpectralComponents[i] + oddOffsetSpectralComponent;
            spectralComponents[halfSignalLength + i] = evenSpectralComponents[i] - oddOffsetSpectralComponent;
        }

        public static double AprxSin(double x)
        {
            const double B = 4.0 / Math.PI;
            const double C = -4.0 / (Math.PI * Math.PI);

            double y = B * x + C * x * Math.Abs(x);
            const double P = 0.225;
            y = P * (y * Math.Abs(y) - y) + y;

            return y;
        }

        public static double AprxCos(double x)
        {
            var comparisonResult = x > Math.PI / 2.0 ? 1 : 0;
            var comparisonResultToDouble = (double)comparisonResult;
            var modified = x + comparisonResultToDouble * -Math.PI;
            var aprxSin = AprxSin(modified + Math.PI / 2.0);

            return aprxSin + comparisonResult * -2 * aprxSin;
        }

        private static readonly Vector128<double> _negTwoPi = new Vector<double>(-2 * Math.PI).AsVector128();
        private static readonly double[] _indexIndices = new double[4] { 0, 1, 0, 1 };
        private static readonly Vector128<double> _indicesVector = new Vector<double>(_indexIndices).AsVector128();

        public static unsafe Complex[] FftSimdRecurse(double* signal, int signalLength)
        {
            var spectralComponents = new Complex[signalLength];

            if (signalLength == 1)
            {
                spectralComponents[0] = new Complex(signal[0], 0);
                return spectralComponents;
            }

            int halfSignalLength = signalLength / 2;

            double[] evenSignal = new double[halfSignalLength];
            double[] oddSignal = new double[halfSignalLength];

            int i = 0;
            for (; i < halfSignalLength; i++)
            {
                evenSignal[i] = signal[i * 2];
                oddSignal[i] = signal[i * 2 + 1];
            }

            Complex[] evenSpectralComponents;
            Complex[] oddSpectralComponents;
            fixed (double* evenSignalPtr = evenSignal)
            {
                evenSpectralComponents = FftSimdRecurse(evenSignalPtr, halfSignalLength);
            }
            fixed (double* oddSignalPtr = oddSignal)
            {
                oddSpectralComponents = FftSimdRecurse(oddSignalPtr, halfSignalLength);
            }

            fixed (Complex* specCompsPtr = spectralComponents)
            {
                fixed (Complex* evenSpecCompsPtr = evenSpectralComponents)
                {
                    fixed (Complex* oddSpecCompsPtr = oddSpectralComponents)
                    {
                        var signalLenVec = Vector128.Create((double)signalLength);
                        Span<double> cosSinArray = stackalloc double[4];
                        for (i = 0; i + 2 <= halfSignalLength; i += 2)
                        {
                            var iVector = Vector128.Create((double)i);

                            var currentIndicesVector = Avx.Add(iVector, _indicesVector);
                            var constMultipliedVector = Avx.Multiply(currentIndicesVector, _negTwoPi);
                            var thetaVector = Avx.Divide(constMultipliedVector, signalLenVec);
                            // order in thetaVector: theta1, theta2
                            Vector128<double> cosV = LessAccurateCos128(thetaVector);
                            Vector128<double> sinV = Sin128(thetaVector);
                            // store in vector as: theta1, theta2, theta1, theta2
                            Vector256<double> cosSinV;
                            fixed (double* cosSinArrPtr = cosSinArray)
                            {
                                Avx.Store(cosSinArrPtr, cosV);
                                Avx.Store(cosSinArrPtr + 2, sinV);
                                cosSinV = Avx.LoadVector256(cosSinArrPtr);
                                cosSinV = Avx2.Permute4x64(cosSinV, 216);
                                //var inLaneShuffled = Avx.Shuffle(cosSinV, cosSinV, 5);
                                //var lanePermuted = Avx.Permute2x128(inLaneShuffled, inLaneShuffled, 1);
                                //cosSinV = Avx.Blend(cosSinV, lanePermuted, 6);
                            }

                            // oddSpecComps[i..i+1] * cossSinV : complex
                            var oddSpecCompsV = Avx.LoadVector256((double*)(oddSpecCompsPtr + i));

                            var bSwap = Avx.Shuffle(oddSpecCompsV, oddSpecCompsV, 5);
                            var aIm = Avx.Shuffle(cosSinV, cosSinV, 15);
                            var aRe = Avx.Shuffle(cosSinV, cosSinV, 0);
                            var aImBSwap = Avx.Multiply(aIm, bSwap);
                            // re1, im1, re2, im2
                            var oddOffsetSpecComp = Fma.MultiplyAddSubtract(aRe, oddSpecCompsV, aImBSwap);

                            // Adjust output spectral components

                            Vector256<double> evenSpecCompsV = Avx.LoadVector256((double*)(evenSpecCompsPtr + i));

                            var ithSpecComp = Avx.Add(evenSpecCompsV, oddOffsetSpecComp);
                            Avx.Store((double*)(specCompsPtr + i), ithSpecComp);

                            var otherIthSpecComp = Avx.Subtract(evenSpecCompsV, oddOffsetSpecComp);
                            Avx.Store((double*)(specCompsPtr + halfSignalLength + i), otherIthSpecComp);
                        }
                    }
                }
            }
            for (; i < halfSignalLength; i++)
            {
                Complex oddOffsetSpectralComponent = Complex.FromPolarCoordinates(1, -2 * Math.PI * i / signalLength) * oddSpectralComponents[i];

                spectralComponents[i] = evenSpectralComponents[i] + oddOffsetSpectralComponent;
                spectralComponents[halfSignalLength + i] = evenSpectralComponents[i] - oddOffsetSpectralComponent;
            }

            return spectralComponents;
        }

        private static readonly Vector128<double> _pi = new Vector<double>(Math.PI).AsVector128();
        private static readonly Vector128<double> _negPi = new Vector<double>(-Math.PI).AsVector128();
        private static readonly Vector128<double> _two = new Vector<double>(2.0).AsVector128();
        private static readonly Vector128<double> _negTwo = new Vector<double>(-2.0).AsVector128();
        private static readonly Vector128<double> _fourDivPi = new Vector<double>(4.0 / Math.PI /*+ 0.000000000001*/).AsVector128();
        private static readonly Vector128<double> _negFourDivPiSq = new Vector<double>(-4.0 / (Math.PI * Math.PI)).AsVector128();
        private static readonly Vector128<double> _piHalf = new Vector<double>(Math.PI / 2).AsVector128();
        private static readonly Vector128<double> _p = new Vector<double>(0.225).AsVector128();
        private static readonly Vector128<double> _positiveSignMask = new Vector<long>(0x7fffffffffffffff).AsVector128().AsDouble();

        public static Vector128<double> LessAccurateCos128(Vector128<double> x)
        {
            var xAddPiHalf = Avx.Add(x, _piHalf);
            var cos = Sin128(xAddPiHalf);

            return cos;
        }

        public static Vector128<double> Cos128(Vector128<double> x)
        {
            var negComparisonResult = Avx.CompareGreaterThan(x, _piHalf);

            var negComparisonResultDouble = Avx.ConvertToVector128Double(negComparisonResult.AsInt32());
            var comparisonMultiplier = Avx.Multiply(negComparisonResultDouble, _pi);
            var modified = Avx.Add(x, comparisonMultiplier);

            var xAddPiHalf = Avx.Add(modified, _piHalf);
            var sin = Sin128(xAddPiHalf);

            var negTwoComparison = Avx.Multiply(negComparisonResultDouble, _two);
            var negTwoSin = Avx.Multiply(negTwoComparison, sin);
            var cos = Avx.Add(sin, negTwoSin);

            return cos;
        }

        public static Vector128<double> Sin128(Vector128<double> x)
        {
            var bTimesX = Avx.Multiply(x, _fourDivPi);

            var cTimesX = Avx.Multiply(x, _negFourDivPiSq);
            Vector128<double> absX = Abs(x);
            var cTimesXTimesAbsX = Avx.Multiply(cTimesX, absX);

            var y = Avx.Add(bTimesX, cTimesXTimesAbsX);

            var yMultAbsY = Avx.Multiply(y, Abs(y));
            var yMultAbsYSubY = Avx.Subtract(yMultAbsY, y);
            var pTimesYMultAbsYSubY = Avx.Multiply(yMultAbsYSubY, _p);
            var pTimesYMultAbsYSubYAddY = Avx.Add(pTimesYMultAbsYSubY, y);

            return pTimesYMultAbsYSubYAddY;
        }

        /// <summary>
        /// Calculates Abs on every double in the vector.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static Vector128<double> Abs(Vector128<double> x)
        {
            // The first bit in a float and double is the sign bit.If set to 0, the result will be the absolute value.
            var abs = Avx.And(x, _positiveSignMask);

            return abs;
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

        public unsafe static FftResult GetFftResult(Complex[] complexSpecComps, double samplingRate, int windowSize)
        {
            if (!IsPowerTwo(windowSize))
                throw new InvalidOperationException("Window size must be a power of 2.");

            var specComps = new List<SpectralComponent>(new SpectralComponent[complexSpecComps.Length]);

            for (int i = 0; i < complexSpecComps.Length; i++)
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
