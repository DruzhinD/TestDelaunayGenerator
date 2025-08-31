using System.Collections.Generic;
using System.Data;
using System.Linq;
using TestDelaunayGenerator.SimpleStructures;
using TestDelaunayGenerator.DCELMesh;

namespace TestDelaunayGenerator.SimpleStructures
{
    /// <summary>
    /// Сегмент, образованный из множество треугольников,
    /// в которые входит вершина <see cref="VertexID"/>,
    /// по сути образует центр сегмента
    /// </summary>
    public class Segment
    {
        /// <summary>
        /// ID вершины
        /// </summary>
        public readonly int VertexID = -1;


        /// <summary>
        /// Принадлежность точки области триангуляции
        /// </summary>
        public PointStatus pointStatus;

        /// <summary>
        /// Полуребра, связанные с <see cref="VertexID"/>
        /// </summary>
        public int[] halfEdgeIds;


        /// <summary>
        /// true - сегмент, центром которого является <see cref="VertexID"/>, является выпуклым
        /// </summary>
        public bool isConvex = true;

        public Segment(int vid, PointStatus pointStatus, int[] halfEdgeIds, bool isConvex = true)
        {
            this.VertexID = vid;
            this.pointStatus = pointStatus;
            this.halfEdgeIds = halfEdgeIds;
            this.isConvex = isConvex;
        }

        /// <summary>
        /// Получить треугольники, в которые входит вершина
        /// <see cref="VertexID"/>
        /// </summary>
        public int[] TriangleIds
        {
            get
            {
                IEnumerable<int> triangleIds;
                //если вершина граничная, то будет дубль одного треугольника
                //поэтому убираем этот дубль
                if (this.pointStatus == PointStatus.Boundary)
                    triangleIds = halfEdgeIds.Select(x => x / 3).ToHashSet();
                else
                    triangleIds = halfEdgeIds.Select(x => x / 3);
                return triangleIds.ToArray();
            }
        }

        /// <summary>
        /// Вершины, смежные с <see cref="VertexID"/>
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public int[] AdjacentVertexes(IRestrictedDCEL mesh)
        {
            return this.halfEdgeIds.Select(halfEdge => mesh.Faces[halfEdge / 3][halfEdge % 3]).ToArray();
        }

    }
}
