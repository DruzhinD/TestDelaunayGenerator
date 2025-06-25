using CommonLib;
using CommonLib.Geometry;
using MemLogLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.Smoothing
{
    public class LaplacianSmoother : ISmoother
    {
        public void Smooth(ExtendedTriMesh mesh, double smoothRatio = 1)
        {
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
                    var defaultColor = Console.BackgroundColor;
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.WriteLine($"{vertexId} пропущена, не найдено соседей, смежные ребра: {string.Join(", ", edgesAroundVId)}");
                    Console.BackgroundColor = defaultColor;
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

                double newX, newY;
                //новые координаты
                if (smoothRatio == 1)
                {
                    newX = sumX / adjacentVertexes.Length;
                    newY = sumY / adjacentVertexes.Length;
                }
                else if (smoothRatio == 0)
                {
                    newX = mesh.CoordsX[vertexId];
                    newY = mesh.CoordsY[vertexId];
                }
                //коэф сглаживания между 0 и 1
                else
                {
                    newX = mesh.CoordsX[vertexId] + (sumX / adjacentVertexes.Length -  mesh.CoordsX[vertexId]) * smoothRatio;
                    newY = mesh.CoordsY[vertexId] + (sumY / adjacentVertexes.Length -  mesh.CoordsY[vertexId]) * smoothRatio;
                }

#if DEBUG
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
        /// Площадь треугольника
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        double TriangleArea(ExtendedTriMesh mesh, int triangleId)
        {
            TriElement triangle = mesh.AreaElems[triangleId];
            int v1 = (int)triangle.Vertex1;
            int v2 = (int)triangle.Vertex2;
            int v3 = (int)triangle.Vertex3;

            var X = mesh.CoordsX;
            var Y = mesh.CoordsY;

            double area = Math.Abs(
                (X[v1] * (Y[v2] - Y[v3]) + X[v2] * (Y[v3] - Y[v1]) + X[v3] * (Y[v1] - Y[v2])) *
                0.5
                );
            return area;
        }
    }
}
