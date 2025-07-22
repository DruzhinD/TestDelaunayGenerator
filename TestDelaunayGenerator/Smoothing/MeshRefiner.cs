using CommonLib.Geometry;
using MemLogLib;
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
    public class MeshRefiner
    {
        public RefineConfig Config { get; set; } = new RefineConfig();

        public MeshRefiner() { }
        public MeshRefiner(RefineConfig config)
        {
            this.Config = config;
        }


        protected enum RefineEnum
        {
            /// <summary>
            /// треугольник пока не проверен
            /// </summary>
            None = 0,
            /// <summary>
            /// проверен
            /// </summary>
            Checked = 1,
            /// <summary>
            /// Пропущен, т.к. смежный треугольник уже имеет статус <see cref="Split"/>
            /// </summary>
            Skipped = 2,
            /// <summary>
            /// будет разделен
            /// </summary>
            Split = 3,

        }


        #region Поля, необходимые для добавления новых треугольников и вершин
        // инициализация после оценки количества потенциальных новых треугольников и точек
        List<IHPoint> points;
        List<int> halfEdges;
        List<PointStatus> pointStatuses;
        List<Troika> faces;
        List<EdgeIndex> boundaryEdges;
        #endregion

        //TODO сделать segments опциональным
        public IRestrictedDCEL Refine(IRestrictedDCEL mesh, Segment[] segments)
        {
            List<int> halfEdgeRebuild = new List<int>();
            //сведения о проверке треугольников
            RefineEnum[] triangleRebuilds = null;
            MEM.Alloc(mesh.Faces.Length, ref triangleRebuilds, RefineEnum.None);

            //проход по вершинам
            for (int vid = 0; vid < mesh.Points.Length; vid++)
            {
                //вершина неграничная
                if (mesh.PointStatuses[vid] != PointStatus.Boundary)
                    continue;

                //проход по полуребрам, указывающим на смежную с vid вершину
                //последний треугольник мб продублирован
                List<int> vidHalfEdges = segments[vid].halfEdgeIds.ToList();
                //выбрасываем последнее полуребро, т.к. оно дублирует крайний треугольник !!!только для граничных точек
                vidHalfEdges.RemoveAt(vidHalfEdges.Count - 1);
                foreach (int he in vidHalfEdges)
                {
                    int trId = he / 3;
                    //проверенные треугольники пропускаем
                    if (triangleRebuilds[trId] != RefineEnum.None)
                        continue;

                    //если смежный треугольник будет разделен, то пропускаем текущий
                    int prevHe = HalfEdgesUtils.Prev(he);
                    int adjHe = mesh.HalfEdges[prevHe];
                    if (adjHe != -1 && triangleRebuilds[adjHe / 3] == RefineEnum.Split)
                    {
                        triangleRebuilds[trId] = RefineEnum.Skipped;
                        continue;
                    }

                    int nextHe = HalfEdgesUtils.Next(he);
                    //надо получить вершину, смежную с vid
                    int adjVid = mesh.Faces[trId][prevHe % 3];
                    int AdjVidHe = prevHe;
                    int VidHe = nextHe;
                    //проверка не получили ли vid
                    if (adjVid == vid)
                    {
                        adjVid = mesh.Faces[trId][nextHe % 3];
                        AdjVidHe = nextHe;
                        VidHe = prevHe;
                    }
                    //если ребро, лежащее напротив he - неграничное, то пропускаем
                    if (mesh.PointStatuses[vid] != PointStatus.Boundary ||
                        mesh.PointStatuses[adjVid] != PointStatus.Boundary ||
                        !mesh.BoundaryEdges[vid].Adjacents.Contains(adjVid)
                        )
                    {
                        triangleRebuilds[trId] = RefineEnum.Checked;
                        continue;
                    }

                    int nonBoundVid = mesh.Faces[trId][he % 3];
                    //если недопустимый угол, то помечаем для разделения
                    double angle = CalcAngle(mesh.Points[vid], mesh.Points[nonBoundVid], mesh.Points[adjVid]);
                    if (angle < Config.MinAngle || Config.MaxAngle < angle)
                    {
                        triangleRebuilds[trId] = RefineEnum.Split;
                        //halfEdgeRebuild.Add(VidHe);
                        halfEdgeRebuild.Add(HalfEdgesUtils.Next(he));
                        continue;
                    }

                }
            }
            return SplitTriangles(halfEdgeRebuild, mesh);

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
            //счетчик новых треугольников
            int newTrCnt = 0;

            //вставляем новую вершину
            points.Add(vertex);
            pointStatuses.Add(PointStatus.Internal);
            boundaryEdges.Add(new EdgeIndex());

            //id новой вершины
            int vn = points.Count - 1;

            //добавляем пару новых треугольников (внутри исходного)
            (int startTr0, int startTr1) = AddTrianglePair(H0, vn);
            newTrCnt += 2;

            //если есть парное ребро у H0
            if (HalfEdgesUtils.IsBoundary(halfEdges, H0) is false)
            {
                int twinH0 = HalfEdgesUtils.Twin(halfEdges, H0);
                //добавляем пару новых треугольников (внутри смежного исходному)
                (int startTr2, int startTr3) = AddTrianglePair(twinH0, vn);
                newTrCnt += 2;

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
                return newTrCnt;
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
            //выделение памяти
            int startTr0 = halfEdges.Count;
            halfEdges.AddRange(new int[] { -1, -1, -1 });

            int startTr1 = halfEdges.Count;
            halfEdges.AddRange(new int[] { -1, -1, -1 });

            //инициализация треугольников на вершинах
            Troika tr0 = new Troika();
            tr0.flag = (int)TriangleInfect.Internal;
            tr0[0] = HalfEdgesUtils.Origin(this.faces, H0);
            tr0[1] = vidNew;
            tr0[2] = HalfEdgesUtils.Origin(this.faces, HalfEdgesUtils.Prev(H0));
            this.faces.Add(tr0);

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

            return (startTr0, startTr1);
        }


        /// <summary>
        /// Разбиение помеченных треугольников
        /// </summary>
        protected IRestrictedDCEL SplitTriangles(List<int> halfEdgeRebuild, IRestrictedDCEL mesh)
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
                int trIdOld = H0 / 3;

                //рассчитать координаты для новых точек
                int vid1 = faces[trIdOld][HalfEdgesUtils.Next(H0) % 3];
                int vid2 = faces[trIdOld][H0 % 3];
                IList<IHPoint> newPoints = SplitLine(points[vid1], points[vid2]);

                //TODO H0 нужно переназначать
                foreach (var p in newPoints)
                {
                    SplitEdge(H0, p);
                    //HalfEdgesUtils.UnLinkTriangle(halfEdges, trIdOld);
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
    }
}
