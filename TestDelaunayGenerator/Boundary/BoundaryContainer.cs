using CommonLib.Geometry;
using GeometryLib.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator.Boundary
{
    /// <summary>
    /// Контейнер для оболочек. Хранит и управляет внешней и внутренними оболочками
    /// </summary>
    public class BoundaryContainer
    {
        /// <summary>
        /// Внешняя оболочка
        /// </summary>
        public BoundaryHull OuterBoundary { get => outerBoundary; }

        /// <summary>
        /// Внешняя оболочка
        /// </summary>
        protected BoundaryHull outerBoundary;

        /// <summary>
        /// Внутренние оболочки
        /// </summary>
        protected List<BoundaryHull> innerBoundaries = new List<BoundaryHull>();

        /// <summary>
        /// Внутренние оболочки
        /// </summary>
        public List<BoundaryHull> InnerBoundaries => innerBoundaries;


        /// <summary>
        /// Все граничные точки
        /// </summary>
        protected IHPoint[] allBoundaryPoints;

        /// <summary>
        /// Все граничные точки
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public IHPoint[] AllBoundaryPoints
        {
            get
            {
                //если массив ранее был инициализирован, то возвращаем его
                if (this.allBoundaryPoints != null)
                    return this.allBoundaryPoints;

                //если внешней оболочки не задано, то поднимаем исключение
                if (this.OuterBoundary is null && this.innerBoundaries.Count == 0)
                    throw new ArgumentException($"Передан пустой контейнер с граничными контурами");

                //количество всех точек
                int pointsCnt = 0;
                if (this.OuterBoundary != null)
                    pointsCnt += this.OuterBoundary.Points.Length;
                pointsCnt += innerBoundaries.Sum(x => x.Points.Length);

                //заполняем массив точек
                allBoundaryPoints = new IHPoint[pointsCnt];
                //точки внешней оболочки
                if (this.OuterBoundary != null)
                    OuterBoundary.Points.CopyTo(allBoundaryPoints, 0);
                //точки внутренних оболочек
                if (this.innerBoundaries.Count > 0)
                {
                    //текущее смещение по точкам
                    //учитываем уже размещенные количество точек внешней оболочки
                    int pointsOffset = 0;
                    if (this.OuterBoundary != null)
                        pointsOffset += this.OuterBoundary.Points.Length;
                    foreach (var innerBound in innerBoundaries)
                    {
                        innerBound.Points.CopyTo(allBoundaryPoints, pointsOffset);
                        pointsOffset += innerBound.Points.Length;
                    }
                }

                //возврат массива точек
                return this.allBoundaryPoints;
            }
        }

        /// <summary>
        /// Индексатор по граничным оболочкам
        /// </summary>
        /// <param name="boundId">index=0 - внешняя оболочка (при наличии), остальные - внутренние (при наличии)</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">неверный index оболочки</exception>
        public BoundaryHull this[int boundId]
        {
            get
            {
                bool existOuter = this.OuterBoundary != null;

                //валидация
                if (existOuter)
                {
                    if (boundId - 1 > this.innerBoundaries.Count - 1)
                        throw new ArgumentException($"{nameof(boundId)} вышел за пределены");
                }
                else
                {
                    if (boundId > this.innerBoundaries.Count - 1)
                        throw new ArgumentException($"{nameof(boundId)} вышел за пределены");
                }

                if (boundId == 0)
                {
                    if (existOuter)
                        return this.OuterBoundary;
                    else
                        return this.InnerBoundaries[0];
                }

                if (existOuter)
                    return this.innerBoundaries[boundId - 1];
                else
                    return this.InnerBoundaries[boundId];
            }
        }

        #region Добавление оболочек
        /// <summary>
        /// Добавить внутреннюю оболочку
        /// </summary>
        /// <param name="boundary">внутренняя оболочка</param>
        /// <exception cref="ArgumentNullException">boundary null</exception>
        public void AddInnerBoundary(BoundaryHull boundary)
        {
            if (boundary is null)
                throw new ArgumentNullException($"{nameof(boundary)} не может быть null!");
            this.innerBoundaries.Add(boundary);
        }

        /// <summary>
        /// Добавить внутреннюю оболочку на основе опорных вершин
        /// </summary>
        /// <param name="baseVertexes">опорные вершины, образующие форму оболочки</param>
        /// <param name="generator">правила генерации точек на ребрах оболочки</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void AddInnerBoundary(IHPoint[] baseVertexes, IGeneratorBase generator)
        {
            //проверка на null
            if (baseVertexes is null || baseVertexes.Length == 0)
                throw new ArgumentNullException($"{nameof(baseVertexes)} null или пуст");
            if (generator is null)
                throw new ArgumentNullException($"{nameof(generator)} не может быть null");


            //создаем объект оболочки и добавляем в коллекцию через метод
            var boundary = new BoundaryHull(baseVertexes, generator);
            this.AddInnerBoundary(boundary);
        }


        /// <summary>
        /// Замена внешней оболочки
        /// </summary>
        /// <param name="boundary">внешняя оболочка</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ReplaceOuterBoundary(BoundaryHull boundary)
        {
            if (boundary is null)
                throw new ArgumentNullException($"{nameof(boundary)} не может быть null!");

            this.outerBoundary = boundary;
        }

        /// <summary>
        /// Замена внешней оболочки
        /// </summary>
        /// <param name="baseVertexes">опорные вершины, образующие форму оболочки</param>
        /// <param name="generator">правила генерации точек на ребрах оболочки</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ReplaceOuterBoundary(IHPoint[] baseVertexes, IGeneratorBase generator)
        {
            //проверка на null
            if (baseVertexes is null || baseVertexes.Length == 0)
                throw new ArgumentNullException($"{nameof(baseVertexes)} null или пуст");
            if (generator is null)
                throw new ArgumentNullException($"{nameof(generator)} не может быть null");

            //создаем объект оболочки и заменяем через метод
            var boundary = new BoundaryHull(baseVertexes, generator);
            this.ReplaceOuterBoundary(boundary);
        }
        #endregion

        //генератор
        public IEnumerator<BoundaryHull> GetEnumerator()
        {
            if (this.OuterBoundary != null)
                yield return this.OuterBoundary;
            for (int i = 0; i < this.innerBoundaries.Count; i++)
            {
                yield return innerBoundaries[i];
            }
        }

        /// <summary>
        /// Количество граничных оболочек, включая внешнюю и внутренние
        /// </summary>
        public int Count
        {
            get
            {
                if (this.OuterBoundary == null)
                {
                    return this.InnerBoundaries.Count;
                }
                return 1 + this.innerBoundaries.Count;
            }

        }
    }
}
