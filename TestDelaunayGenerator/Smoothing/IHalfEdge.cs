using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.Smoothing
{
    /// <summary>
    /// Содержит свойства для работы с полуребрами в неодносвязном контуре
    /// </summary>
    public interface IHalfEdge
    {
        /// <summary>
        /// Полуребра
        /// </summary>
        int[] HalfEdges { get; set; }

        /// <summary>
        /// Принадлежность точек области. Хранит точки, входящие в границу
        /// </summary>
        PointStatus[] PointStatuses { get; set; }

        Troika[] Triangles { get; set; }

        /// <summary>
        /// Получить индекс вершины, расположенной в по индексу <paramref name="index"/>
        /// в массиве последовательных троек вершин треугольников
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        int GetVertex(int index);
    }
}
