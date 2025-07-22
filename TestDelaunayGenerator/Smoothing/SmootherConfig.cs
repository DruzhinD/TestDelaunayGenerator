using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.Smoothing
{
    //TODO добавить валидацию параметров
    /// <summary>
    /// Конфигурация сглаживания
    /// </summary>
    public class SmootherConfig
    {
        public SmootherConfig(
            int smoothRatio = 1,
            int iterationsCount = 10)
        {
            SmoothRatio = smoothRatio;
            IterationsCount = iterationsCount;
        }

        /// <summary>
        /// Коэффициент сглаживания (КС)
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

        /// <summary>
        /// Минимально допустимый угол треугольника
        /// </summary>
    }
}
