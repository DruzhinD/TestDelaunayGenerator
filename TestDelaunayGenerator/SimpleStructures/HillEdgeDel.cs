using CommonLib.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.SimpleStructures
{
    //TODO перенести в либу GeometryLib
    /// <summary>
    /// Ребро границы. Может содержать целое множество точек, а не только свои вершины.
    /// По сути является ребром, построенном на паре опорных вершин оболочки
    /// Реализовано в рамках делонатора
    /// </summary>
    public struct HillEdgeDel : IHillEdge
    {

        public HillEdgeDel(int id, IHPoint iHPoint1, IHPoint iHPoint2, int mark = 0, int count = 0)
            : this()
        {
            ID = id;
            A = iHPoint1;
            B = iHPoint2;
            this.mark = mark;
            this.Count = count;
        }

        public int mark { get; set; }
        public int Count { get; set; }
        public int ID { get; set; }
        public IHPoint A { get; set; }
        public IHPoint B { get; set; }
    }
}
