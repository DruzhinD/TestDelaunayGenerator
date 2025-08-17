using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator
{
    /// <summary>
    /// Строка отладки структуры делонатора. Схема <see cref="DebugDelaunay"/>
    /// </summary>
    public class DebugDelaunayRecord
    {
        public DebugDelaunayRecord(TriangleState areaStatus, int triangleId, int index, int halfEdge, int triangleVertex, PointStatus pointStatus)
        {
            AreaStatus = areaStatus;
            TriangleId = triangleId;
            Index = index;
            HalfEdge = halfEdge;
            TriangleVertex = triangleVertex;
            PointStatus = pointStatus;
        }

        /// <summary>
        /// Вхождение треугольника в область
        /// </summary>
        public TriangleState AreaStatus { get; set; }

        /// <summary>
        /// ID треугольника
        /// </summary>
        public int TriangleId { get; set; }

        /// <summary>
        /// индекс в разрезе троек вершин всех треугольников
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Полуребро
        /// </summary>
        public int HalfEdge { get; set; }


        /// <summary>
        /// ID вершины
        /// </summary>
        public int TriangleVertex { get; set; }

        /// <summary>
        /// Вхождение точки в область
        /// </summary>
        public PointStatus PointStatus { get; set; }
    }
}
