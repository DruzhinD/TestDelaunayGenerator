using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.DelaunatorModels
{
    /// <summary>
    /// Хранит информацию о принадлежности точки <see cref="vid"/> контуру
    /// </summary>
    public struct ContourPoint
    {
        //TODO проверить корректность обхода
        /// <summary>
        /// Определение точки <paramref name="vid"/> как, входящей в контур
        /// </summary>
        /// <param name="prevVid">предыдущая вершина при обходе по ч.с.</param>
        /// <param name="nextVid">следующая вершина при обходе по ч.с.</param>
        /// <param name="boundaryId">Индекс граничного контура (оболочки), которой принадлежит точка</param>
        public ContourPoint(int vid, int prevVid, int nextVid, int boundaryId)
        {
            this.vid = vid;
            this.PrevVid = prevVid;
            this.NextVid = nextVid;
            this.BoundaryID = boundaryId;
        }

        /// <summary>
        /// Индекс текущей вершины
        /// </summary>
        public int vid;

        /// <summary>
        /// Индекс 1-ой соседней вершины с <see cref="vid"/>
        /// </summary>
        public int PrevVid;
        /// <summary>
        /// Индекс 2-ой соседней вершины с <see cref="vid"/>
        /// </summary>
        public int NextVid;

        /// <summary>
        /// Индексы соседних вершин с <see cref="vid"/>
        /// </summary>
        public int[] Adjacents => new int[] { PrevVid, NextVid };

        /// <summary>
        /// Индекс граничного контура (оболочки), которой принадлежит <see cref="vid"/>
        /// </summary>
        public int BoundaryID;

        //если значения равны, то точка неграничная, т.е. не было изменено значений по умолчанию
        public bool IsBoundary => !(vid == PrevVid && vid == NextVid);
    }
}
