using CommonLib.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.DCELMesh
{
    /// <summary>
    /// Формат хранения сетки DCEL (Doubly Connected Edge List)
    /// с ограничениями на сетку, в т.ч. формирование невыпуклой триангуляции.
    /// </summary>
    public interface IRestrictedDCEL
    {
        /// <summary>
        /// Массив вершин
        /// </summary>
        IHPoint[] Points { get; set; }

        /// <summary>
        /// Полуребра
        /// </summary>
        int[] HalfEdges { get; set; }

        /// <summary>
        /// отношение точек к области (внутренняя/внешняя/граница и пр.)
        /// </summary>
        PointStatus[] PointStatuses { get; set; }

        /// <summary>
        /// треугольники, содержащие тройки индексов вершин. <br/>
        /// Примечание: если развернуть в массив длиной 3*<see cref="Triangles.Length"/>
        /// то индексация будет совпадать с <see cref="HalfEdges"/>
        /// </summary>
        Troika[] Faces { get; set; }


        /// <summary>
        /// массив граничных вершин,
        /// содержит 2 соседних вершины на граничном ребре по отношению к текущему,
        /// индексация совпадает с <see cref="Points"/>. <br/>
        /// Примечание: часть вершин содержит значения по умолчанию (0),
        /// т.к. не являются граничными, но для индексации под них выделяется память
        /// </summary>
        EdgeIndex[] BoundaryEdges { get; set; }

        /// <summary>
        /// Получить индекс вершины, расположенной в по индексу <paramref name="index"/>
        /// в массиве последовательных троек вершин треугольников
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        int GetVertex(int index);
    }
}
