using CommonLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator;

namespace TestDelaunayGenerator.Smoothing
{
    [Obsolete("Пока не готово")]
    public class OdtSmoother : ISmoother
    {
        //[Obsolete("Пока не готово")]
        public void Smooth(ExtendedTriMesh mesh, double smoothRatio = 1)
        {
            //проход по всем вершинам
            for (int pId = 0; pId < mesh.CountKnots; pId++)
            {
                //треугольники, составляющие область омега
                int[] trianglesInOmega =
                    HalfEdgesUtils.AdjacentTrianglesWithEdge(mesh.HalfEdges, pId);

                //площадь сегмента омега
                double omegaArea = 0;

                //вектор, накапливающий взвешенные градиенты площадей
                //т.е. произведение градиента и суммы норм векторов
                double sum_term = 0;

                //проход по всем треугольникам из омега
                for (int i = 0; i < trianglesInOmega.Length; i++)
                {
                    //рассчет площади
                    omegaArea += trianglesInOmega[i];

                    //градиент треугольника
                }
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
