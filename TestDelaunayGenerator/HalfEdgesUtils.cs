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

        //TODO Troika заменить на TriElement (в делонаторе нужно убрать треугольники, находящиеся вне области)
        /// <summary>
        /// Получить все ребра, окружающие конкретную вершину.
        /// </summary>
        /// <param name="halfEdges">массив полуребер</param>
        /// <param name="edgeId">полуребро, указывающее на общую вершину.
        /// Индекс полуребра, которое хранит индекс общей вершины в массиве треугольников.
        /// Сами же полуребра указывают на вершины, смежные с указанной общей
        /// </param>
        /// <returns>индексы в массиве треугольников, которые содержат смежные вершины с искомой общей.
        /// Все полуребра (индексы, которые они хранят) указывают на общую вершину/returns>
        public static int[] EdgesAroundVertex(int[] halfEdges, Troika[] triangles, int edgeId)
        {
//#if DEBUG
//            Console.WriteLine($"Стартовое ребро: {triangles[edgeId / 3][edgeId % 3]}-{triangles[halfEdges[edgeId] / 3][halfEdges[edgeId] % 3]}");
//#endif
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
            int adjacentEdgeId = halfEdges[edgeId];

            //true - сегмент замкнут, т.е. треугольники окружают точку вкруг
            bool isSegmentClosed = false;

            List<int> segmentHalfEdges = new List<int>();
            int incoming, outgoing;

            //incoming (start), outgoing, incoming
            List<(int, int, int)> debug = new List<(int, int, int)>();
            //обход по следующим ребрам
            for (incoming = adjacentEdgeId; ;)
            {
                int startIncoming = incoming;
                //помещаем текущее ребро в список ребер
                segmentHalfEdges.Add(incoming);

                //одно ребро
                outgoing = NextHalfEdge(incoming); //указывает на общую вершину
                incoming = halfEdges[outgoing]; //указывает на смежную с ней

                debug.Add((startIncoming, outgoing, incoming));
#if DEBUG
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
                if (incoming == -1)
                    break;
            }

            //сегмент замкнут, поэтому возвращаем все треугольники с первого прохода
            if (isSegmentClosed)
                return segmentHalfEdges.ToArray();

            //TODO System out of memory если выход закомментирован. Первое проверяемое ребро
            var debug2 = new List<(int, int, int)>();
            //обход по предыдущим ребрам (если сегмент не замкнут)
            for (incoming = adjacentEdgeId; ;)
            {
                int startIncoming = incoming;
                outgoing = halfEdges[incoming];

                //если нет смежного полуребра, то выходим
                if (outgoing == -1)
                    break;
                //до исключения дойти не должно. Поднимается для анализа аномалии обхода
                if (outgoing == adjacentEdgeId)
                    throw new ArgumentException($"Достигнута исходная вершина {adjacentEdgeId} при обратном обходе!");

                incoming = PrevHalfEdge(outgoing);

                debug2.Add((startIncoming, outgoing, incoming));
                segmentHalfEdges.Add(incoming);
            }

            return segmentHalfEdges.ToHashSet().ToArray();
        }

        /// <summary>
        /// Треугольники, смежные с вершиной, на которую указывает <paramref name="edgeId"/>
        /// </summary>
        /// <param name="halfEdges"></param>
        /// <param name="edgeId"></param>
        /// <returns></returns>
        public static int[] AdjacentTrianglesWithEdge(int[] halfEdges, Troika[] triangles, int edgeId)
        {
            int[] edgesAroundVertex = EdgesAroundVertex(halfEdges, triangles, edgeId);
            return edgesAroundVertex.Select(x => x / 3).ToArray();
        }
    }
}
