namespace TestDelaunayGenerator.Boundary
{
    using System;
    using System.Linq;
    using CommonLib.Geometry;
    using GeometryLib;
    using MemLogLib;

    /// <summary>
    /// Класс для определения контура области, через грани оболочки и 
    /// </summary>
    public class CounturHill
    {
        /// <summary>
        /// Название контура
        /// </summary>
        public string Name;
        /// <summary>
        /// Точки, образующие границу, включая вершины области
        /// </summary>
        public HNumbKnot[] Points = null;
        /// <summary>
        /// Грани оболочки
        /// </summary>
        public IHillEdge[] hEdges = null;
        public CounturHill(string Name, IHillEdge[] hEdges)
        {
            this.Name = Name;
            this.hEdges = hEdges;
            Init();
        }
        /// <summary>
        /// Сгенерировать опорные точки границы и определить их маркер
        /// </summary>
        protected void Init()
        {
            for (int i = 0; i < hEdges.Length; i++)
                if (MEM.Equals(hEdges[i].B, hEdges[(i + 1) % hEdges.Length].A) == false)
                    throw new Exception("Контур оболочки не замкнут");

            int countPints = hEdges.Sum(x=>x.Count) - hEdges.Length;
            Points = new HNumbKnot[countPints];
            int ip = 0;
            foreach(var e in hEdges)
            {
                double dx = (e.A.X - e.B.X) / e.Count;
                double dy = (e.A.Y - e.B.Y) / e.Count;
                for (int p = 0; p < e.Count - 1; p++) 
                    Points[ip] = new HNumbKnot(e.A.X + dx * p, e.A.Y + dy * p, e.mark, ip++);
            }
        }
    }
}
