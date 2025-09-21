using CommonLib.Areas;
using CommonLib.Geometry;
using GeometryLib.Areas;
using MemLogLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.DCELMesh;
using TestDelaunayGenerator.SimpleStructures;
using GeometryLib.Locators;
using System.Diagnostics;
using Serilog;

namespace TestDelaunayGenerator.Smoothing
{
    public class Smoother
    {
        public SmootherConfig Config;

        IRestrictedDCEL mesh;
        AreaSegment[] areaSegments;

        public Smoother(SmootherConfig config = null)
        {
            this.Config = config;
            if (this.Config is null)
            {
                this.Config = new SmootherConfig();
            }
            if (!(0 < Config.SmoothRatio && Config.SmoothRatio <= 1))
                throw new ArgumentException($"Коэффициент сглаживания выходит за пределы (0,1]! " +
                    $"(текущее значение - {Config.SmoothRatio})");
        }

        public DcelTriMesh Smooth(IRestrictedDCEL mesh)
        {
            //валидация
            if (mesh is null)
                throw new ArgumentNullException($"{nameof(mesh)} не должна быть null!");
            this.mesh = mesh;

            int doneIterCnt = 0;

            //первая итерация
            var sw = Stopwatch.StartNew();
            InitializeIteration();
            sw.Stop();
            doneIterCnt++;
#if DEBUG
            int convexCnt = areaSegments.Count(s => s.IsConvex);
            Log.Information(
                    $"Первое (init) сглаживание. #{doneIterCnt}; " +
                    $"Время: {sw.Elapsed.TotalSeconds}(c); " +
                    $"Выпуклых сегментов: {convexCnt}/{areaSegments.Length} " +
                    $"({Math.Round((double)convexCnt / areaSegments.Length, 3) * 100}%)");
#endif

            //следующие обычные итерации
            for (; doneIterCnt < this.Config.IterationsCount; doneIterCnt++)
            {
                sw = Stopwatch.StartNew();
                CommonSmoothIteration();
#if DEBUG
                Log.Information($"Сглаживание #{doneIterCnt}. Время: {sw.Elapsed.TotalSeconds}(с)");
#endif
            }

            //конвертирование в сетку визуализатора
            var refiner = new MeshRefiner();
            refiner.Config.RebuildOnlyBoundary = true;
            //refiner.Config.SplitTriangleParts = 2;
            IRestrictedDCEL qualityMesh = refiner.Refine(mesh);
            return qualityMesh.ToDcelTriMesh();
        }


        /// <summary>
        /// Первая итерация сглаживания:
        /// внутри выполняется инициализация для массива <see cref="areaSegments"/>
        /// </summary>
        private void InitializeIteration()
        {
            //true - вершина уже обработана (пропущена или перемещена)
            bool[] isProcessed = null;
            MEM.Alloc(mesh.Points.Length, ref isProcessed, false);

            areaSegments = new AreaSegment[mesh.Points.Length];
            for (int he = 0; he < mesh.HalfEdges.Length; he++)
            {
                //id вершины
                int vid = HalfEdgeUtils.Origin(mesh.Faces, he);

                //если вершина ранее была обработана, то пропускаем её
                if (isProcessed[vid])
                    continue;
                //если треугольник внешний или удален
                if (mesh.Faces[he / 3].flag != TriangleState.Internal)
                    continue;

                //определение полуребер, смежных с vid
                int[] edgesAroundVId = HalfEdgeUtils.AdjacentEdgesVertex(mesh.HalfEdges, mesh.Faces, he, true);
                //внутри определяется выпуклость сегмента
                areaSegments[vid] = new AreaSegment(vid, mesh.PointStatuses[vid], edgesAroundVId, mesh);

                isProcessed[vid] = true;
                SmoothAreaSegment(vid);
                continue;
            }
        }
        /// <summary>
        /// Обычная итерация сглаживания.
        /// Требуется, чтобы <see cref="InitializeIteration"/> был вызван
        /// как минимум 1 раз
        /// </summary>
        /// <remarks>Использует проход по вершинам</remarks>
        protected void CommonSmoothIteration()
        {
            for (int vid = 0; vid < mesh.Points.Length; vid++)
            {
                SmoothAreaSegment(vid);
            }
        }

        /// <summary>
        /// Попытка рассчета и применения сглаживания к сегменту
        /// </summary>
        /// <param name="vid"></param>
        /// <returns>true - вершина была перемещена в новые координаты, иначе - false</returns>
        protected bool SmoothAreaSegment(int vid)
        {
            AreaSegment seg = areaSegments[vid];

            // пропуск граничной вершины
            if (seg.pointStatus == PointStatus.Boundary)
            {
                return false;
            }

            // вычисление новой координаты для vid
            (double avgX, double avgY) = GetSegmentAvgPoint(vid);

            // координаты с учетом КС
            (double newX, double newY) = (avgX, avgY);

            //область выпуклая, поэтому сразу применяем сглаживание
            if (seg.IsConvex is true)
            {
                (newX, newY) = UseSmoothRatio(vid, avgX, avgY, this.Config.SmoothRatio);
                mesh.Points[vid] = new HPoint(newX, newY);
                return true;
            }

            //сегмент невыпуклый

            //id треугольников, формирующих сегмент
            int[] trIds = seg.TriangleIds;
            //КС с учетом уменьшения
            double currentSmoothRatio = this.Config.SmoothRatio;
            //true - ни один треугольник из сегмента не вывернут
            bool isNotDestroyed = true;

            //проход по всем треугольникам сегмента
            for (int i = 0; i < trIds.Length; i++)
            {
                //id треугольника
                int trId = trIds[i];
                for (int attempt = 0; attempt < this.Config.AttemptCnt; attempt++)
                {
                    //новые координаты точки
                    (newX, newY) = UseSmoothRatio(vid, avgX, avgY, currentSmoothRatio);

                    isNotDestroyed = IsTriangleNotDestroyed(trId, vid, newX, newY);
                    //треугольник не вывернут, идем дальше
                    if (isNotDestroyed)
                        break;
                    //выворот треугольника
                    //уменьшаем коэффициент сглаживания
                    currentSmoothRatio *= this.Config.ReductionRatio;
                    //устанавливаем изначальные координаты
                    (newX, newY) = (mesh.Points[vid].X, mesh.Points[vid].Y);
                }

                //треугольник вывернут, поэтому нет смысла проверять оставшиеся,
                //вершина не перемещается в новые координаты
                if (!isNotDestroyed)
                {
                    break;
                }
            }

            //если не было выворотов, то применяем новые координаты
            if (isNotDestroyed)
            {
                mesh.Points[vid] = new HPoint(newX, newY);
                return true;
            }
            // выворот, поэтому точку не перемещаем
            else
            {
#if DEBUG
                Log.Warning(
                    $"Вершина {vid} не перемещена за {this.Config.AttemptCnt} попытки."
                );
#endif
                return false;
            }
        }


#region Вспомогательные функции
        // TODO если центром сегмента является граничная вершина,
        // то в вычислениях она не участвует => поправить
        /// <summary>
        /// Рассчитать координаты центра тяжести сегмента
        /// </summary>
        /// <param name="vid"></param>
        /// <returns></returns>
        protected (double, double) GetSegmentAvgPoint(int vid)
        {
            AreaSegment seg = this.areaSegments[vid];
            // вершины сегмента
            int[] vertexes = seg.AdjacentVertexes(mesh);

            //сумма
            (double sumX, double sumY) = (0, 0);
            for (int i = 0; i < vertexes.Length; i++)
            {
                int vertexId = vertexes[i];
                sumX += mesh.Points[vertexId].X;
                sumY += mesh.Points[vertexId].Y;
            }

            //центр тяжести
            double avgX = sumX / vertexes.Length;
            double avgY = sumY / vertexes.Length;
            return (avgX, avgY);
        }



        /// <summary>
        /// Применить сглаживание
        /// </summary>
        /// <param name="vid">индекс вершины/></param>
        /// <param name="avgX">новое значение по X</param>
        /// <param name="avgY">новое значение по Y</param>
        /// <returns></returns>
        protected (double, double) UseSmoothRatio(int vid, double avgX, double avgY, double smoothRatio)
        {
            if (smoothRatio == 1)
            {
                return (avgX, avgY);
            }
            else if (smoothRatio == 0)
            {
                return (mesh.Points[vid].X, mesh.Points[vid].Y);
            }
            //коэф в пределах (0,1)
            else
            {
                double newX = mesh.Points[vid].X + (avgX - mesh.Points[vid].X) * smoothRatio;
                double newY = mesh.Points[vid].Y + (avgY - mesh.Points[vid].Y) * smoothRatio;
                return (newX, newY);
            }
        }

        /// <summary>
        /// Проверка треугольника на разрушение/выворот.
        /// Основа - ориентировнная площадь,
        /// т.е. знак ориентированной площади до построения и после должен совпадать
        /// </summary>
        /// <param name="trid">id треугольника</param>
        /// <param name="vid">id перемещаемой вершины</param>
        /// <param name="newX">новая X вершины <paramref name="vid"/></param>
        /// <param name="newY">новая Y вершины <paramref name="vid"/></param>
        /// <returns>true - треугольник в норме, иначе - треугольник разрушен/вывернут</returns>
        protected bool IsTriangleNotDestroyed(int trid, int vid, double newX, double newY)
        {
            //вершины треугольника
            List<int> vertexes = new List<int>(3)
            {
                mesh.Faces[trid].i,
                mesh.Faces[trid].j,
                mesh.Faces[trid].k,
            };
            //удаляем вершину vid из списка вершин
            vertexes.Remove(vid);

            double areaOld = CrossLineUtils.DoubledOrientArea(new HPoint(mesh.Points[vid].X, mesh.Points[vid].Y),
                new HPoint(mesh.Points[vertexes[0]].X, mesh.Points[vertexes[0]].Y),
                new HPoint(mesh.Points[vertexes[1]].X, mesh.Points[vertexes[1]].Y)
                );

            double areaNew = CrossLineUtils.DoubledOrientArea(new HPoint(newX, newY),
                new HPoint(mesh.Points[vertexes[0]].X, mesh.Points[vertexes[0]].Y),
                new HPoint(mesh.Points[vertexes[1]].X, mesh.Points[vertexes[1]].Y)
                );

            //знаки должны совпасть, также 0 не допускается
            if (areaOld * areaNew > 0)
                return true;
            else
                return false;
        }
#endregion
    }
}
