using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator
{
    public class DelaunatorConfig
    {

        /// <summary>
        /// Отсечение точек выполняется параллельно
        /// </summary>
        /// <remarks>Имеет смысл, если <see cref="UseClippingPoints"/> включен</remarks>
        public bool ParallelClippingPoints { get; set; } = true;

        /// <summary>
        /// true - использовать отсечение точек
        /// </summary>
        public bool UseClippingPoints { get; set; } = true;

        /// <summary>
        /// Использовать алгоритм восстановления границы.
        /// </summary>
        public bool RestoreBorder { get; set; } = true;


        /// <summary>
        /// true - включить в сетку внешние треугольники
        /// </summary>
        public bool IncludeExtTriangles { get; set; } = false;
    }
}
