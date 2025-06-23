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
        public DebugDelaunayRecord(TriangleInfect areaStatus, int triangleId, int index, int halfEdge, int triangleVertex, PointStatus pointStatus)
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
        TriangleInfect AreaStatus { get; set; }

        /// <summary>
        /// ID треугольника
        /// </summary>
        int TriangleId { get; set; }

        /// <summary>
        /// индекс в разрезе троек вершин всех треугольников
        /// </summary>
        int Index { get; set; }

        /// <summary>
        /// Полуребро
        /// </summary>
        int HalfEdge { get; set; }


        /// <summary>
        /// ID вершины
        /// </summary>
        int TriangleVertex { get; set; }

        /// <summary>
        /// Вхождение точки в область
        /// </summary>
        PointStatus PointStatus { get; set; }
    }
}
