using CommonLib.Geometry;
using GeometryLib.Geometry;
using MemLogLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.Boundary
{
    public class BoundaryNew
    {
        /// <summary>
        /// Счетчик для уникальности границ
        /// </summary>
        protected static int uniqueIdCounter = 0;

        /// <summary>
        /// уникальный идентификатор границы
        /// </summary>
        public readonly int ID;

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
        /// Граничные ребра, формирующие оболочку
        /// </summary>
        protected IHillEdge[] _baseBoundaryEdges;
        /// <summary>
        /// Граничные ребра, формирующие оболочку.
        /// Построены на опорных вершинах оболочки <see cref="BaseVertexes"/>
        /// </summary>
        public IHillEdge[] BaseBoundaryEdges { get => _baseBoundaryEdges; }

        /// <summary>
        /// Граничные ребра оболочки.
        /// Построены на всем множестве точек оболочки <see cref="Points"/>.
        /// Если между опорными вершинами оболочки больше нет точек,
        /// тогда дублирует <see cref="_baseBoundaryEdges"/>
        /// </summary>
        protected IHEdge[] boundaryEdges;
        /// <summary>
        /// Граничные ребра оболочки.
        /// Построены на всем множестве точек оболочки <see cref="Points"/>.
        /// Если между опорными вершинами оболочки больше нет точек,
        /// тогда дублирует <see cref="BaseBoundaryEdges"/>
        /// </summary>
        public IHEdge[] BoundaryEdges => boundaryEdges;

        /// <summary>
        /// Прямоугольник, описанный около текущей ограниченной области
        /// </summary>
        public IHPoint[] OutRect => outRect;
        public IHPoint[] outRect;


        /// <summary>
        /// Инициализация оболочки
        /// </summary>
        /// <param name="baseVertexes">опорные вершины, образующие форму оболочки</param>
        /// <param name="generator">правила генерации точек на ребрах оболочки, между опорными вершинами</param>
        /// <exception cref="ArgumentNullException"></exception>
        public BoundaryNew(IHPoint[] baseVertexes, IGeneratorBase generator)
        {
            this.ID = BoundaryNew.uniqueIdCounter;
            //наращиваем счетчик для индексации границ
            BoundaryNew.uniqueIdCounter++;
            //проверка на null
            if (baseVertexes is null || baseVertexes.Length == 0)
                throw new ArgumentNullException($"{nameof(baseVertexes)} null или пуст");
            if (generator is null)
                throw new ArgumentNullException($"{nameof(generator)} не может быть null");

            this.baseVertexes = baseVertexes;
            //генерация точек
            this.points = generator.Generate(this);

            //инициализация описанного прямоугольника
            this.InitilizeRect();
            //сохраняем индексы вершин, образующих область
            InitializeVertexIds();
            // Инициализация граничных ребер
            InitializeBoundaryEdges();
        }

        #region Инициализация свойств оболочки
        /// <summary>
        /// Инициализация граничных ребер <see cref="_baseBoundaryEdges"/>, <see cref="boundaryEdges"/>
        /// </summary>
        protected void InitializeBoundaryEdges()
        {
            //выделение памяти для массива опорных ребер
            MEM.Alloc(BaseVertexes.Length, ref _baseBoundaryEdges);
            //выделение памяти для массива всех ребер
            MEM.Alloc(Points.Length, ref boundaryEdges);

            //индекс текущего опорного ребра
            int baseEdgeId = -1;
            //заполняем оба массива ребер
            for (int i = 0; i < Points.Length; i++)
            {
                //если достигнута следующая опорная вершина
                //делаем инкремент для индекса опорных вершин
                //и создаем новое опорное ребро с началом в baseEdgeId + 1
                IHPoint v1 = Points[i];
                IHPoint v2 = BaseVertexes[(baseEdgeId + 1) % BaseVertexes.Length];
                if (Math.Abs(v1.X - v2.X) < 1e-15 && Math.Abs(v1.Y - v2.Y) < 1e-15)
                //if (Points[i] == BaseVertexes[(baseEdgeId + 1) % BaseVertexes.Length])
                {
                    baseEdgeId += 1;
                    _baseBoundaryEdges[baseEdgeId] =
                        new HillEdgeDel(
                            baseEdgeId,
                            BaseVertexes[baseEdgeId % BaseVertexes.Length],
                            BaseVertexes[(baseEdgeId + 1) % BaseVertexes.Length]
                        );
                }

                //создаем обычное ребро, входящее в состав опорного ребра
                boundaryEdges[i] =
                    new HEdge(i, Points[i % Points.Length], Points[(i + 1) % Points.Length]);

                //наращиваем счетчик ребер у текущего опорного ребра
                //т.к. опорное ребро состоит из множества таких ребер
                _baseBoundaryEdges[baseEdgeId].Count += 1;
            }
        }


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

        //TODO убрать бы...
        /// <summary>
        /// Инициализация индексов вершин оболочки
        /// </summary>
        protected void InitializeVertexIds()
        {
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
        }
        #endregion
    }
}
