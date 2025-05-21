using CommonLib.Geometry;
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
        public BoundaryNew OuterBoundary { get => outerBoundary; }

        /// <summary>
        /// Внешняя оболочка
        /// </summary>
        protected BoundaryNew outerBoundary;

        /// <summary>
        /// Внутренние оболочки
        /// </summary>
        protected List<BoundaryNew> innerBoundaries = new List<BoundaryNew>();


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
                if (this.OuterBoundary is null)
                    throw new ArgumentNullException($"Внешняя оболочка не задана");

                //количество всех точек
                int pointsCnt = this.OuterBoundary.Points.Length +
                    innerBoundaries
                    .Sum(x => x.Points.Length);

                //заполняем массив точек
                allBoundaryPoints = new IHPoint[pointsCnt];
                //точки внешней оболочки
                outerBoundary.Points.CopyTo(allBoundaryPoints, 0);
                //точки внутренних оболочек
                if (this.innerBoundaries.Count > 0)
                {
                    //текущее смещение по точкам
                    //учитываем уже размещенные количество точек внешней оболочки
                    int pointsOffset = this.OuterBoundary.Points.Length;
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


        //TODO поправить индексацию, а также индексацию в методе GetBoundaryOffset
        public BoundaryNew this[int boundId]
        {
            get
            {
                if (boundId == 0)
                    return this.OuterBoundary;

                if (boundId - 1 > this.innerBoundaries.Count - 1)
                    throw new ArgumentException($"{nameof(boundId)} вышел за пределены");

                return this.innerBoundaries[boundId - 1];
            }
        }

        #region Добавление оболочек
        /// <summary>
        /// Добавить внутреннюю оболочку
        /// </summary>
        /// <param name="boundary">внутренняя оболочка</param>
        /// <exception cref="ArgumentNullException">boundary null</exception>
        public void AddInnerBoundary(BoundaryNew boundary)
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
            var boundary = new BoundaryNew(baseVertexes, generator);
            this.AddInnerBoundary(boundary);
        }


        /// <summary>
        /// Замена внешней оболочки
        /// </summary>
        /// <param name="boundary">внешняя оболочка</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ReplaceOuterBoundary(BoundaryNew boundary)
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
            var boundary = new BoundaryNew(baseVertexes, generator);
            this.ReplaceOuterBoundary(boundary);
        }
        #endregion

        //генератор
        public IEnumerator<BoundaryNew> GetEnumerator()
        {
            yield return this.outerBoundary;
            for (int i = 0; i < this.innerBoundaries.Count; i++)
            {
                yield return innerBoundaries[i];
            }
        }

        //TODO удалить/изменить
        public int Count => 1 + this.innerBoundaries.Count;

        
        /// <summary>
        /// Смещение по количеству узлов в общем массиве узлов для конкретной границы. <br/>
        /// Для первой границы смещение будет 0, для 2-ой границы смещение будет 0 + количество узлов в первой границе и т.д.
        /// </summary>
        /// <param name="boundId"></param>
        /// <returns></returns>
        public int GetBoundaryOffset(int boundId)
        {
            //смещение по внешней оболочке
            if (boundId == 0)
                return 0;

            //проверка на корректный индекс границы
            //для внутренних границ индексация начинается с 1
            if (1 + this.innerBoundaries.Count - 1 < boundId || boundId < 0)
                throw new ArgumentException($"{nameof(boundId)} вышел за пределы индексации.");

            //смещение по внешней оболочке
            int offset = this.OuterBoundary.Points.Length;
            //отсчитываем по внутренних оболочкам
            for (int i = 0; i < boundId-1; i++)
            {
                offset += this.innerBoundaries[i].Points.Length;
            }
            return offset;
        }
    }
}
