using CommonLib.Geometry;
using GeometryLib;
using Serilog;
using Serilog.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.DCELMesh;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator
{
    public static class Interpolation
    {
        //ист. - TriagnleNET.Triangle.Interpolation
        /// <summary>
        /// Выполнение интерполяции атрибутов
        /// </summary>
        /// <param name="knot">узел, у которого требуется интерполировать атрибуты</param>
        /// <param name="mesh">сетка</param>
        /// <param name="trid">id треугольника, внутри которого находится интерполируемый узел</param>
        public static void InterpolateAttributes(CloudKnot knot, IRestrictedDCEL mesh, int trid)
        {
            Troika triangle = mesh.Faces[trid];
            //TODO вершины заданы по ч.с. - допустимо ли?
            var orgId = triangle[0];
            var destId = triangle[1];
            var apexId = triangle[2];

            var org = mesh.Points[orgId] as CloudKnot;
            var dest = mesh.Points[destId] as CloudKnot;
            var apex = mesh.Points[apexId] as CloudKnot;

            //валидация
            int errorKnotId = -1;
            if (org is null)
                errorKnotId = orgId;
            else if (dest is null)
                errorKnotId = destId;
            else if (apex is null)
                errorKnotId = apexId;

            if (errorKnotId == -1)
            {
                Log.Error($"Не удалось вычислить атрибуты для {knot.ID}: " +
                    $"vid={errorKnotId} (trid={trid}) не относится к типу с атрибутами");
                return;
            }

            InterpolateAttributes(knot, org, dest, apex);
        }


        /// <summary>
        /// Выполнение интерполяции атрибутов
        /// </summary>
        /// <param name="knot">узел, у которого требуется интерполировать атрибуты</param>
        ///<param name="org">1 узел треугольника, который включает <paramref name="knot"/></param>
        ///<param name="dest">2 узел треугольника, который включает <paramref name="knot"/></param>
        ///<param name="apex">3 узел треугольника, который включает <paramref name="knot"/></param>
        public static void InterpolateAttributes(CloudKnot knot, CloudKnot org, CloudKnot dest, CloudKnot apex)
        {

            int minCntAttribs = (new int[] { org.Attributes.Length, dest.Attributes.Length, apex.Attributes.Length })
                .Min();

            double xdo = dest.X - org.X;
            double ydo = dest.Y - org.Y;
            double xao = apex.X - org.X;
            double yao = apex.Y - org.Y;

            double denominator = 0.5 / (xdo * yao - xao * ydo);

            double dx = knot.X - org.X;
            double dy = knot.Y - org.Y;

            // To interpolate vertex attributes for the new vertex, define a
            // coordinate system with a xi-axis directed from the triangle's
            // origin to its destination, and an eta-axis, directed from its
            // origin to its apex.
            double xi = (yao * dx - xao * dy) * (2.0 * denominator);
            double eta = (xdo * dy - ydo * dx) * (2.0 * denominator);

            for (int i = 0; i < minCntAttribs; i++)
            {
                // Interpolate the vertex attributes.
                knot.Attributes[i] = org.Attributes[i]
                    + xi * (dest.Attributes[i] - org.Attributes[i])
                    + eta * (apex.Attributes[i] - org.Attributes[i]);
            }
        }
    }
}
