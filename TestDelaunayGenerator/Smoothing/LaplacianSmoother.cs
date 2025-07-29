using CommonLib;
using CommonLib.Geometry;
using MemLogLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TestDelaunayGenerator.SimpleStructures;
using TestDelaunayGenerator.DCELMesh;
using System.Diagnostics;
using System.Windows.Forms;
using Serilog;

namespace TestDelaunayGenerator.Smoothing
{
    public class LaplacianSmoother : SmootherBase
    {
        /// <summary>
        /// Общее количество итераций
        /// </summary>
        public int TotalIterations => totalIterations;
        /// <summary>
        /// Общее количество итераций сглаживания,
        /// доступно для записи
        /// </summary>
        protected int totalIterations = 0;
        bool firstIterFlag = false;

        /// <summary>
        /// Содержит массив треугольников, с которыми связаны вершины.
        /// Индексация совпадает с <see cref="IRestrictedDCEL.Points"/>
        /// </summary>
        protected Segment[] segments;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="config"><inheritdoc/></param>
        /// <param name="mesh"><inheritdoc/></param>
        public LaplacianSmoother(SmootherConfig config, IRestrictedDCEL mesh)
            : base(config, mesh)
        {
        }

        public override void Smooth()
        {
            //валидация
            if (mesh is null)
                throw new ArgumentNullException($"{nameof(mesh)} не должна быть null!");
            if (!(0 <= Config.SmoothRatio && Config.SmoothRatio <= 1))
                throw new ArgumentException($"Коэффициент сглаживания выходит за пределы [0,1]! " +
                    $"(текущее значение - {Config.SmoothRatio})");

            //итераций нет
            if (firstIterFlag is false)
            {
                Stopwatch sw = Stopwatch.StartNew();
                SegmentsInitializeIteration();
                sw.Stop();
                int convexCnt = segments.Count(s => s.isConvex);
                Log.Information(
                    $"Сглаживание инициализации сегментов. #{totalIterations}. " +
                    $"Время: {sw.Elapsed.TotalSeconds}(c). " +
                    $"Выпуклых сегментов: {convexCnt}/{segments.Length}");
                totalIterations += 1;
            }

            //итерации сглаживания
            for (int iteration = 0; iteration < this.Config.IterationsCount; iteration++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                CommonSmoothIteration();
                sw.Stop();
                totalIterations++;
                Log.Information(
                    $"Сглаживание #{totalIterations}. Время: {sw.Elapsed.TotalSeconds}(c)");
            }
            //TODO поправить
            mesh = new QualityMesher(
                new QualityMesherConfig()
                { SplitTriangleParts = 2, RebuildOnlyBoundary = false, MaxAngle = Math.PI * 3.5 / 6 })
                .Refine(mesh);
            mesh = mesh.ToDcelTriMesh();
            firstIterFlag = false;
        }

        /// <summary>
        /// Первая итерация сглаживанияю
        /// В процессе заполняется массив <see cref="segments"/>,
        /// в котором хранятся индексы на id треугольников, в которые входят вершины
        /// </summary>
        /// <remarks>Использует проход по полуребрам (значительно больше, чем вершин)</remarks>
        protected void SegmentsInitializeIteration()
        {
            //true - вершина уже обработана (пропущена или перемещена)
            bool[] isProcessed = null;
            MEM.Alloc(mesh.Points.Length, ref isProcessed, false);

            //TODO не перезаписывать, а модифицировать массив
            //выделение памяти под массив
            segments = new Segment[mesh.Points.Length];
            for (int halfEdgeId = 0; halfEdgeId < mesh.HalfEdges.Length; halfEdgeId++)
            {
                //id вершины
                int vertexId = mesh.Faces[halfEdgeId / 3][halfEdgeId % 3];

                //если вершина ранее была обработана, то пропускаем её
                if (isProcessed[vertexId])
                    continue;

                //определение полуребер, смежных с vid
                int[] edgesAroundVId = HalfEdgesUtils.AdjacentEdgesVertex(mesh.HalfEdges, mesh.Faces, halfEdgeId, true);
                segments[vertexId] = new Segment(vertexId, mesh.PointStatuses[vertexId], edgesAroundVId);
                //определение выпуклости
                segments[vertexId].isConvex = IsSegmentConvex(vertexId);

                isProcessed[vertexId] = true;
                SmoothSegment(vertexId);
                continue;
            }
        }

        /// <summary>
        /// Обычная итерация сглаживания.
        /// Требуется, чтобы <see cref="SegmentsInitializeIteration"/> был вызван
        /// как минимум 1 раз
        /// </summary>
        /// <remarks>Использует проход по вершинам</remarks>
        protected void CommonSmoothIteration()
        {
            for (int vid = 0; vid < mesh.Points.Length; vid++)
            {
                SmoothSegment(vid);
            }
        }

        /// <summary>
        /// Попытка рассчета и применения сглаживания к сегменту
        /// </summary>
        /// <param name="vid"></param>
        /// <returns>true - вершина была перемещена в новые координаты, иначе - false</returns>
        protected bool SmoothSegment(int vid)
        {
            Segment seg = segments[vid];

            // пропуск граничной вершины
            if (seg.pointStatus == PointStatus.Boundary)
            {
#if DEBUG
                //Log.Debug($"{vid} пропущена, т.к. является граничной");
#endif
                return false;
            }

            // вычисление новой координаты для vid
            (double avgX, double avgY) = GetSegmentAvgPoint(vid);

            // координаты с учетом КС
            (double newX, double newY) = (avgX, avgY);

            //область выпуклая, поэтому сразу применяем сглаживание
            if (seg.isConvex is true)
            {
#if DEBUG
                //Log.Debug($"Перемещение вершины {vid} из " +
                //    $"({mesh.Points[vid].X}, {mesh.Points[vid].Y}) " +
                //    $"в ({newX}, {newY})");
#endif
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

#if DEBUG
                    //Log.Debug(
                    //    $"не удалось переместить вершину {vid} ({mesh.Points[vid].X},{mesh.Points[vid].Y}) " +
                    //    $"в новые координаты ({newX},{newY}) - выворот треугольника {trId}({mesh.Faces[trId]})"
                    //    );
                    //Log.Debug($"Установка коэффициента сглаживания:{currentSmoothRatio}");

#endif
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
#if DEBUG
                Log.Debug($"Перемещение вершины {vid} из " +
                    $"({mesh.Points[vid].X}, {mesh.Points[vid].Y}) " +
                    $"в ({newX}, {newY})");
#endif
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
            Segment seg = this.segments[vid];
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
        /// Рассчитать ориентированную площадь
        /// </summary>
        protected static double OrientArea(IHPoint p1, IHPoint p2, IHPoint p3)
        {
            double area = (p2.X - p1.X) * (p3.Y - p1.Y) -
                (p2.Y - p1.Y) * (p3.X - p1.X);
            return area;
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

            double areaOld = OrientArea(new HPoint(mesh.Points[vid].X, mesh.Points[vid].Y),
                new HPoint(mesh.Points[vertexes[0]].X, mesh.Points[vertexes[0]].Y),
                new HPoint(mesh.Points[vertexes[1]].X, mesh.Points[vertexes[1]].Y)
                );

            double areaNew = OrientArea(new HPoint(newX, newY),
                new HPoint(mesh.Points[vertexes[0]].X, mesh.Points[vertexes[0]].Y),
                new HPoint(mesh.Points[vertexes[1]].X, mesh.Points[vertexes[1]].Y)
                );

            //знаки должны совпасть, также 0 не допускается
            if (areaOld * areaNew > 0)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Является ли сегмент выпуклым
        /// </summary>
        /// <param name="vid">ID вершины</param>
        /// <returns>true - сегмент выпуклый, иначе - false</returns>
        protected bool IsSegmentConvex(int vid)
        {
            //берем указатель (переменную) для упрощения записи
            Segment seg = segments[vid];
            // вершины, формирующие сегмент
            int[] segmentVertexes = seg.AdjacentVertexes(mesh);

            // vid - граничная
            // в начало массива вершин сегмента добавляем vid
            if (seg.pointStatus == PointStatus.Boundary)
            {
                int[] tempSeg = segmentVertexes;
                segmentVertexes = new int[1 + tempSeg.Length];
                segmentVertexes[0] = vid;
                Array.Copy(tempSeg, 0, segmentVertexes, 1, tempSeg.Length);
            }

            //для замкнутых областей
            //условие для выявления аномалий
            if (seg.pointStatus != PointStatus.Boundary)
            {
                //в первый треугольник должна входить и первая и последняя вершины
                int trid = seg.halfEdgeIds[seg.halfEdgeIds.Length - 1] / 3;
                //id вершин треугольника
                int[] trVertexes = new int[] { mesh.Faces[trid].i, mesh.Faces[trid].j, mesh.Faces[trid].k };
                if ((trVertexes.Contains(segmentVertexes[0]) &&
                    trVertexes.Contains(segmentVertexes[segmentVertexes.Length - 1])) == false)
                    throw new ArgumentException($"Общая вершина не граничная, но сегмент не замкнут!");

            }

            //произведение векторных произведений должно быть > 0
            //вычисление первого векторного произведенияы
            double firstVectorCross = VectorCross(
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
                double localCross = VectorCross(
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

        /// <summary>
        /// Рассчитать векторное произведение
        /// </summary>
        protected static double VectorCross(IHPoint left, IHPoint center, IHPoint right)
        {
            double cross = (left.X - center.X) * (right.Y - center.Y) -
                (left.Y - center.Y) * (right.X - center.X);
            return cross;
        }
        #endregion
    }
}
