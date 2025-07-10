using MemLogLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator
{
    /// <summary>
    /// Набор методов для работы с полуребрами
    /// </summary>
    public static class HalfEdgesUtils
    {
        /// <summary>
        /// Следующее ребро в текущем треугольнике
        /// </summary>
        /// <param name="curHalfEdge">текущее полуребро</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static int NextHalfEdge(int curHalfEdge)
        {
            if (curHalfEdge == -1)
                throw new ArgumentException("Полуребро не может быть -1", nameof(curHalfEdge));

            int nextHalfEdge = -1;
            if (curHalfEdge % 3 == 2)
                nextHalfEdge = curHalfEdge - 2;
            else
                nextHalfEdge = curHalfEdge + 1;
            return nextHalfEdge;
        }

        /// <summary>
        /// Предыдущее ребро в текущем треугольнике
        /// </summary>
        /// <param name="curHalfEdge">текущее полуребро</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static int PrevHalfEdge(int curHalfEdge)
        {
            if (curHalfEdge == -1)
                throw new ArgumentException("Полуребро не может быть -1", nameof(curHalfEdge));

            int prevHalfEdge = -1;
            if (curHalfEdge % 3 == 0)
                prevHalfEdge = curHalfEdge + 2;
            else
                prevHalfEdge = curHalfEdge - 1;
            return prevHalfEdge;
        }

        /// <summary>
        /// Получить все ребра, окружающие вершину, на которую указывает <paramref name="edgeId"/>.
        /// </summary>
        /// <param name="halfEdges">полуребра</param>
        /// <param name="triangles">треугольники, связанные с <paramref name="halfEdges"/></param>
        /// <param name="edgeId">
        /// Полуребро, указывающее на вершину, вокруг которой требуется найти смежные ребра
        /// </param>
        /// <param name="include">
        /// true - учитывать при обходе против ч.с. полуребро,
        /// не являющееся смежным с общей вершиной,
        /// но при этом это ребро указывает на вершину, смежную с общей вершиной. <br/>
        /// Поэтому последнее <u>полуребро</u> не будет смежным с общей вершиной
        /// </param>
        /// <returns>полуребра, смежные с общей вершиной</returns>
        /// <remarks>
        /// Параметр <paramref name="include"/> уместен, если общая вершина является граничной.
        /// </remarks>
        public static int[] AdjacentEdgesVertex(int[] halfEdges, Troika[] triangles, int edgeId, bool include = false)
        {
            int vertexId = triangles[edgeId / 3][edgeId % 3];
            //если нет смежного полуребра, то ищем другое полуребро, которое связано с такой же вершиной
            if (halfEdges[edgeId] == -1)
            {
                //id треугольника и вершины в нем, переданные в качестве аргумента из edgeId
                int currentTriangleId = edgeId / 3;
                int currentVertexId = edgeId % 3;
                for (int i = 0; i < triangles.Length; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        //если глобальный id вершины совпал и есть смежное полуребро
                        if (triangles[i][j] == triangles[currentTriangleId][currentVertexId] &&
                            halfEdges[i * 3 + j] != -1)
                            //то используем его как исходное полуребро
                            edgeId = i * 3 + j;
                    }
                }
            }

            //true - сегмент замкнут, т.е. треугольники окружают точку вкруг
            bool isSegmentClosed = false;

            List<int> segmentHalfEdges = new List<int>();
            int incoming, outgoing;

            int adjacentEdgeId = halfEdges[edgeId];
            //обход против ч.с.
            for (incoming = adjacentEdgeId; ;)
            {
                int startIncoming = incoming;
                //помещаем текущее ребро в список ребер
                segmentHalfEdges.Add(incoming);

                //одно ребро
                outgoing = NextHalfEdge(incoming); //указывает на общую вершину
                incoming = halfEdges[outgoing]; //указывает на смежную с ней
#if DEBUG
                //outgoing должна указывать на vid
                if (triangles[outgoing / 3][outgoing % 3] != vertexId)
                {
                    string log = $"общая вершина ({nameof(outgoing)}) != {vertexId} " +
                        $"({triangles[startIncoming / 3][startIncoming % 3]}," +
                        $"{triangles[outgoing / 3][outgoing % 3]}," +
                        $"{triangles[incoming / 3][incoming % 3]})";

                    Utils.ConsoleWriteLineColored(
                        ConsoleColor.Magenta,
                        log
                        );
                }
#endif
                //достигли вершины, с которой начался цикл
                //поэтому сегмент замкнут
                if (incoming == adjacentEdgeId)
                {
                    isSegmentClosed = true;
                    break;
                }

                //нет смежного ребра
                //попадание только если vid является граничной
                if (incoming == -1)
                {
                    if (include)
                    {
                        //ребро указывает на вершину, смежную с vid
                        //но полуребра этих вершин не связаны
                        int nextBoundaryVid = NextHalfEdge(outgoing);
                        segmentHalfEdges.Add(nextBoundaryVid);
                    }

                    break;
                }
            }

            //сегмент замкнут, поэтому возвращаем все треугольники с первого прохода
            if (isSegmentClosed)
                return segmentHalfEdges.ToArray();

            //количество полуребер, полученное при обходе против ч.с.
            int firstRoundCnt = segmentHalfEdges.Count;
            // обход по ч.с.
            for (outgoing = edgeId; ;)
            {
                incoming = PrevHalfEdge(outgoing);
                segmentHalfEdges.Add(incoming);
                outgoing = halfEdges[incoming]; //указывает на vid

                if (outgoing == -1)
                    break;

                if (outgoing == edgeId)
                    //throw new ArgumentException($"Достигнута исходная вершина {adjacentEdgeId} при обратном обходе!");
                    break;
            }

            //полуребра без повторов
            segmentHalfEdges = segmentHalfEdges.ToHashSet().ToList();
            int[] returnHalfEdges = new int[segmentHalfEdges.Count];

            //заполняем начало массива полуребрами с прохода по ч.с. в обратном порядке
            int arrayIndex = 0;
            for (int i = segmentHalfEdges.Count - 1; i >= firstRoundCnt; i--)
            {
                returnHalfEdges[arrayIndex] = segmentHalfEdges[i];
                arrayIndex++;
            }

            //заполняем массив обходом против ч.с.
            for (int i = 0; i < firstRoundCnt; i++)
            {
                returnHalfEdges[arrayIndex] = segmentHalfEdges[i];
                arrayIndex++;
            }

            return returnHalfEdges;
        }


        /// <summary>
        /// Треугольники, смежные с вершиной, на которую указывает <paramref name="edgeId"/>
        /// </summary>
        /// <param name="halfEdges"></param>
        /// <param name="edgeId"></param>
        /// <returns></returns>
        public static int[] AdjacentTrianglesWithEdge(int[] halfEdges, Troika[] triangles, int edgeId)
        {
            int[] edgesAroundVertex = AdjacentEdgesVertex(halfEdges, triangles, edgeId, false);
            return edgesAroundVertex.Select(x => x / 3).ToArray();
        }

        /// <summary>
        /// Получить все полуребра, смежные с вершиной <paramref name="vid"/>.
        /// </summary>
        /// <param name="halfEdges"></param>
        /// <param name="triangles"></param>
        /// <param name="vid"></param>
        /// <returns></returns>
        [Obsolete("Медленный, не рекомендуется к использованию, лучше использовать EdgesAroundVertex")]
        public static int[] AdjacentVertexesWithVid(int[] halfEdges, Troika[] triangles, int vid)
        {
            //id треугольников, в которые входит вершина vid
            var trIds = new HashSet<int>();
            for (int edgeId = 0; edgeId < halfEdges.Length; edgeId++)
            {
                //if (triangles[edgeId / 3][edgeId % 3] == vid && halfEdges[edgeId] != -1)
                if (triangles[edgeId / 3][edgeId % 3] == vid)
                {
                    trIds.Add(edgeId / 3);
                    if (halfEdges[edgeId] != -1)
                        trIds.Add(halfEdges[edgeId] / 3);
                }
            }

            //вершины
            var vids = new HashSet<int>();
            foreach (int trid in trIds)
            {
                for (int i = 0; i < 3; i++)
                    vids.Add(triangles[trid][i]);
            }

            return vids.ToArray();
        }
    }
}
