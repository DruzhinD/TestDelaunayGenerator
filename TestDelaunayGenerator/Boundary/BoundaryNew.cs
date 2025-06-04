using CommonLib.Geometry;
using GeometryLib.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.Boundary
{
    public class BoundaryNew
    {
        /// <summary>
        /// Вершины, обращующие форму оболочки (опорные вершины)
        /// </summary>
        public IHPoint[] BaseVertexes { get => baseVertexes; }

        /// <summary>
        /// Все множество точек, принадлежащее оболочке, включая опорные вершины
        /// <see cref="BaseVertexes"/>
        /// </summary>
        public IHPoint[] Points { get => points; }


        /// <summary>
        /// вершины, образующие форму оболочки (опорные вершины)
        /// </summary>
        protected IHPoint[] baseVertexes;

        /// <summary>
        /// все множество точек, принадлежащее оболочке, включая опорные вершины
        /// <see cref="baseVertexes"/>
        /// </summary>
        protected IHPoint[] points;

        public int[] VertexesIds;

        /// <summary>
        /// массив граничных ребер(пары индексов точек)
        /// </summary>
        private IHillEdge[] _boundaryEdges;
        public IHillEdge[] BoundaryEdges { get => _boundaryEdges; private set => _boundaryEdges = value; }

        /// <summary>
        /// Инициализация оболочки
        /// </summary>
        /// <param name="baseVertexes">опорные вершины, образующие форму оболочки</param>
        /// <param name="generator">правила генерации точек на ребрах оболочки, между опорными вершинами</param>
        /// <exception cref="ArgumentNullException"></exception>
        public BoundaryNew(IHPoint[] baseVertexes, IGeneratorBase generator)
        {
            //проверка на null
            if (baseVertexes is null || baseVertexes.Length == 0)
                throw new ArgumentNullException($"{nameof(baseVertexes)} null или пуст");
            if (generator is null)
                throw new ArgumentNullException($"{nameof(generator)} не может быть null");

            // Помечаем опорные вершины как граничные
            this.baseVertexes = new IHPoint[baseVertexes.Length];
            for (int i = 0; i < baseVertexes.Length; i++)
            {
                if (baseVertexes[i] is HPoint hp)
                {
                    this.baseVertexes[i] = new HPoint(hp.X, hp.Y, isBoundary: true, boundaryEdgeMarker: i);
                }
                else
                {
                    this.baseVertexes[i] = new HPoint(baseVertexes[i].X, baseVertexes[i].Y, isBoundary: true, boundaryEdgeMarker: i);
                }
            }

            //TODO сменить тип
            this.points = generator.Generate(this);
            this.InitilizeRect();

            //сохраняем индексы вершин, образующих область
            VertexesIds = new int[this.BaseVertexes.Length];
            int currentVertexId = 0;
            for (int i = 0; i < Points.Length; i++)
            {
                if (BaseVertexes[currentVertexId].X == Points[i].X &&
                    BaseVertexes[currentVertexId].Y == Points[i].Y)
                {
                    VertexesIds[currentVertexId] = i;
                    currentVertexId++;
                    if (currentVertexId == VertexesIds.Length)
                        break;
                }
            }
            // Инициализация граничных ребер
            InitializeBoundaryEdges2();
        }
        private void InitializeBoundaryEdges2()
        {
            int n = BaseVertexes.Length;
            _boundaryEdges = new IHillEdge[n];
            for (int i = 0; i < n; i++)
            {
                int start = VertexesIds[i];
                int end = VertexesIds[(i + 1) % n];
                _boundaryEdges[i] = new HEdge(i, Points[start], Points[end], mark: 1, isBoundary: true);
            }
        }
        private void InitializeBoundaryEdges()
        {

            // Количество опорных вершин (только они определяют настоящие граничные рёбра)
            int n = BaseVertexes.Length;
            _boundaryEdges = new IHillEdge[n];
            for (int i = 0; i < n; i++)
            {
                int start = VertexesIds[i];
                int end = VertexesIds[(i + 1) % n];
                _boundaryEdges[i] = new HEdge(i, Points[start], Points[end], mark: 1, isBoundary: true);
            




            //Console.WriteLine($"Edge {i}: ({start}, {end})");
            // Выводим индексы и координаты вершин ребра
            //Console.WriteLine($"Edge {i}: ({start}, {end}) -> " +
            //                  $"(({Points[start].X}, {Points[start].Y}), " +
            //                  $"({Points[end].X}, {Points[end].Y}))");
            }
        }


        /// <summary>
        /// Прямоугольник, описанный около текущей ограниченной области
        /// </summary>
        public IHPoint[] OutRect => outRect;
        public IHPoint[] outRect;

        /// <summary>
        /// Инициализация прямоугольника, описанного около оболочки
        /// </summary>
        protected void InitilizeRect()
        {
            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            //собираем края области
            foreach (var vertex in this.BaseVertexes)
            {
                if (vertex.X < minX)
                    minX = vertex.X;
                if (vertex.X > maxX)
                    maxX = vertex.X;
                if (vertex.Y < minY)
                    minY = vertex.Y;
                if (vertex.Y > maxY)
                    maxY = vertex.Y;
            }
            //формируем описанный прямоугольник
            IHPoint[] rectangle = new IHPoint[4];
            rectangle[0] = new HPoint(minX, minY);
            rectangle[1] = new HPoint(minX, maxY);
            rectangle[2] = new HPoint(maxX, maxY);
            rectangle[3] = new HPoint(maxX, minY);
            this.outRect = rectangle;
        }
        public bool IsBoundaryEdge(int start, int end)
        {
            if (Points[start] is HPoint p1 && Points[end] is HPoint p2)
            {
                bool isBoundary = p1.IsBoundary && p2.IsBoundary && p1.BoundaryEdgeMarker == p2.BoundaryEdgeMarker;
                if (isBoundary)
                {
                    Console.WriteLine($"Граничное ребро найдено: ");
                    Console.WriteLine($"Точка 1: ({p1.X:F4}, {p1.Y:F4}), IsBoundary: {p1.IsBoundary}, BoundaryEdgeMarker: {p1.BoundaryEdgeMarker}");
                    Console.WriteLine($"Точка 2: ({p2.X:F4}, {p2.Y:F4}), IsBoundary: {p2.IsBoundary}, BoundaryEdgeMarker: {p2.BoundaryEdgeMarker}");
                }
                return isBoundary;
            }
            return false;
        }
        public bool IsBoundaryEdge2(int start, int end)
        {
            if (Points[start] is HPoint p1 && Points[end] is HPoint p2)
            {
                return p1.IsBoundary && p2.IsBoundary && p1.BoundaryEdgeMarker == p2.BoundaryEdgeMarker;
            }
            return false;
        }
       
    }
}
