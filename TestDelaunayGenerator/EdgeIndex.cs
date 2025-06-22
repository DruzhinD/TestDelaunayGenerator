using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator
{
    /// <summary>
    /// Структура ребра на основе индексов вершин в замкнутом контуре
    /// </summary>
    public struct EdgeIndex
    {
        /// <summary>
        /// Создание ребра на основе индексов вершин
        /// </summary>
        /// <param name="adjacent1">1-ая соседняя вершина</param>
        /// <param name="adjacent2">2-ая соседняя вершина</param>
        /// <param name="boundaryId">Индекс граничного контура (оболочки), которой принадлежит точка</param>
        public EdgeIndex(int pointID, int adjacent1, int adjacent2, int boundaryId)
        {
            PointID = pointID;
            this.adjacent1 = adjacent1;
            this.adjacent2 = adjacent2;
            this.BoundaryID = boundaryId;
        }

        /// <summary>
        /// Индекс текущей вершины
        /// </summary>
        public int PointID;

        /// <summary>
        /// Индекс 1-ой соседней вершины с <see cref="PointID"/>
        /// </summary>
        public int adjacent1;
        /// <summary>
        /// Индекс 2-ой соседней вершины с <see cref="PointID"/>
        /// </summary>
        public int adjacent2;

        /// <summary>
        /// Индексы соседних вершин с <see cref="PointID"/>
        /// </summary>
        public int[] Adjacents => new int[] { adjacent1, adjacent2 };

        /// <summary>
        /// Индекс граничного контура (оболочки), которой принадлежит <see cref="PointID"/>
        /// </summary>
        public int BoundaryID;
    }
}
