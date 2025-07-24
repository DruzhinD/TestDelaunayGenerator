using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.Smoothing
{
    /// <summary>
    /// Параметры улучшения сетки
    /// </summary>
    public class QualityMesherConfig
    {
        public double MinAngle { get; set; } = Math.PI / 6;

        /// <summary>
        /// Максимально допустимый угол треугольника
        /// </summary>
        public double MaxAngle { get; set; } = Math.PI * 3.5 / 6;


        /// <summary>
        /// На какое количество равных треугольников поделить исходный с недопустимым углом.
        /// Допущения для угла задаются через 
        /// <see cref="MinAngle"/> и
        /// <see cref="MaxAngle"/>
        /// </summary>
        public int SplitTriangleParts { get; set; } = 2;

        /// <summary>
        /// true - разделены на части будут только граничные треугольники с недопустимым углом
        /// </summary>
        public bool RebuildOnlyBoundary { get; set; } = true;
    }
}
