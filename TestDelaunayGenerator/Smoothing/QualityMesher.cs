using CommonLib.Geometry;
using MemLogLib;
using MeshLib.Wrappers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.DCELMesh;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.Smoothing
{
    /// <summary>
    /// Усовершенствование треугольной сетки, засчет новых точек
    /// </summary>
    public class QualityMesher
    {
        public QualityMesherConfig Config { get; set; } = new QualityMesherConfig();

        public QualityMesher() { }
        public QualityMesher(QualityMesherConfig config)
        {
            this.Config = config;
        }


        /// <summary>
        /// Статусы полуребер относительно соответствия условиям улучшения сетки в <see cref="Config"/>
        /// </summary>
        protected enum EdgeStatus
        {
            /// <summary>
            /// полуребро не проверено
            /// </summary>
            None = 0,
            /// <summary>
            /// Полуребро соответствует условиям, поэтому не подлежит делению
            /// </summary>
            Ok = 1,
            /// <summary>
            /// Полуребро пропущено, т.к. смежное с ним уже имеет статус,
            /// отличный от <see cref="None"/>
            /// </summary>
            Skipped = 2,
            /// <summary>
            /// Ребро будет разделено
            /// </summary>
            Split = 3,
            /// <summary>
            /// полуребро не является граничным.
            /// Устанавливается, если есть <see cref="QualityMesherConfig.RebuildOnlyBoundary"/>
            /// </summary>
            NotBoundary = 4,
            /// <summary>
            /// Полуребро заблокировано.
            /// Назначается если в текущем треугольнике есть полуребро для разбиения
            /// или в смежном треугольнике есть полуребро для разбиение
            /// </summary>
            Blocked = 5

        }


        #region Поля, необходимые для добавления новых треугольников и вершин
        //во время поиска треугольников для деления
        //ссылаются на массивы из сетки
        //после - являются типом List<T> для расширения коллекции
        IList<IHPoint> points;
        IList<int> halfEdges;
        IList<PointStatus> pointStatuses;
        IList<Troika> faces;
        IList<EdgeIndex> boundaryEdges;
        #endregion

        //TODO выполнять перестроение здесь же
        public IRestrictedDCEL Refine(IRestrictedDCEL mesh)
        {
#if DEBUG
            Log.Information($"Вызов {nameof(QualityMesher)}.{nameof(Refine)} " +
                $"MinAngle:{Config.MinAngle}, " +
                $"MaxAngle:{Config.MaxAngle}, " +
                $"1 треугольник на {Config.SplitTriangleParts} части ");
#endif
            //копируем ссылки на массивы
            points = mesh.Points;
            halfEdges = mesh.HalfEdges;
            pointStatuses = mesh.PointStatuses;
            faces = mesh.Faces;
            boundaryEdges = mesh.BoundaryEdges;

            //индексация совпадает с halfEdges
            EdgeStatus[] halfEdgeStatus = null;
            MEM.Alloc(halfEdges.Count, ref halfEdgeStatus, EdgeStatus.None);

            List<int> halfEdgeSplit = new List<int>((int)Math.Sqrt(halfEdges.Count));

            //проход по всем полуребрам
            for (int he = 0; he < halfEdges.Count; he++)
            {
                if (halfEdgeStatus[he] != EdgeStatus.None)
                    continue;

                //пропуск ребра, входящего во внешний треугольник
                if (faces[he / 3].flag == (int)TriangleInfect.External)
                {
                    halfEdgeStatus[he] = EdgeStatus.Skipped;
#if DEBUG
                    Log.Debug($"{HalfEdgesUtils.HeInfo(faces, halfEdges, he)}. trid внешний.");
#endif
                    continue;
                }

                int twinHe = HalfEdgesUtils.Twin(halfEdges, he);
                //пропуск неграничных если установлено такое условие
                if (Config.RebuildOnlyBoundary &&
                    twinHe != -1)
                {
                    halfEdgeStatus[he] = EdgeStatus.NotBoundary;
#if DEBUG
                    Log.Debug($"{HalfEdgesUtils.HeInfo(faces, halfEdges, he)}. Status:{halfEdgeStatus[he]}. SKIP");
#endif
                    continue;
                }

                //для неграничных полуребер. Если смежное полуребро уже проверено
                if (twinHe != -1 && halfEdgeStatus[twinHe] != EdgeStatus.None)
                {
                    halfEdgeStatus[he] = EdgeStatus.Skipped;
#if DEBUG
                    Log.Debug($"{HalfEdgesUtils.HeInfo(faces, halfEdges, he)}. Status:{halfEdgeStatus[he]}. SKIP");
#endif
                    continue;
                }

                //пропуск ребра, если в треугольнике, которому принадлежит смежное полуребро,
                //уже есть полуребро для деления
                if (twinHe != -1)
                {
                    if (halfEdgeStatus[HalfEdgesUtils.Next(twinHe)] == EdgeStatus.Split ||
                        halfEdgeStatus[HalfEdgesUtils.Prev(twinHe)] == EdgeStatus.Split)
                    {
                        halfEdgeStatus[he] = EdgeStatus.Skipped;
#if DEBUG
                        Log.Debug($"{HalfEdgesUtils.HeInfo(faces, halfEdges, he)}. Status:{halfEdgeStatus[he]}. SKIP");
#endif
                        continue;
                    }
                }

                int vid = HalfEdgesUtils.Origin(faces, he);
                int nextVid = HalfEdgesUtils.Origin(faces, HalfEdgesUtils.Next(he));
                int prevVid = HalfEdgesUtils.Origin(faces, HalfEdgesUtils.Prev(he));

                //угол напротив полуребра he
                double angle = CalcAngle(points[vid], points[prevVid], points[nextVid]);
                //TODO добавить проверку на минимальный угол
                if (angle > Config.MaxAngle)
                {
                    halfEdgeStatus[he] = EdgeStatus.Split;
                    halfEdgeSplit.Add(he);
#if DEBUG
                    Log.Debug(
                    $"{HalfEdgesUtils.HeInfo(faces, halfEdges, he)}. Status:{halfEdgeStatus[he]}. " +
                    $"angle{(vid, nextVid, prevVid)}={ToDegrees(angle)}"
                    );
#endif

                    //помечаем все ребра в этом треугольнике пропущенными,
                    //т.к. можно делить всего 1 ребро в треугольнике
                    //смежный треугольник тоже блокируем
                    int twinNextHe, twinPrevHe;
                    if (twinHe != -1)
                    {
                        halfEdgeStatus[twinHe] = EdgeStatus.Skipped;
                        twinNextHe = HalfEdgesUtils.Next(twinHe);
                        halfEdgeStatus[twinNextHe] = EdgeStatus.Blocked;
                        int twintwinNextHe = HalfEdgesUtils.Twin(halfEdges, twinNextHe);
                        if (twintwinNextHe != -1)
                            halfEdgeStatus[twintwinNextHe] = EdgeStatus.Blocked;

                        twinPrevHe = HalfEdgesUtils.Prev(twinHe);
                        halfEdgeStatus[twinPrevHe] = EdgeStatus.Blocked;
                        int twintwinPrevHe = HalfEdgesUtils.Twin(halfEdges, twinPrevHe);
                        if (twintwinPrevHe != -1)
                            halfEdgeStatus[twintwinPrevHe] = EdgeStatus.Blocked;
                    }
                    int nextHe = HalfEdgesUtils.Next(he);
                    halfEdgeStatus[nextHe] = EdgeStatus.Blocked;
                    twinNextHe = HalfEdgesUtils.Twin(halfEdges, nextHe);
                    if (twinNextHe != -1)
                        halfEdgeStatus[twinNextHe] = EdgeStatus.Blocked;
                    int prevHe = HalfEdgesUtils.Prev(he);
                    halfEdgeStatus[prevHe] = EdgeStatus.Blocked;
                    twinPrevHe = HalfEdgesUtils.Twin(halfEdges, prevHe);
                    if (twinPrevHe != -1)
                        halfEdgeStatus[twinPrevHe] = EdgeStatus.Blocked;

                }
                else
                {
                    halfEdgeStatus[he] = EdgeStatus.Ok;
                    if (twinHe != -1)
                        halfEdgeStatus[twinHe] = EdgeStatus.Skipped;
#if DEBUG
                    Log.Debug($"{HalfEdgesUtils.HeInfo(faces, halfEdges, he)}. Status:{halfEdgeStatus[he]}. " +
                        $"angle{(vid, nextVid, prevVid)}={ToDegrees(angle)}");
#endif
                }
            }
            return SplitTriangles(halfEdgeSplit, mesh);
        }


        /// <summary>
        /// Разделить ребро на 2 части.
        /// Таким образом треугольник с полуребром <paramref name="H0"/>,
        /// а также смежный с ним (для <paramref name="H0"/> существует twin)
        /// будет разделен надвое при помощи <paramref name="vertex"/>
        /// </summary>
        /// <param name="H0">Полуребро, принадлежащее ребру, которое будет поделено надвое</param>
        /// <param name="vertex">координаты точки деления ребра</param>
        /// <returns>Количество новых треугольников</returns>
        protected int SplitEdge(int H0, IHPoint vertex)
        {
            //полуребро нового треугольника,
            //которое также может быть разбито
            int newTrHe = -1;

            //вставляем новую вершину
            points.Add(vertex);
            pointStatuses.Add(PointStatus.Internal);
            boundaryEdges.Add(new EdgeIndex());

            //id новой вершины
            int vn = points.Count - 1;
#if DEBUG
            Log.Debug($"Новая вершина vid: {vn} {(vertex.X, vertex.Y)}");
#endif

            //добавляем пару новых треугольников (внутри исходного)
            (int startTr0, int startTr1) = AddTrianglePair(H0, vn);
            newTrHe = startTr1;
#if DEBUG
            Log.Debug($"Новый треугольник: {HalfEdgesUtils.TriangleInfo(faces, startTr0 / 3)}");
            Log.Debug($"Новый треугольник: {HalfEdgesUtils.TriangleInfo(faces, startTr1 / 3)}");
#endif
            int twinH0 = HalfEdgesUtils.Twin(halfEdges, H0);
            //если есть парное ребро у H0
            if (twinH0 != -1)
            {
                //добавляем пару новых треугольников (внутри смежного исходному)
                (int startTr2, int startTr3) = AddTrianglePair(twinH0, vn);
#if DEBUG
                Log.Debug($"Новый треугольник: {HalfEdgesUtils.TriangleInfo(faces, startTr2 / 3)}");
                Log.Debug($"Новый треугольник: {HalfEdgesUtils.TriangleInfo(faces, startTr3 / 3)}");
#endif
                //связываем t1 и t2
                HalfEdgesUtils.Link(this.halfEdges, startTr1, startTr2);
                //связываем t0 и t3
                HalfEdgesUtils.Link(this.halfEdges, startTr0, startTr3);
            }
            //иначе ребро граничное => новая точка тоже граничная
            else
            {

                int adj1 = HalfEdgesUtils.Origin(faces, HalfEdgesUtils.Next(H0));
                int adj2 = HalfEdgesUtils.Origin(faces, HalfEdgesUtils.Prev(H0));
                //обновление массивов границ
                pointStatuses[vn] = PointStatus.Boundary;
                boundaryEdges[vn] = new EdgeIndex()
                {
                    PointID = vn,
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
            HalfEdgesUtils.UnLinkTriangle(halfEdges, H0 / 3);
            if (twinH0 != -1)
                HalfEdgesUtils.UnLinkTriangle(halfEdges, twinH0 / 3);
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
            tr0.flag = (int)TriangleInfect.Internal;
            tr0[0] = HalfEdgesUtils.Origin(this.faces, H0);
            tr0[1] = vidNew;
            tr0[2] = HalfEdgesUtils.Origin(this.faces, HalfEdgesUtils.Prev(H0));
            this.faces.Add(tr0);

            int startTr1 = halfEdges.Count;
            for (int i = 0; i < 3; i++)
                halfEdges.Add(-1);
            Troika tr1 = new Troika();
            tr1.flag = (int)TriangleInfect.Internal;
            tr1[0] = vidNew;
            tr1[1] = HalfEdgesUtils.Origin(this.faces, HalfEdgesUtils.Next(H0));
            tr1[2] = HalfEdgesUtils.Origin(this.faces, HalfEdgesUtils.Prev(H0));
            this.faces.Add(tr1);

            //связать пары полуребер новых треугольников внутри исходного
            HalfEdgesUtils.Link(
                this.halfEdges, HalfEdgesUtils.Next(startTr0), HalfEdgesUtils.Prev(startTr1));

            //связать пары полуребер из исходного треугольника
            HalfEdgesUtils.Link(this.halfEdges, HalfEdgesUtils.Prev(startTr0), halfEdges[HalfEdgesUtils.Prev(H0)]);
            HalfEdgesUtils.Link(this.halfEdges, HalfEdgesUtils.Next(startTr1), halfEdges[HalfEdgesUtils.Next(H0)]);

            //помечаем исходный треугольник как внешний
            var exTr = faces[H0 / 3];
            exTr.flag = (int)TriangleInfect.External;
            faces[H0 / 3] = exTr;

            return (startTr0, startTr1);
        }


        /// <summary>
        /// Разбиение помеченных треугольников
        /// </summary>
        protected IRestrictedDCEL SplitTriangles(IList<int> halfEdgeRebuild, IRestrictedDCEL mesh)
        {
            IRestrictedDCEL refinedMesh = null;

            //если нет плохих треугольников, то возвращаем исходную сетку
            if (halfEdgeRebuild.Count == 0)
            {
                refinedMesh = mesh;
                return refinedMesh;
            }

            //инициализация списков
            points = new List<IHPoint>(mesh.Points);
            halfEdges = new List<int>(mesh.HalfEdges);
            pointStatuses = new List<PointStatus>(mesh.PointStatuses);
            faces = new List<Troika>(mesh.Faces);
            boundaryEdges = new List<EdgeIndex>(mesh.BoundaryEdges);

            foreach (int H0 in halfEdgeRebuild)
            {
#if DEBUG
                Log.Debug($"Деление {HalfEdgesUtils.TriangleInfo(faces, H0 / 3)}(he:{H0}) на {Config.SplitTriangleParts} частей.");
#endif
                int trIdOld = H0 / 3;

                //рассчитать координаты для новых точек
                int vid1 = faces[trIdOld][HalfEdgesUtils.Next(H0) % 3];
                int vid2 = faces[trIdOld][H0 % 3];
                IList<IHPoint> newPoints = SplitLine(points[vid1], points[vid2]);

                int curH0 = H0;
                //TODO H0 нужно переназначать
                foreach (var p in newPoints)
                {
                    curH0 = SplitEdge(curH0, p);
                }

            }
            refinedMesh = new RestrictedDCEL(
                points.ToArray(),
                halfEdges.ToArray(),
                pointStatuses.ToArray(),
                faces.ToArray(),
                boundaryEdges.ToArray());

            return refinedMesh;
        }


        protected int AddTriangle(int v0, int v1, int v2, int he0, int he1, int he2)
        {
            //формируем треугольник
            var tr = new Troika();
            tr.i = v0;
            tr.j = v1;
            tr.k = v1;
            tr.flag = (int)TriangleInfect.Internal;

            //вставляем
            faces.Add(tr);
            //заполняем полуребра
            for (int i = 0; i < 3; i++)
                halfEdges.Add(-1);
            int idx = faces.Count - 1;
            HalfEdgesUtils.Link(halfEdges, idx, he0);
            HalfEdgesUtils.Link(halfEdges, idx + 1, he1);
            HalfEdgesUtils.Link(halfEdges, idx + 2, he2);
            return idx;

        }

        //TODO переименовать
        /// <summary>
        /// Разделить отрезок на несколько равных частей
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns>новые точки</returns>
        public IList<IHPoint> SplitLine(IHPoint start, IHPoint end)
        {
            //расстояние между точками покоординатно
            double lenX = start.X - end.X;
            double lenY = start.Y - end.Y;

            //(де-)инкремент покоординатно
            double incrementX = lenX / Config.SplitTriangleParts;
            double incrementY = lenY / Config.SplitTriangleParts;

            var newPoints = new List<IHPoint>(Config.SplitTriangleParts - 1);

            for (int i = 1; i < Config.SplitTriangleParts; i++)
            {
                double newX = end.X + incrementX * i;
                double newY = end.Y + incrementY * i;
                newPoints.Add(new HPoint(newX, newY));
            }
            return newPoints;
        }


        /// <summary>
        /// Рассчет угла в радианах через arccos
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <param name="C"></param>
        /// <returns></returns>
        public static double CalcAngle(IHPoint A, IHPoint B, IHPoint C)
        {
            // Вектора BA и BC
            var ba = new HPoint(A.X - B.X, A.Y - B.Y);
            var bc = new HPoint(C.X - B.X, C.Y - B.Y);

            // Скалярное произведение
            double dotProduct = ba.X * bc.X + ba.Y * bc.Y;

            // Длины векторов
            double lengthBA = Math.Sqrt(ba.X * ba.X + ba.Y * ba.Y);
            double lengthBC = Math.Sqrt(bc.X * bc.X + bc.Y * bc.Y);

            // Косинус угла (с защитой от деления на ноль)
            double cosTheta = dotProduct / (lengthBA * lengthBC);
            //cosTheta = Math.Clamp(cosTheta, -1.0, 1.0); // Исключает NaN из-за ошибок округления

            // Угол в радианах (0 < θ < π)
            return Math.Acos(cosTheta);
        }

        static double ToDegrees(double rad) => 180 / Math.PI * rad;
    }
}
