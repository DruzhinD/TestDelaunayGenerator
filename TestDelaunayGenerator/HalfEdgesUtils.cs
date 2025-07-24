using MemLogLib;
using Serilog;
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

        #region Стандартные HalfEdge
        /// <summary>
        /// Следующее ребро в текущем треугольнике
        /// </summary>
        /// <param name="he">текущее полуребро</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static int Next(int he)
        {
            ValidateParam(he, nameof(he));

            int nextHe = -1;
            if (he % 3 == 2)
                nextHe = he - 2;
            else
                nextHe = he + 1;
            return nextHe;
        }

        /// <summary>
        /// Предыдущее ребро в текущем треугольнике
        /// </summary>
        /// <param name="he">текущее полуребро</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static int Prev(int he)
        {
            ValidateParam(he, nameof(he));

            int prevHe = -1;
            if (he % 3 == 0)
                prevHe = he + 2;
            else
                prevHe = he - 1;
            return prevHe;
        }

        /// <summary>
        /// получить id вершины, на которую указывает полуребро
        /// </summary>
        /// <param name="faces"></param>
        /// <param name="he"></param>
        /// <returns></returns>
        public static int Origin(IList<Troika> faces, int he)
        {
            return faces[he / 3][he % 3];
        }

        /// <summary>
        /// Получить двойственное (смежное) полуребро для <paramref name="he"/>
        /// </summary>
        /// <param name="halfEdges"></param>
        /// <param name="he"></param>
        /// <returns></returns>
        public static int Twin(IList<int> halfEdges, int he)
        {
            return halfEdges[he];
        }

        /// <summary>
        /// Является ли ребро граничным
        /// </summary>
        /// <returns></returns>
        public static bool IsBoundary(IList<int> halfEdges, int he)
        {
            int twin = Twin(halfEdges, he);
            return twin == -1;
        }

        /// <summary>
        /// Связать 2 полуребра
        /// </summary>
        /// <param name="halfEdges"></param>
        /// <param name="edgeA">значение должно быть неотрицательным</param>
        /// <param name="edgeB">может быть опущен</param>
        public static void Link(IList<int> halfEdges, int edgeA, int edgeB = -1)
        {
            ValidateParam(edgeA, nameof(edgeA));
            halfEdges[edgeA] = edgeB;
            if (edgeB > 0)
                halfEdges[edgeB] = edgeA;
        }

        /// <summary>
        /// Удалить связи между парой полуребер
        /// </summary>
        /// <param name="halfEdges"></param>
        /// <param name="edgeA"></param>
        /// <param name="edgeB"></param>
        public static void UnLink(IList<int> halfEdges, int edgeA, int edgeB)
        {
            if (edgeA != -1)
                halfEdges[edgeA] = -1;
            if (edgeB != -1)
                halfEdges[edgeB] = -1;
        }
        #endregion


        /// <summary>
        /// Удалить связи в полуребрах для треугольник <paramref name="trId"/>
        /// </summary>
        /// <param name="halfEdges"></param>
        /// <param name="trId"></param>
        /// <param name="both">true - связи будут удалены и у смежного с <paramref name="trId"/> треугольника.
        /// Может привести к неожиданным результатам!</param>
        public static void UnLinkTriangle(IList<int> halfEdges, int trId, bool both = false)
        {
            ValidateParam(trId, nameof(trId));

            //проходим по его полуребрам
            for (int he = trId * 3; he < trId * 3 + 3; he++)
            {
                int secondHe = -1;
                //пара к этому полуребру
                if (both)
                    secondHe = Twin(halfEdges, he); ;
                UnLink(halfEdges, he, secondHe);
            }
        }


        //ПОРЯДОК против ч.с. ВАЖЕН!
        /// <summary>
        /// Получить все ребра, окружающие вершину, на которую указывает <paramref name="he"/>.
        /// </summary>
        /// <param name="halfEdges">полуребра</param>
        /// <param name="triangles">треугольники, связанные с <paramref name="halfEdges"/></param>
        /// <param name="he">
        /// Полуребро, указывающее на вершину, вокруг которой требуется найти смежные ребра
        /// </param>
        /// <param name="include">
        /// true - учитывать при обходе против ч.с. полуребро,
        /// не являющееся смежным с общей вершиной,
        /// но при этом это ребро указывает на вершину, смежную с общей вершиной. <br/>
        /// Поэтому последнее <u>полуребро</u> не будет смежным с общей вершиной
        /// </param>
        /// <returns>полуребра, смежные с общей вершиной.
        /// Порядок - против ч.с.
        /// </returns>
        /// <remarks>
        /// Параметр <paramref name="include"/> уместен, если общая вершина является граничной.
        /// Тогда последнее полуребро не будет смежным с полуребром, указывающим на общую вершину.
        /// </remarks>
        public static int[] AdjacentEdgesVertex(IList<int> halfEdges, IList<Troika> triangles, int he, bool include = false)
        {
            int vid = triangles[he / 3][he % 3];
            int twinHe = Twin(halfEdges, he);
            //если нет смежного полуребра, то ищем другое полуребро, которое связано с такой же вершиной
            if (halfEdges[he] == -1)
            {
                //id треугольника и вершины в нем, переданные в качестве аргумента из edgeId
                int currentTrid = he / 3;
                int currentVertexId = he % 3;
                for (int i = 0; i < triangles.Count; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        //если глобальный id вершины совпал и есть смежное полуребро
                        if (triangles[i][j] == Origin(triangles, he) &&
                            halfEdges[i * 3 + j] != -1)
                            //то используем его как исходное полуребро
                            he = i * 3 + j;
                    }
                }
            }
            List<int> segmentHalfEdges = new List<int>();

            //повторное читаем смежное ребро, т.к. исходное могло измениться
            twinHe = Twin(halfEdges, he);

            // если все также нет смежного полуребра, то вершина является граничной
            // и находится в углу, т.е. входит всего в 1 треугольник =>
            // смежные с ней вершины содержатся в одном треугольнике
            if (twinHe == -1)
            {
                int nextHe = Next(he);
                segmentHalfEdges.Add(nextHe);
                nextHe = Next(nextHe);
                segmentHalfEdges.Add(nextHe);
                return segmentHalfEdges.ToArray();
            }

            //true - сегмент замкнут, т.е. треугольники окружают точку вкруг
            bool isSegmentClosed = false;

            int incoming, outgoing;

            //обход против ч.с.
            for (incoming = twinHe; ;)
            {
                int startIncoming = incoming;
                //помещаем текущее ребро в список ребер
                segmentHalfEdges.Add(incoming);

                //одно ребро
                outgoing = Next(incoming); //указывает на общую вершину
                incoming = Twin(halfEdges, outgoing); //указывает на смежную с ней
#if DEBUG
                //outgoing должна указывать на vid
                if (Origin(triangles, outgoing) != vid)
                {
                    string log = $"общая вершина ({nameof(outgoing)}) != {vid} " +
                        $"({triangles[startIncoming / 3][startIncoming % 3]}," +
                        $"{triangles[outgoing / 3][outgoing % 3]}," +
                        $"{triangles[incoming / 3][incoming % 3]})";
                    Log.Error(log);
                }
#endif
                //достигли вершины, с которой начался цикл
                //поэтому сегмент замкнут
                if (incoming == twinHe)
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
                        int nextBoundaryVid = Next(outgoing);
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
            for (outgoing = he; ;)
            {
                incoming = Prev(outgoing);
                segmentHalfEdges.Add(incoming);
                outgoing = Twin(halfEdges, incoming); //указывает на vid

                if (outgoing == -1)
                    break;

                if (outgoing == he)
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


        #region Валидация, вспомогательные функции
        /// <summary>
        /// Валидация полуребра/треугольника
        /// или другого параметра, который должен быть неотрицательным
        /// </summary>
        /// <param name="param"></param>
        private static void ValidateParam(int param, string paramName)
        {
            if (param < 0)
                throw new ArgumentException($"Аргумент не может быть меньше нуля! {param})", paramName);
        }

        /// <summary>
        /// Сведения о треугольнике: id, вершины, полуребра
        /// </summary>
        /// <param name="faces"></param>
        /// <param name="trid"></param>
        /// <returns></returns>
        public static string TriangleInfo(IList<Troika> faces, int trid)
        {
            string log = $"trid:{trid};vid:{faces[trid].Get()};he:({trid * 3},{trid * 3 + 1},{trid * 3 + 2})";
            return log;
        }


        /// <summary>
        /// Сведения о полуребре: id, twin, треугольник, вершины, другие полуребра
        /// </summary>
        /// <param name="faces"></param>
        /// <param name="he"></param>
        /// <returns></returns>
        public static string HeInfo(IList<Troika> faces, IList<int> halfEdges, int he)
        {
            int trid = he / 3;
            string log = $"he:{he};twin:{Twin(halfEdges, he)};trid:{trid} " +
                $"{faces[trid].Get()} " +
                $"he:{(trid * 3, trid * 3 + 1, trid * 3 + 2)}";
            return log;
        }
        #endregion
    }
}
