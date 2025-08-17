using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.SimpleStructures
{
    /// <summary>
    /// Отношение принадлежности точки области
    /// </summary>
    public enum PointStatus
    {
        None = 0,
        /// <summary>
        /// Внешний
        /// </summary>
        External = 1,
        /// <summary>
        /// Входит в область
        /// </summary>
        Internal = 2,
        /// <summary>
        /// Принадлежит границе
        /// </summary>
        Boundary = 3
    }


    /// <summary>
    /// Отношение принадлежности треугольника области
    /// </summary>
    public enum TriangleState
    {
        /// <summary>
        /// Значение по умолчанию, треугольник не имеет состояния
        /// </summary>
        /// <remarks>При инициализации структур также является значением по умолчанию</remarks>
        None = 0,
        /// <summary>
        /// Треугольник внешний
        /// </summary>
        External = 1,
        /// <summary>
        /// треугольник принадлежит области
        /// </summary>
        Internal = 2,
        
        /// <summary>
        /// треугольник удален из оболочки
        /// </summary>
        Deleted = -1
    };
}
