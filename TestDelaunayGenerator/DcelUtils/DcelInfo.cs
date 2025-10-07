using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.DelaunatorModels;

namespace TestDelaunayGenerator.DcelUtils
{
    public static class DcelInfo
    {


        /// <summary>
        /// Сведения о полуребре: id, twin, треугольник, вершины, другие полуребра
        /// </summary>
        /// <param name="faces"></param>
        /// <param name="he"></param>
        /// <returns></returns>
        public static string HeInfo(IList<Triangle> faces, IList<int> halfEdges, int he, bool extended = false)
        {
            int trid = he / 3;
            string log = $"he:{he};twin:{HalfEdgeUtils.Twin(halfEdges, he)};";
            //origin
            if (extended)
                log += $"origin:{HalfEdgeUtils.Origin(faces, he)};";
            //trid
            log += $"trid:{trid}{faces[trid].Get()};";
            int he1 = trid * 3;
            int he2 = trid * 3 + 1;
            int he3 = trid * 3 + 2;
            //he
            if (extended)
                log += $"he:({he1}|{HalfEdgeUtils.Twin(halfEdges, he1)}, {he2}|" +
                    $"{HalfEdgeUtils.Twin(halfEdges, he2)}, {he3}|" +
                    $"{HalfEdgeUtils.Twin(halfEdges, he3)});";
            else
                log += $"he:{(he1, he2, he3)};";

            return log;
        }

        public static string TwinHEdges(int[] halfEdges, int trid)
        {
            string log = "";
            for (int he = trid * 3; he < trid * 3 + 3; he++)
            {
                log += $"{he}->{HalfEdgeUtils.Twin(halfEdges, he)};";
            }
            return log;
        }


        /// <summary>
        /// Сведения о треугольнике: id, вершины, полуребра
        /// </summary>
        /// <param name="faces"></param>
        /// <param name="trid"></param>
        /// <returns></returns>
        public static string TriangleInfo(IList<Triangle> faces, int trid)
        {
            string log = $"trid:{trid};vid:{faces[trid].Get()};he:({trid * 3},{trid * 3 + 1},{trid * 3 + 2})";
            return log;
        }
    }
}
