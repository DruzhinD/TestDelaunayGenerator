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
        /// true - использовать отсечение треугольников
        /// </summary>
        /// <remarks>Использование без <see cref="RestoreBorder"/> 
        /// может повлечь разрушение сетки!
        /// <br/>
        /// Совместно с <see cref="IncludeExtTriangles"/> бесполезен</remarks>
        public bool ClippingTriangles { get; set; } = true;

        /// <summary>
        /// Отсечение точек выполняется параллельно
        /// </summary>
        public bool ParallelClippingPoints { get; set; } = true;

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
