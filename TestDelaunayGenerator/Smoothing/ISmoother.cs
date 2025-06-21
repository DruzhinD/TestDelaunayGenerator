using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator;

namespace TestDelaunayGenerator.Smoothing
{
    /// <summary>
    /// Сглаживатель
    /// </summary>
    public interface ISmoother
    {
        /// <summary>
        /// Применить сглаживание
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="smoothRatio">Коэффициент сглаживания в пределах [0, 1],
        /// где 1 - полное сглаживание</param>
        void Smooth(ExtendedTriMesh mesh, double smoothRatio = 1);
    }
}
