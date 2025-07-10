using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.Smoothing
{
    /// <summary>
    /// Конфигурация сглаживания
    /// </summary>
    public class SmootherConfig
    {
        public SmootherConfig(
            int smoothRatio = 1,
            int iterationsCount = 10,
            int attemtCnt = 3,
            double reductionRatio = 0.5)
        {
            SmoothRatio = smoothRatio;
            IterationsCount = iterationsCount;
            AttemptCnt = attemtCnt;
            ReductionRatio = reductionRatio;
        }

        /// <summary>
        /// Коэффициент сглаживания
        /// </summary>
        public double SmoothRatio { get; set; } = 1;

        /// <summary>
        /// Количество итераций сглаживания
        /// </summary>
        public int IterationsCount { get; set; } = 10;

        /// <summary>
        /// Количество попыток сглаживания вершины в сегменте.
        /// </summary>
        public int AttemptCnt { get; set; } = 3;

        /// <summary>
        /// Уменьшить коэффициент сглаживания <see cref="SmoothRatio"/>,
        /// в случае неуспешной попытки перемещения вершины
        /// </summary>
        public double ReductionRatio { get; set; } = 0.5;
    }
}
