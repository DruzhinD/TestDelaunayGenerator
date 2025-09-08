using CommonLib.Geometry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.Smoothing
{
    /// <summary>
    /// Содержит метод деления полуребер
    /// </summary>
    public class EdgeSplitter
    {
        IList<IHPoint> points;
        IList<int> halfEdges;
        IList<PointStatus> pointStatuses;
        IList<Troika> faces;
        IList<EdgePair> boundaryEdges;

        public EdgeSplitter(IList<IHPoint> points, IList<int> halfEdges, IList<PointStatus> pointStatuses, IList<Troika> faces, IList<EdgePair> boundaryEdges)
        {
            this.points = points;
            this.halfEdges = halfEdges;
            this.pointStatuses = pointStatuses;
            this.faces = faces;
            this.boundaryEdges = boundaryEdges;
        }

        /// <summary>
        /// Разделить ребро на 2 части.
        /// Таким образом треугольник с полуребром <paramref name="H0"/>,
        /// а также смежный с ним (для <paramref name="H0"/> существует twin)
        /// будет разделен надвое при помощи <paramref name="vertex"/>
        /// </summary>
        /// <param name="H0">Полуребро, принадлежащее ребру, которое будет поделено надвое</param>
        /// <param name="vertex">координаты точки деления ребра</param>
        /// <returns>idx первого нового треугольника</returns>
        public int SplitEdge(int H0, IHPoint vertex, bool boundaryVertex = false)
        {
            //полуребро нового треугольника,
            //которое также может быть разбито
            int newTrHe = -1;

            //вставляем новую вершину
            points.Add(vertex);
            pointStatuses.Add(PointStatus.Internal);
            boundaryEdges.Add(new EdgePair());

            //id новой вершины
            int vn = points.Count - 1;
#if DEBUG
            Log.Debug($"Новая вершина vid: {vn} {(vertex.X, vertex.Y)}");
#endif

            //добавляем пару новых треугольников (внутри исходного)
            (int startTr0, int startTr1) = AddTrianglePair(H0, vn);
            newTrHe = startTr1;
#if DEBUG
            Log.Debug($"Новый треугольник: {HalfEdgeUtils.TriangleInfo(faces, startTr0 / 3)}");
            Log.Debug($"Новый треугольник: {HalfEdgeUtils.TriangleInfo(faces, startTr1 / 3)}");
#endif
            int twinH0 = HalfEdgeUtils.Twin(halfEdges, H0);
            //если есть парное ребро у H0
            if (twinH0 != -1)
            {
                //добавляем пару новых треугольников (внутри смежного исходному)
                (int startTr2, int startTr3) = AddTrianglePair(twinH0, vn);
#if DEBUG
                Log.Debug($"Новый треугольник: {HalfEdgeUtils.TriangleInfo(faces, startTr2 / 3)}");
                Log.Debug($"Новый треугольник: {HalfEdgeUtils.TriangleInfo(faces, startTr3 / 3)}");
#endif
                //связываем t1 и t2
                HalfEdgeUtils.Link(this.halfEdges, startTr1, startTr2);
                //связываем t0 и t3
                HalfEdgeUtils.Link(this.halfEdges, startTr0, startTr3);
            }
            //иначе ребро граничное => новая точка тоже граничная
            else
            {
                //TODO использовать LinkBound
                int adj1 = HalfEdgeUtils.Origin(faces, HalfEdgeUtils.Next(H0));
                int adj2 = HalfEdgeUtils.Origin(faces, HalfEdgeUtils.Prev(H0));
                //обновление массивов границ
                pointStatuses[vn] = PointStatus.Boundary;
                boundaryEdges[vn] = new EdgePair()
                {
                    vid = vn,
                    adjacent1 = adj1, //v0
                    adjacent2 = adj2 //v1
                };

                //изменяем соседей у исходных граничных вершин
                var edgeAdj1 = boundaryEdges[adj1];
                if (edgeAdj1.adjacent1 == adj2)
                    edgeAdj1.adjacent1 = vn;
                else
                    edgeAdj1.adjacent2 = vn;
                boundaryEdges[adj1] = edgeAdj1;

                var edgeAdj2 = boundaryEdges[adj2];
                if (edgeAdj2.adjacent1 == adj1)
                    edgeAdj2.adjacent1 = vn;
                else
                    edgeAdj2.adjacent2 = vn;
                boundaryEdges[adj2] = edgeAdj2;
            }
            HalfEdgeUtils.UnLinkTriangle(halfEdges, H0 / 3);

            var tr = faces[H0 / 3];
            tr.flag = TriangleState.Deleted;
            faces[H0 / 3] = tr;

            if (twinH0 != -1)
            {
                HalfEdgeUtils.UnLinkTriangle(halfEdges, twinH0 / 3);
                tr = faces[twinH0 / 3];
                tr.flag = TriangleState.Deleted;
                faces[twinH0 / 3] = tr;
            }
            return newTrHe;
        }



        /// <summary>
        /// Добавить пару новых треугольников внутри треугольника,
        /// содержащего полуребро <paramref name="H0"/>.
        /// ребро <paramref name="H0"/> разделено вершиной с idx=<paramref name="vidNew"/>
        /// </summary>
        /// <param name="H0"></param>
        /// <param name="vidNew"></param>
        /// <returns>индексы новых треугольников. Обход - против ч.с.</returns>
        protected (int, int) AddTrianglePair(int H0, int vidNew)
        {
            //инициализация указателей на начало троек новых полуребер/треугольников и 
            int startTr0 = halfEdges.Count;
            for (int i = 0; i < 3; i++)
                halfEdges.Add(-1);
            //инициализация треугольников на вершинах
            Troika tr0 = new Troika();
            tr0.flag = faces[H0 / 3].flag;
            tr0[0] = HalfEdgeUtils.Origin(this.faces, H0);
            tr0[1] = vidNew;
            tr0[2] = HalfEdgeUtils.Origin(this.faces, HalfEdgeUtils.Prev(H0));
            this.faces.Add(tr0);

            int startTr1 = halfEdges.Count;
            for (int i = 0; i < 3; i++)
                halfEdges.Add(-1);
            Troika tr1 = new Troika();
            tr1.flag = faces[H0 / 3].flag;
            tr1[0] = vidNew;
            tr1[1] = HalfEdgeUtils.Origin(this.faces, HalfEdgeUtils.Next(H0));
            tr1[2] = HalfEdgeUtils.Origin(this.faces, HalfEdgeUtils.Prev(H0));
            this.faces.Add(tr1);

            //связать пары полуребер новых треугольников внутри исходного
            HalfEdgeUtils.Link(
                this.halfEdges, HalfEdgeUtils.Next(startTr0), HalfEdgeUtils.Prev(startTr1));

            //связать пары полуребер из исходного треугольника
            HalfEdgeUtils.Link(this.halfEdges, HalfEdgeUtils.Prev(startTr0), halfEdges[HalfEdgeUtils.Prev(H0)]);
            HalfEdgeUtils.Link(this.halfEdges, HalfEdgeUtils.Next(startTr1), halfEdges[HalfEdgeUtils.Next(H0)]);

            //помечаем исходный треугольник как внешний
            var exTr = faces[H0 / 3];
            exTr.flag = TriangleState.External;
            faces[H0 / 3] = exTr;

            return (startTr0, startTr1);
        }
    }
}
