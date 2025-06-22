using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
        public static int NextHalfEdge(int curHalfEdge)
        {
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
        public static int PrevHalfEdge(int curHalfEdge)
        {
            int prevHalfEdge = -1;
            if (curHalfEdge % 3 == 0)
                prevHalfEdge = curHalfEdge + 2;
            else
                prevHalfEdge = curHalfEdge - 1;
            return prevHalfEdge;
        }

        //TODO можно добавить флаг на замкнутость сегмента, т.е. если сегмент замкнут,
        //то не делаем обход с другой стороны
        /// <summary>
        /// Получить все ребра, окружающие конкретную вершину.
        /// </summary>
        /// <param name="halfEdges">массив полуребер</param>
        /// <param name="startEdge">полуребро, указывающее на общую вершину.
        /// Индекс полуребра, которое хранит индекс общей вершины в массиве треугольников.
        /// Сами же полуребра указывают на вершины, смежные с указанной общей
        /// </param>
        /// <returns>индексы в массиве треугольников, которые содержат смежные вершины с искомой общей.
        /// Все полуребра (индексы, которые они хранят) указывают на общую вершину/returns>
        public static int[] EdgesAroundVertex(int[] halfEdges, int startEdge)
        {
            //true - сегмент замкнут, т.е. треугольники окружают точку вкруг
            bool isSegmentClosed = false;

            List<int> segmentHalfEdges = new List<int>();
            int incoming, outgoing;

            //обход по следующим ребрам
            for (incoming = startEdge; ;)
            {
                //помещаем текущее ребро в список ребер
                segmentHalfEdges.Add(incoming);

                outgoing = NextHalfEdge(incoming);
                incoming = halfEdges[outgoing];

                //достигли вершины, с которой начался цикл
                //поэтому сегмент замкнут
                if (incoming == startEdge)
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

            //обход по предыдущим ребрам (если сегмент не замкнут)
            for (incoming = startEdge; ;)
            {
                outgoing = halfEdges[incoming];

                //если нет смежного полуребра, то выходим
                if (outgoing == -1)
                    break;
                //до исключения дойти не должно. Поднимается для анализа аномалии обхода
                if (outgoing == startEdge)
                    throw new ArgumentException($"Достигнута исходная вершина {startEdge} при обратном обходе!");

                incoming = PrevHalfEdge(outgoing);

                segmentHalfEdges.Add(incoming);
            }

            return segmentHalfEdges.ToHashSet().ToArray();
        }

        /// <summary>
        /// Треугольники, смежные с вершиной, на которую указывает <paramref name="startEdge"/>
        /// </summary>
        /// <param name="halfEdges"></param>
        /// <param name="startEdge"></param>
        /// <returns></returns>
        public static int[] AdjacentTrianglesWithEdge(int[] halfEdges, int startEdge)
        {
            int[] edgesAroundVertex = EdgesAroundVertex(halfEdges, startEdge);
            return edgesAroundVertex.Select(x => x / 3).ToArray();
        }
    }
}
