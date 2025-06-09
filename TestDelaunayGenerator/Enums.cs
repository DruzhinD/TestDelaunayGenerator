using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator
{
    /// <summary>
    /// Отношение принадлежности точки области
    /// </summary>
    public enum PointStatus
    {
        /// <summary>
        /// Внешний
        /// </summary>
        External = 0,
        /// <summary>
        /// Входит в область
        /// </summary>
        Internal = 10,
        /// <summary>
        /// Принадлежит границе
        /// </summary>
        Boundary = 20
    }


    /// <summary>
    /// Отношение принадлежности треугольника области
    /// </summary>
    public enum TriangleInfect
    {
        /// <summary>
        /// Треугольник внешний
        /// </summary>
        External = 10,
        /// <summary>
        /// треугольник принадлежит области
        /// </summary>
        Internal = 20,
    };
}
