using CommonLib.Geometry;
using GeometryLib.Locators;
using GeometryLib.Vector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using TestDelaunayGenerator.DCELMesh;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.SimpleStructures
{
    /// <summary>
    /// Сегмент, образованный из множество треугольников,
    /// в которые входит вершина <see cref="vid"/>,
    /// по сути образует центр сегмента
    /// </summary>
    public class AreaSegment
    {
        /// <summary>
        /// ID вершины
        /// </summary>
        public readonly int vid = -1;


        /// <summary>
        /// Принадлежность точки области триангуляции
        /// </summary>
        public PointStatus pointStatus;

        /// <summary>
        /// Полуребра, связанные с <see cref="vid"/>
        /// </summary>
        public int[] heIds;


        /// <summary>
        /// true - сегмент, центром которого является <see cref="vid"/>, является выпуклым
        /// </summary>
        public bool IsConvex
        {
            get
            {
                if (!IsConvexStatusSet)
                    throw new Exception($"Выпуклость для сегмента с центом в {vid} не была определена!");
                return isConvex;
            }
            set
            {
                isConvex = value;
                IsConvexStatusSet = true;
            }
        }
        protected bool isConvex = true;
        /// <summary>
        /// true - выпуклость области <see cref="IsConvex"/> была определена
        /// </summary>
        protected bool IsConvexStatusSet = false;

        /// <summary>
        /// Базовый конструктор, без определения выпуклости сегмента
        /// </summary>
        /// <param name="vid"></param>
        /// <param name="pointStatus"></param>
        /// <param name="halfEdgeIds"></param>
        public AreaSegment(int vid, PointStatus pointStatus, int[] halfEdgeIds)
        {
            this.vid = vid;
            this.pointStatus = pointStatus;
            this.heIds = halfEdgeIds;
        }

        /// <summary>
        /// Конструктор сегмента, который включает определение выпуклости сегмента <see cref="IsConvex"/>
        /// при помощи <paramref name="mesh"/>
        /// </summary>
        /// <param name="vid"></param>
        /// <param name="pointStatus"></param>
        /// <param name="heIds"></param>
        /// <param name="mesh"></param>
        public AreaSegment(int vid, PointStatus pointStatus, int[] heIds, IRestrictedDCEL mesh)
            : this(vid, pointStatus, heIds)
        {
            if (mesh is null)
                throw new ArgumentNullException("Не удалось определить выпуклость сегмента, т.к. сетка null");
            this.IsConvex = CheckIsConvex(mesh);
        }


        /// <summary>
        /// Получить треугольники, в которые входит вершина
        /// <see cref="vid"/>
        /// </summary>
        public int[] TriangleIds
        {
            get
            {
                IEnumerable<int> triangleIds;
                //если вершина граничная, то будет дубль одного треугольника
                //поэтому убираем этот дубль
                if (this.pointStatus == PointStatus.Boundary)
                    triangleIds = heIds.Select(x => x / 3).ToHashSet();
                else
                    triangleIds = heIds.Select(x => x / 3);
                return triangleIds.ToArray();
            }
        }

        /// <summary>
        /// Вершины, смежные с <see cref="vid"/>
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public int[] AdjacentVertexes(IRestrictedDCEL mesh)
        {
            return this.heIds.Select(halfEdge => mesh.Faces[halfEdge / 3][halfEdge % 3]).ToArray();
        }

        /// <summary>
        /// Определение выпуклости заданного сегмента.
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <remarks>Не перезаписывает <see cref="IsConvex"/></remarks>
        public bool CheckIsConvex(IRestrictedDCEL mesh)
        {
            //берем указатель (переменную) для упрощения записи
            var vid = this.vid;
            // вершины, формирующие сегмент
            int[] segmentVertexes = this.AdjacentVertexes(mesh);

            // vid - граничная
            // в начало массива вершин сегмента добавляем vid
            if (this.pointStatus == PointStatus.Boundary)
            {
                int[] tempSeg = segmentVertexes;
                segmentVertexes = new int[1 + tempSeg.Length];
                segmentVertexes[0] = vid;
                Array.Copy(tempSeg, 0, segmentVertexes, 1, tempSeg.Length);
            }

            //для замкнутых областей
            //условие для выявления аномалий
            if (this.pointStatus != PointStatus.Boundary)
            {
                //в первый треугольник должна входить и первая и последняя вершины
                int trid = this.heIds[this.heIds.Length - 1] / 3;
                //id вершин треугольника
                int[] trVertexes = new int[] { mesh.Faces[trid].i, mesh.Faces[trid].j, mesh.Faces[trid].k };
                if ((trVertexes.Contains(segmentVertexes[0]) &&
                    trVertexes.Contains(segmentVertexes[segmentVertexes.Length - 1])) == false)
                    throw new ArgumentException($"Общая вершина не граничная, но сегмент не замкнут!");

            }

            //произведение векторных произведений должно быть > 0
            //вычисление первого векторного произведенияы
            double firstVectorCross = CrossLineUtils.DoubledOrientArea(
                mesh.Points[segmentVertexes[segmentVertexes.Length - 1]],
                mesh.Points[segmentVertexes[0]],
                mesh.Points[segmentVertexes[1]]
                );

            //проход по вершинам, в которых нужно рассчитать векторное произведение
            //по сути угол формируется в вершине i
            for (int i = 1; i < segmentVertexes.Length; i++)
            {
                //в цикле проще собирать вершины угла
                var vertexes = new List<int>(3);
                for (int k = -1; k <= -1 + 2; k++)
                {
                    int angleVid = segmentVertexes[(segmentVertexes.Length + i + k) % segmentVertexes.Length];
                    vertexes.Add(angleVid);
                }
                double localCross = CrossLineUtils.DoubledOrientArea(
                    mesh.Points[vertexes[0]],
                    mesh.Points[vertexes[1]],
                    mesh.Points[vertexes[2]]
                    );

                //если произведение векторных произведений неположительное =>
                // перегиб в сегменте
                if (firstVectorCross * localCross <= 0)
                    return false;
            }

            return true;
        }
    }
}
