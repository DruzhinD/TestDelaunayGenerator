using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.SimpleStructures
{
    /// <summary>
    /// Пара ребер, соединенных общей вершиной
    /// </summary>
    public struct EdgePair
    {
        /// <summary>
        /// Создание ребра на основе индексов вершин
        /// </summary>
        /// <param name="adjacent1">1-ая соседняя вершина</param>
        /// <param name="adjacent2">2-ая соседняя вершина</param>
        /// <param name="boundaryId">Индекс граничного контура (оболочки), которой принадлежит точка</param>
        public EdgePair(int vid, int adjacent1, int adjacent2, int boundaryId)
        {
            this.vid = vid;
            this.adjacent1 = adjacent1;
            this.adjacent2 = adjacent2;
            this.BoundaryID = boundaryId;
        }

        /// <summary>
        /// Индекс текущей вершины
        /// </summary>
        public int vid;

        /// <summary>
        /// Индекс 1-ой соседней вершины с <see cref="vid"/>
        /// </summary>
        public int adjacent1;
        /// <summary>
        /// Индекс 2-ой соседней вершины с <see cref="vid"/>
        /// </summary>
        public int adjacent2;

        /// <summary>
        /// Индексы соседних вершин с <see cref="vid"/>
        /// </summary>
        public int[] Adjacents => new int[] { adjacent1, adjacent2 };

        /// <summary>
        /// Индекс граничного контура (оболочки), которой принадлежит <see cref="vid"/>
        /// </summary>
        public int BoundaryID;
    }
}
