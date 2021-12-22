using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AR.P2.Manager.Models
{
    public enum ProcessingType
    {
        Sequential,
        Parallel,
        Simd,
        SimdParallel
    }
}
