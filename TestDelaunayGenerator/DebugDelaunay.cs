using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator
{
    public class DebugDelaunay
    {
        public DebugDelaunay(TriangleInfect[] triangleInfects, int[] triangleIds, int[] indexes, int[] halfEdges, int[] triangleVertexes, PointStatus[] pointStatuses)
        {
            TriangleInfects = triangleInfects;
            TriangleIds = triangleIds;
            Indexes = indexes;
            HalfEdges = halfEdges;
            TriangleVertexes = triangleVertexes;
            PointStatuses = pointStatuses;
        }

        /// <summary>
        /// вхождение треугольника в область,
        /// </summary>
        TriangleInfect[] TriangleInfects { get; set; }

        /// <summary>
        /// id треугольника (одинаковые значения идут тройками)
        /// </summary>
        int[] TriangleIds { get; set; }

        /// <summary>
        /// index в разрезе троек вершин
        /// </summary>
        int[] Indexes { get; set; }

        int[] HalfEdges { get; set; }

        /// <summary>
        /// triangles (тройки вершин, образующие треугольники)
        /// </summary>
        int[] TriangleVertexes { get; set; }

        /// <summary>
        /// point_status (принадлежность области)
        /// </summary>
        PointStatus[] PointStatuses { get; set; }

        public DebugDelaunayRecord[] Records
        {
            get
            {
                if (records is null)
                {
                    records = new DebugDelaunayRecord[TriangleInfects.Length];
                    for (int i = 0; i < TriangleInfects.Length; i++)
                    {
                        records[i] = new DebugDelaunayRecord(
                            TriangleInfects[i],
                            TriangleIds[i],
                            Indexes[i],
                            HalfEdges[i],
                            TriangleVertexes[i],
                            PointStatuses[i]);
                    }
                }
                return records;
            }
        }

        DebugDelaunayRecord[] records;
    }
}
