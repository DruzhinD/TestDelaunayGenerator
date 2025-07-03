using CommonLib;
using CommonLib.Geometry;
using MemLogLib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.Smoothing
{
    public class LaplacianSmoother : ISmoother
    {
        /// <summary>
        /// Объект сетки
        /// </summary>
        protected ExtendedTriMesh mesh;

        /// <summary>
        /// количество попыток перемещения точки в новые координаты
        /// с уменьшем степени сглаживания
        /// </summary>
        const int attemptCount = 1;

        public void Smooth(ExtendedTriMesh mesh, double smoothRatio = 1)
        {
            this.mesh = mesh;
            //валидация
            if (mesh is null)
                throw new ArgumentNullException($"{nameof(mesh)} не должна быть null!");
            if (!(0 <= smoothRatio && smoothRatio <= 1))
                throw new ArgumentException($"Коэффициент сглаживания выходит за пределы [0,1]! " +
                    $"(текущее значение - {smoothRatio})");

            //true - вершина уже обработана (пропущена или перемещена)
            bool[] isProcessed = null;
            MEM.Alloc(mesh.CountKnots, ref isProcessed, false);

            //проход по всем полуребрам (поиск основан на полуребрах)
            for (int halfEdgeId = 0; halfEdgeId < mesh.HalfEdges.Length; halfEdgeId++)
            {
                //id вершины
                int vertexId = mesh.Triangles[halfEdgeId / 3][halfEdgeId % 3];

                //если вершина ранее была обработана, то пропускаем её
                if (isProcessed[vertexId])
                    continue;

                //пропуск граничных вершин
                if (mesh.PointStatuses[vertexId] == PointStatus.Boundary)
                {
#if DEBUG
                    Console.WriteLine($"{vertexId} пропущена, т.к. является граничной");
#endif
                    //помечаем как обработанную
                    isProcessed[vertexId] = true;
                    continue;
                }

                //треугольники сегмента омега
                int[] edgesAroundVId = HalfEdgesUtils.EdgesAroundVertex(mesh.HalfEdges, mesh.Triangles, halfEdgeId);
                //нет соседей
                if (edgesAroundVId.Length < 2)
                {
#if DEBUG
                    Utils.ConsoleWriteLineColored(
                        ConsoleColor.Red,
                        $"{vertexId} пропущена, не найдено соседей, смежные ребра: {string.Join(", ", edgesAroundVId)}"
                        );
#endif
                    isProcessed[vertexId] = true;
                    continue;
                }
                int[] adjacentVertexes = edgesAroundVId.Select(x => mesh.GetVertex(x)).ToArray();

                //суммируем вершины
                double sumX = 0, sumY = 0;
                for (int i = 0; i < adjacentVertexes.Length; i++)
                {
                    sumX += mesh.CoordsX[adjacentVertexes[i]];
                    sumY += mesh.CoordsY[adjacentVertexes[i]];
                }

                //новая координата без коэф сглаживания
                double avgX = sumX / adjacentVertexes.Length;
                double avgY = sumY / adjacentVertexes.Length;

                //новые значения с учетом сглаживания
                double newX = mesh.CoordsX[vertexId];
                double newY = mesh.CoordsY[vertexId];

                bool isNotDestroyed = true;
                //проверка выворота треугольников
                //проход по всем треугольникам, которые содержат вершину
                for (int i = 0; i < edgesAroundVId.Length; i++)
                {
                    #region Второй способ
                    double currentSmoothRatio = smoothRatio;
                    //выполняем перемещение вершины до тех пор,
                    //пока перемещение не станет приемлемым
                    for (int attempt = 0; attempt < attemptCount; attempt++)
                    {
                        //новые координаты точки
                        (newX, newY) = UseSmoothRatio(vertexId, avgX, avgY, currentSmoothRatio);

                        //id полуребра
                        int edgeId = mesh.HalfEdges[edgesAroundVId[i]];
                        int trid = edgeId / 3;

                        isNotDestroyed = this.IsTriangleNotDestroyed(trid, vertexId, newX, newY);

                        //треугольник не вывернут, идем дальше
                        if (isNotDestroyed)
                            break;
                        //уменьшаем коэффициент сглаживания
                        currentSmoothRatio *= 0.5;
#if DEBUG
                        Utils.ConsoleWriteLineColored(
                            ConsoleColor.Red,
                            $"не удалось переместить вершину {vertexId} ({mesh.CoordsX[vertexId]},{mesh.CoordsY[vertexId]}) " +
                            $"в новые координаты ({newX},{newY}) - выворот треугольника {trid}({mesh.Triangles[trid]})"
                            );
                        Console.WriteLine($"Установка коэффициента сглаживания:{currentSmoothRatio}");

                        //если не удалось переместить вершину за заданное количество попыток,
                        if (attempt == attemptCount - 1)
                            Utils.ConsoleWriteLineColored(
                                ConsoleColor.DarkRed,
                                $"Вершина {vertexId} не перемещена за {attemptCount} попытки."
                                );
#endif
                        //устанавливаем изначальные координаты
                        (newX, newY) = (mesh.CoordsX[vertexId], mesh.CoordsY[vertexId]);
                    }
                    if (!isNotDestroyed)
                        break;
                    #endregion

                    #region Первый способ
                    //                    //новые координаты точки
                    //                    (newX, newY) = UseSmoothRatio(vertexId, avgX, avgY, smoothRatio);
                    //                    int edgeId = mesh.HalfEdges[edgesAroundVId[i]];
                    //                    //id вершин
                    //                    List<int> vertexIds = new List<int>(3);
                    //                    vertexIds.Add(mesh.Triangles[edgeId / 3].i);
                    //                    vertexIds.Add(mesh.Triangles[edgeId / 3].j);
                    //                    vertexIds.Add(mesh.Triangles[edgeId / 3].k);
                    //                    //удаляем из списка общую вершину
                    //                    vertexIds.Remove(vertexId);

                    //                    double areaOld = OrientArea(new HPoint(mesh.CoordsX[vertexId], mesh.CoordsY[vertexId]),
                    //                        new HPoint(mesh.CoordsX[vertexIds[0]], mesh.CoordsY[vertexIds[0]]),
                    //                        new HPoint(mesh.CoordsX[vertexIds[1]], mesh.CoordsY[vertexIds[1]])
                    //                        );

                    //                    double areaNew = OrientArea(new HPoint(newX, newY),
                    //                        new HPoint(mesh.CoordsX[vertexIds[0]], mesh.CoordsY[vertexIds[0]]),
                    //                        new HPoint(mesh.CoordsX[vertexIds[1]], mesh.CoordsY[vertexIds[1]])
                    //                        );

                    //                    //произведение должно быть положительным, т.е. знаки должны быть равны
                    //                    if (areaOld * areaNew > 0)
                    //                    {
                    //                        //break;
                    //                        continue;
                    //                    }
                    //                    //если произведение неположительное, то не перемещаем точку
                    //                    else
                    //                    {
                    //#if DEBUG
                    //                        Utils.ConsoleWriteLineColored(
                    //                            ConsoleColor.Red,
                    //                            $"Пропуск перемещения для точки {vertexId}({mesh.CoordsX[vertexId]},{mesh.CoordsY[vertexId]}) " +
                    //                            $"({vertexId},{vertexIds[0]},{vertexIds[1]}). " +
                    //                            $"Причина: выворот области"
                    //                            );
                    //#endif
                    //                        newX = mesh.CoordsX[vertexId];
                    //                        newY = mesh.CoordsY[vertexId];
                    //                        //TODO убрать 
                    //                        break;
                    //                    }
                    #endregion

                }
#if DEBUG
                if (isNotDestroyed)
                    Console.WriteLine($"Перемещение вершины {vertexId} из " +
                        $"({mesh.CoordsX[vertexId]}, {mesh.CoordsY[vertexId]}) " +
                        $"в ({newX}, {newY})");
#endif
                //сохраняем новые координаты
                mesh.CoordsX[vertexId] = newX;
                mesh.CoordsY[vertexId] = newY;
                isProcessed[vertexId] = true;
            }
        }

        /// <summary>
        /// Рассчитать ориентированную площадь
        /// </summary>
        double OrientArea(IHPoint p1, IHPoint p2, IHPoint p3)
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
        (double, double) UseSmoothRatio(int vid, double avgX, double avgY, double smoothRatio)
        {
            if (smoothRatio == 1)
            {
                return (avgX, avgY);
            }
            else if (smoothRatio == 0)
            {
                return (mesh.CoordsX[vid], mesh.CoordsY[vid]);
            }
            //коэф в пределах (0,1)
            else
            {
                double newX = mesh.CoordsX[vid] + (avgX - mesh.CoordsX[vid]) * smoothRatio;
                double newY = mesh.CoordsY[vid] + (avgY - mesh.CoordsY[vid]) * smoothRatio;
                return (newX, newY);
            }
        }

        //TODO доделать выворот
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
        bool IsTriangleNotDestroyed(int trid, int vid, double newX, double newY)
        {
            //вершины треугольника
            List<int> vertexes = new List<int>(3)
            {
                mesh.Triangles[trid].i,
                mesh.Triangles[trid].j,
                mesh.Triangles[trid].k,
            };
            //удаляем вершину vid из списка вершин
            vertexes.Remove(vid);

            double areaOld = OrientArea(new HPoint(mesh.CoordsX[vid], mesh.CoordsY[vid]),
                        new HPoint(mesh.CoordsX[vertexes[0]], mesh.CoordsY[vertexes[0]]),
                        new HPoint(mesh.CoordsX[vertexes[1]], mesh.CoordsY[vertexes[1]])
                        );

            double areaNew = OrientArea(new HPoint(newX, newY),
                new HPoint(mesh.CoordsX[vertexes[0]], mesh.CoordsY[vertexes[0]]),
                new HPoint(mesh.CoordsX[vertexes[1]], mesh.CoordsY[vertexes[1]])
                );

            //знаки должны совпасть, также 0 не допускается
            if (areaOld * areaNew > 0)
                return true;
            else
                return false;
        }
    }
}
