using CommonLib.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DelaunayUI
{
    /// <summary>
    /// Содержит генераторы контуров
    /// </summary>
    public class GeneratorUtils
    {

        /// <summary>
        /// Генератор правильного многоугольника
        /// </summary>
        /// <param name="radius">радиус описанной окружности</param>
        /// <param name="vertexesCnt"></param>
        /// <param name="center"></param>
        /// <returns></returns>
        public static IHPoint[] TruePolygonVertices(double radius, int vertexesCnt, IHPoint center)
        {
            var vertexes = new IHPoint[vertexesCnt];

            for (int i = 0; i < vertexesCnt; i++)
            {
                double theta = (2 * Math.PI * i - Math.PI) / vertexesCnt;
                double x = center.X + radius * Math.Cos(theta);
                double y = center.Y + radius * Math.Sin(theta);
                vertexes[i] = new HPoint(x, y);
            }
            return vertexes;
        }


        /// <summary>
        /// Звездообразный контур
        /// </summary>
        /// <param name="vertexCnt">округляется до ближайшего меньшего четного числа</param>
        /// <param name="innerR"></param>
        /// <param name="externalR"></param>
        /// <param name="alpha">в радианах. Не влияет на форму контура, влияет на угол поворота</param>
        /// <param name="center"></param>
        /// <returns></returns>
        public static IHPoint[] Star(int vertexCnt, double innerR, double externalR, double alpha, IHPoint center)
        {
            int startVertexCnt = (vertexCnt / 2);
            IHPoint[] vertexes = new IHPoint[2 * startVertexCnt];
            double a = alpha;
            double da = Math.PI / startVertexCnt;
            double l;
            for (int k = 0; k < 2 * startVertexCnt; k++)
            {
                l = k % 2 == 0 ? externalR : innerR;
                vertexes[k] = new HPoint(center.X + l * Math.Cos(a), center.Y + l * Math.Sin(a));
                a += da;
            }
            return vertexes;
        }
    }

    /// <summary>
    /// Выбор формы контура для генерации
    /// </summary>
    public enum Figure
    {
        /// <summary>
        /// Правильный многоугольник
        /// </summary>
        RegularPolygon,
        /// <summary>
        /// звезда
        /// </summary>
        RegularStar
    }
}
