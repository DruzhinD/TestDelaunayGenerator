using CommonLib.Geometry;
using MemLogLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.DelaunatorMesh;
using TestDelaunayGenerator.DelaunatorModels;

namespace TestDelaunayGenerator.Smoothing
{
    public class MeshRefiner
    {
        public MeshRefinerConfig Config;
        IRestrictedDCEL mesh;

        public MeshRefiner(MeshRefinerConfig config = null)
        {
            this.Config = config;
            if (this.Config == null)
                this.Config = new MeshRefinerConfig();
        }

        protected enum HeStatus
        {
            /// <summary>
            /// полуребро не обработано
            /// </summary>
            None = 0,
            /// <summary>
            /// п/р норм
            /// </summary>
            Normal = 1,
            /// <summary>
            /// П/р удалено
            /// </summary>
            Removed = 2,
            /// <summary>
            /// Пропущенное п/р
            /// </summary>
            Skipped = 3,
            Split = 4
        }

        public IRestrictedDCEL Refine(IRestrictedDCEL mesh)
        {
            //валидация
            if (mesh is null)
                throw new ArgumentNullException($"{nameof(mesh)} не должна быть null!");
            this.mesh = mesh;

            var refinedMesh = SplitTriangles();
            return refinedMesh.ToDcelTriMesh();
        }

        protected IRestrictedDCEL SplitTriangles()
        {
            HeStatus[] heStatuses = null;
            MEM.Alloc(mesh.HalfEdges.Length, ref heStatuses, HeStatus.None);

            //для деления пополам
            List<int> heToSplit = new List<int>((int)Math.Sqrt(mesh.HalfEdges.Length));

            //определяем п/р для деления
            for (int he = 0; he < mesh.HalfEdges.Length; he++)
            {
                int vid = HalfEdgeUtils.Origin(mesh.Faces, he);
                int trid = he / 3;
                int twinHe = HalfEdgeUtils.Twin(mesh.HalfEdges, he);

                //пропуск п/р
                if (heStatuses[he] != HeStatus.None)
                    continue;
                if (mesh.Faces[he / 3].flag != TriangleState.Internal)
                {
                    heStatuses[he] = HeStatus.Removed;
                    continue;
                }

                //пропуск неграничных, согласно конфигу
                if (Config.RebuildOnlyBoundary && twinHe != -1)
                {
                    heStatuses[he] = HeStatus.Skipped;
                    continue;
                }

                int nextVid = HalfEdgeUtils.Origin(mesh.Faces, HalfEdgeUtils.Next(he));
                int prevVid = HalfEdgeUtils.Origin(mesh.Faces, HalfEdgeUtils.Prev(he));
                //угол напротив полуребра he
                double angle = CalcAngle(mesh.Points[vid], mesh.Points[prevVid], mesh.Points[nextVid]);
                
                if (angle > Config.MaxAngle)
                {
                    heStatuses[he] = HeStatus.Split;
                    heToSplit.Add(he);

                    //неделимые ребра этого треугольника и смежные с ними
                    int nextHe = HalfEdgeUtils.Next(he);
                    SetSkipped(nextHe);
                    int prevHe = HalfEdgeUtils.Prev(he);
                    SetSkipped(prevHe);

                    //треугольник, смежный по ребру, которое будет разделено
                    if (twinHe != -1)
                    {
                        heStatuses[twinHe] = HeStatus.Skipped;
                        int twinNextHe = HalfEdgeUtils.Next(twinHe);
                        SetSkipped(twinNextHe);
                        int twinPrevHe = HalfEdgeUtils.Prev(twinHe);
                        SetSkipped(twinPrevHe);
                    }

                    //помечает как пропущенное текущее п/р и смежное с ним
                    void SetSkipped(int skipHe)
                    {
                        heStatuses[skipHe] = HeStatus.Skipped;
                        int twinSkipHe = HalfEdgeUtils.Twin(mesh.HalfEdges, skipHe);
                        if (twinSkipHe != -1)
                            heStatuses[twinSkipHe] = HeStatus.Skipped;
                    }
                }
                else
                {
                    heStatuses[he] = HeStatus.Normal;
                    //TODO мб помечать смежное п/р как пропущенное?
                }
            }

            double ToPercent(double s1, double s2)
            {
                return Math.Round((double)heToSplit.Count / mesh.Faces.Length, 3) * 100;
            }

            int newPointCnt = heToSplit.Count * (Config.SplitTriangleParts - 1);
#if DEBUG
            Log.Information($"треугольников для деления:{heToSplit.Count}шт " +
                $"({ToPercent(heToSplit.Count, mesh.Faces.Length)}%)");
            Log.Information($"Новых точек:{newPointCnt}шт " +
                $"({ToPercent(newPointCnt, mesh.Points.Length)}%)");
#endif
            //делим п/р
            if (heToSplit.Count == 0)
            {
                return mesh;
            }

            var pointsNew = new List<IHPoint>(mesh.Points.Length + newPointCnt);
            pointsNew.AddRange(mesh.Points);
            var halfEdgesNew = new List<int>(mesh.HalfEdges.Length + newPointCnt * 3);
            halfEdgesNew.AddRange(mesh.HalfEdges);
            var pointStatusesNew = new List<PointStatus>(mesh.PointStatuses.Length + newPointCnt);
            pointStatusesNew.AddRange(mesh.PointStatuses);
            var facesNew = new List<Triangle>(mesh.Faces.Length + newPointCnt * 4);
            facesNew.AddRange(mesh.Faces);
            var boundaryEdgesNew = new List<ContourPoint>(mesh.BoundaryEdges.Length + newPointCnt);
            boundaryEdgesNew.AddRange(mesh.BoundaryEdges);

            var edgeSplitter = new EdgeSplitter(
                pointsNew,
                halfEdgesNew,
                pointStatusesNew,
                facesNew,
                boundaryEdgesNew
                );

            for (int i = 0; i < heToSplit.Count; i++)
            {
                int he = heToSplit[i];
                int trid = he / 3;
                //рассчитать координаты для новых точек
                int vid = HalfEdgeUtils.Origin(facesNew, he);
                int vid1 = HalfEdgeUtils.Origin(facesNew, HalfEdgeUtils.Next(he));
                IList<IHPoint> pointsToSplit = FindSplitPoints(pointsNew[vid], pointsNew[vid1]);

                int curHe = he;
                foreach (var p in pointsToSplit)
                    curHe = edgeSplitter.SplitEdge(curHe, p);
            }
            var refinedMesh = new RestrictedDCEL(
                pointsNew.ToArray(),
                halfEdgesNew.ToArray(),
                pointStatusesNew.ToArray(),
                facesNew.ToArray(),
                boundaryEdgesNew.ToArray()
                );
            return refinedMesh;
        }

        #region Вспомогательные
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

        /// <summary>
        /// Разделить отрезок на несколько равных частей
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns>новые точки</returns>
        public IList<IHPoint> FindSplitPoints(IHPoint start, IHPoint end)
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
        #endregion
    }
}
