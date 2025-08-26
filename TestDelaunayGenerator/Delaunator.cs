using CommonLib;
using CommonLib.Geometry;
using GeometryLib;
using GeometryLib.Geometry;
using GeometryLib.Locators;
using MemLogLib;
using MeshLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TestDelaunayGenerator.Boundary;
using TestDelaunayGenerator.DCELMesh;
using TestDelaunayGenerator.SimpleStructures;
using TestDelaunayGenerator.Smoothing;

namespace TestDelaunayGenerator
{
    public class Delaunator
    {
        #region Базовые поля, свойства
        /// <summary>
        /// Узлы триангуляции <br/>
        /// </summary>
        IHPoint[] points;

        /// <summary>
        /// Узлы триангуляции <br/>
        /// </summary>
        public IHPoint[] Points => points;

        /// <summary>
        /// контейнер граничных оболочек
        /// </summary>
        BoundaryContainer boundaryContainer;

        /// <summary>
        /// контейнер граничных оболочек
        /// </summary>
        public BoundaryContainer BoundaryContainer => boundaryContainer;

        public DelaunatorConfig Config { get; set; }
        #endregion

        /// <summary>
        /// Построитель триангуляции Делоне.
        /// </summary>
        /// <param name="points">Множество точек триангуляции</param>
        /// <param name="boundaryContainer">контейнер границ.
        /// Не требуется объединять с <paramref name="points"/></param>
        /// <exception cref="ArgumentException"></exception>
        public Delaunator(IHPoint[] points, BoundaryContainer boundaryContainer = null, DelaunatorConfig config = null)
        {
            //валидация множества точек
            if (points is null || points.Length < 3)
                throw new ArgumentException($"{nameof(points)} должен содержать минимум 3 точки!");

            this.points = points;

            //валидация контейнера границ
            if (boundaryContainer != null && boundaryContainer.OuterBoundary is null)
                throw new ArgumentException(
                    "При инициализированном контейнере оболочек должна быть задана как минимум внешняя оболочка!");
            this.boundaryContainer = boundaryContainer;

            Config = config;
            if (Config is null)
                Config = new DelaunatorConfig();
        }

        #region Свойства, необходимые для генерации триангуляции по s-hull
        /// <summary>
        /// Центр триангуляции
        /// </summary>
        IHPoint pc;

        /// <summary>
        /// точки, формирующие начальную оболочку
        /// </summary>
        int i0, i1, i2;

        /// <summary>
        /// Массив значений координаты X множества узлов <see cref="points"/>
        /// </summary>
        double[] coordsX;

        /// <summary>
        /// Массив значений координаты Y множества узлов <see cref="points"/>
        /// </summary>
        double[] coordsY;

        /// <summary>
        /// индексы узлов из массива <see cref="points"/>,
        /// отсортированных по увеличению расстояния до центра <see cref="pc"/>
        /// </summary>
        int[] ids;

        /// <summary>
        /// Массив квадрат расстояний от центра триангуляции до точки,
        /// расположенной по такому же индексу в массиве <see cref="points"/>
        /// </summary>
        double[] dists;

        /// <summary>
        /// Массив индексов выпуклой оболочки данных против часовой стрелки
        /// вычисляется по hullNext по окончанию треангуляции
        /// </summary>
        int[] Hull;
        /// <summary>
        /// Выпуклая оболочка против ч.с.
        /// <br/>
        /// /// Индексация совпадает с <see cref="Points"/>,
        /// ячейки содержат вершины из <see cref="Points"/>.
        /// </summary>
        int[] hullPrev;
        /// <summary>
        /// Выпуклая оболочка по ч.с.
        /// <br/>
        /// Индексация совпадает с <see cref="Points"/>,
        /// ячейки содержат вершины из <see cref="Points"/>.
        /// Прим.: если значение ячейки совпадает с индексом, то вершина удалена из оболочки
        /// </summary>
        int[] hullNext;
        /// <summary>
        /// Массив индексов выпуклой оболочки данных против часовой стрелки.
        /// <br/>
        /// Индексация совпадает с <see cref="Points"/>,
        /// ячейки содержат полуребра, которые указывают на вершины из <see cref="Points"/> и
        /// образуют выпуклую оболочку
        /// </summary>
        int[] hullTri;
        /// <summary>
        /// условно нулевой узел входа в оболочку
        /// </summary>
        int hullStart;

        /// <summary>
        /// Количество узлов, образующих выпуклую оболочку
        /// </summary>
        int CountHullKnots;

        /// <summary>
        /// хэш-таблица для узлов выпулой оболочки, позволяет "быстро" по псевдо углу 
        /// добовляемого узла определять узел на ближайшей видимой грани оболочки, 
        /// необходимый для добавления в оболочку новых треугольников.
        /// Используется в <see cref="HashKey"/>
        /// </summary>
        int[] hullHash;
        /// <summary>
        /// Размерность хеш пространства.
        /// Используется в <see cref="HashKey"/>
        /// </summary>
        int hashSize;

        /// <summary>
        /// Ссылки индексов ребер треугольника на ребра сопряженных треугольников
        /// (или -1 для ребер на выпуклой оболочке). (Ребра диаграмы Вронского)
        /// </summary>
        public int[] HalfEdges;

        /// <summary>
        /// Размер стека для перестройки треугольников по Делоне.
        /// Используется в <see cref="Legalize"/>
        /// </summary>
        int[] EdgeStack;

        /// <summary>
        /// Счетчик вершин треугольников.
        /// Указывает на индекс следующей вершины, кратен 3.
        /// увеличение в <see cref="AddTriangle"/>
        /// </summary>
        int triangleVertexCounter;

        /// <summary>
        /// Массив троек вершин, образующих треугольник
        /// Обход вершин всех треугольников направлен против ч.с. <br/>
        /// flag = 20 - принадлежит области, 10 - не принадлежит области
        /// </summary>
        public Troika[] Triangles;

        /// <summary>
        /// Принадлежность точки области.
        /// Индексация совпадает с <see cref="points"/>
        /// </summary>
        public PointStatus[] pointStatuses;

        /// <summary>
        /// Граничные ребра.
        /// Индексация внутри <see cref="EdgePair"/> используется из <see cref="points"/>
        /// </summary>
        EdgePair[] boundaryEdges;
        #endregion


        #region Экспорт в объекты данных (сетка, DCEL, прочее)

        /// <summary>
        /// Экспорт в объект DCEL с ограничениями
        /// </summary>
        /// <returns></returns>
        public IRestrictedDCEL ToRestrictedDCEL(bool useActual = false)
        {
            //if (Config.IncludeExtTriangles)
            //    for (int i = 0; i < Triangles.Length; i++)
            //        this.Triangles[i].flag = TriangleState.Internal;

            IRestrictedDCEL dcel = null;
            if (!useActual)
                dcel = new RestrictedDCEL(
                    points,
                    this.HalfEdges,
                    this.pointStatuses,
                    this.Triangles,
                    this.boundaryEdges
                    );
            //используется в процессе дебага,
            //сохраняет существующие треугольники
            else
            {
                //кол-во треугольников
                int cnt = 0;
                for (int i = 0; i < Triangles.Length; i++)
                {

                    var tr = Triangles[i];
                    if (tr.i == 0 && tr.j == 0 && tr.k == 0)
                    {
                        cnt = i;
                        break;
                    }

                }
                return this.ToRestrictedDCEL(cnt);
            }
            return dcel;
        }

        public IRestrictedDCEL ToRestrictedDCEL(int trCnt)
        {
            //if (IncludeExternal)
            for (int i = 0; i < Triangles.Length; i++)
                this.Triangles[i].flag = TriangleState.Internal;

            int[] hes = HalfEdges.Take(trCnt * 3).ToArray();
            var faces = Triangles.Take(trCnt).ToArray();

            IRestrictedDCEL dcel = new RestrictedDCEL(
                points,
                hes,
                this.pointStatuses,
                faces,
                this.boundaryEdges
                );
            return dcel;
        }

        /// <summary>
        /// Выводит массивы размером 3 * количество треугольников. <br/>
        /// Поля:
        /// вхождение треугольника в область,
        /// id треугольника (одинаковые значения идут тройками),
        /// index в разрезе троек вершин,
        /// halfEdge,
        /// triangles (тройки вершин, образующие треугольники),
        /// point_status (принадлежность области)
        /// </summary>
        public DebugDelaunay ToDebug()
        {
            //вхождение треугольника в область
            TriangleState[] triangleInfects = new TriangleState[Triangles.Length * 3];
            //id треугольника
            int[] triangleIds = new int[Triangles.Length * 3];
            //index в разрезе троек вершин
            int[] indexes = new int[Triangles.Length * 3];
            //triangles (тройки вершин, образующие треугольники),
            int[] triangleVertexes = new int[Triangles.Length * 3];
            PointStatus[] pointStatuses = new PointStatus[Triangles.Length * 3];

            //заполнение
            for (int i = 0; i < Triangles.Length * 3; i++)
            {
                triangleInfects[i] = Triangles[i / 3].flag;
                triangleIds[i] = i / 3;
                indexes[i] = i;
                triangleVertexes[i] = Triangles[i / 3][i % 3];
                pointStatuses[i] = this.pointStatuses[Triangles[i / 3][i % 3]];
            }

            DebugDelaunay debugDelaunay = new DebugDelaunay(
                triangleInfects,
                triangleIds,
                indexes,
                HalfEdges,
                triangleVertexes,
                pointStatuses
            );
            return debugDelaunay;
        }

        public TriMesh ToMesh()
        {
            IRestrictedDCEL restrictedDcel = ToRestrictedDCEL();
            return restrictedDcel.ToDcelTriMesh();
        }
        #endregion


        /// <summary>
        /// Генерация триангуляции
        /// </summary>
        public void Generate()
        {
            //определение центра триангуляции (временного)
            pc = new HPoint(
                points.Sum(p => p.X) / points.Length,
                points.Sum(p => p.Y) / points.Length
                );

            if (this.boundaryContainer != null)
            {
                //выделение памяти принадлежностей точек
                //по умолчанию точки входят в область
                MEM.Alloc(
                    points.Length + this.boundaryContainer.AllBoundaryPoints.Length,
                    ref pointStatuses,
                    PointStatus.Internal);

                int pointCnt = Points.Length;
                //отсечение точек
                if (Config.UseClippingPoints)
                    pointCnt = this.ClippingPoints();
                //объединение с множеством граничных точек
                CombinePointSets(pointCnt);

                //если не совпадает с размером массива точек, то обрезаем массив до нужного размера
                if (this.points.Length != pointStatuses.Length)
                    Array.Resize(ref pointStatuses, points.Length);
            }
            else
            {
                MEM.Alloc(points.Length, ref pointStatuses, PointStatus.Internal);
            }


            //выделение памяти для массива точек и его заполнение
            MEM.Alloc(points.Length, ref coordsX);
            MEM.Alloc(points.Length, ref coordsY);
            for (int i = 0; i < points.Length; i++)
            {
                coordsX[i] = points[i].X;
                coordsY[i] = points[i].Y;
            }

            //выделение памяти для начального состояния адресации верншин
            //заполнение массива
            MEM.Alloc(points.Length, ref ids);
            for (int i = 0; i < points.Length; i++)
                ids[i] = i;


            #region поиск начального треугольника
            //минимальное расстояние до проверяемого узла
            double minDist = double.PositiveInfinity;

            //поиск первой точки, ближайшей к центру области
            for (int i = 0; i < points.Length; i++)
            {
                double dist = DistanceSquare(pc, points[i]);
                //если текущая точка ближе к центру триангуляции,
                //то сохраняем её
                if (dist < minDist)
                {
                    this.i0 = i;
                    minDist = dist;
                }
            }

            //поиск второй точки, которая будет ближайшей к первой точке
            minDist = double.PositiveInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                //пропуск первой точки, определенной ранее
                if (i == i0)
                    continue;

                double dist = DistanceSquare(i0, i);
                if (dist < minDist && dist > 0)
                {
                    i1 = i;
                    minDist = dist;
                }
            }

            // поиск третьей точки, которая образует
            // наименьшую окружность с первыми двумя точками
            double minRadius = double.PositiveInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                //пропуск найденных точек
                if (i == i0 || i == i1)
                    continue;

                //расчет квадрата радиуса окружности, проходящей через 3 точки
                double radius = CircumRadiusSquare(i, i0, i1);

                if (radius < minRadius)
                {
                    i2 = i;
                    minRadius = radius;
                }
            }

            //Проверка на наличие трех точек
            if (minRadius == double.PositiveInfinity)
                throw new ArgumentException("Для этих входных данных не существует триангуляции Делоне!");
            #endregion

            //ориентация вершин начальной оболочки (треугольника)
            if (Orient(i0, i1, i2) is true)
            {
                int i = i1;
                i1 = i2;
                i2 = i;
            }

            //пересчет центра триангуляции, как центр окружности трех найденных точек
            this.pc = CircumCenter(i0, i1, i2);

            //выделение памяти массива квадратов расстояний
            //расчет расстояний от центра области до каждой из точек в области
            MEM.Alloc(points.Length, ref dists);
            for (int i = 0; i < points.Length; i++)
                dists[i] = DistanceSquare(pc, points[i]);

            //быстрая сортировка точек по расстоянию
            //от центра окружности начального треугольника
            Quicksort(ids, dists, 0, points.Length - 1);

            //выделяем память для вспомогательных массивов
            const int none = -99;
            int maxTriangles = 2 * points.Length - 5;
            MEM.Alloc(maxTriangles * 3, ref HalfEdges, none);
            MEM.Alloc(points.Length, ref hullPrev);
            MEM.Alloc(points.Length, ref hullNext);
            MEM.Alloc(points.Length, ref hullTri, none);

            //вычисление размера хеш-пространства
            //и выделение памяти
            hashSize = (int)Math.Ceiling(Math.Sqrt(points.Length));
            MEM.Alloc(hashSize, ref hullHash);

            //выделение памяти под массив троек индексов,
            //образующих треугольники
            MEM.Alloc(maxTriangles, ref Triangles);

            #region начальная оболочка из первого треугольника
            // стартовый условно нулевой узел входа в оболочку
            hullStart = i0;
            CountHullKnots = 3;
            hullNext[i0] = i1;
            hullNext[i1] = i2;
            hullNext[i2] = i0;

            hullPrev[i2] = i1;
            hullPrev[i0] = i2;
            hullPrev[i1] = i0;

            hullTri[i0] = 0;
            hullTri[i1] = 1;
            hullTri[i2] = 2;

            hullHash[HashKey(i0)] = i0;
            hullHash[HashKey(i1)] = i1;
            hullHash[HashKey(i2)] = i2;
            // счетчик треугольников
            triangleVertexCounter = 0;

            // Добавление 1 треугольника в список треугольников
            int trid = AddTriangle(i0, i1, i2, -1, -1, -1) / 3;
            //определение принадлежности первого треугольника
            //TODO разобраться с этой хуйней
            if (boundaryContainer != null)
            {

                if (!Config.RestoreBorder)
                {
                    InitializeExternalPoint();
                    var inArea = IsTriangleInArea(0);
                    var status = TriangleState.Internal;
                    if (!inArea)
                        status = TriangleState.External;
                    Triangles[0].flag = status;
                }
            }
            else
            {
                Triangles[0].flag = TriangleState.Internal;
            }
                #endregion

                //выделение памяти для массива стека перестроения
                MEM.Alloc((int)Math.Sqrt(points.Length), ref EdgeStack);
            #region Поиск выпуклой оболочки и триангуляции

            //проход по всем узлам оболочки, за исключением тех, что уже в ней,
            //т.е. первых трех
            for (int k = 0; k < ids.Length; k++)
            {
                int vid = ids[k];

                //ближайший узел к текущему на выпуклой оболочке
                int start = 0;

                //поиск ближайшего узла не видимой части выпуклой оболочки
                for (int j = 0; j < hashSize; j++)
                {
                    int key = HashKey(vid);
                    start = hullHash[(key + j) % hashSize];
                    if (start != -1 && start != hullNext[start])
                        break;
                }
                start = hullPrev[start];
                //вершины
                int e = start;
                int q = hullNext[e];

                // проверка видимости найденного стартового узла и возможности
                // построения новых треугольников на оболочке
                //true - грань видима для добавляемой точки
                while (Orient(vid, e, q) == false)
                {
                    e = q;
                    if (e == start)
                    {
                        //плохой узел
                        e = int.MaxValue;
                        break;
                    }
                    q = hullNext[e];
                }

                // скорее всего, это почти повторяющаяся точка; пропустите ее
                if (e == int.MaxValue)
                    continue;
                // если e - hullNext[e] - на видимой границе оболочки
                //  добавьте первый треугольник от точки i
                //    hullTri[e]
                //        |
                // -- e ---- hullNext[e] ---
                //     \       /
                //  -1  \     / -1
                //       \   /
                //         i

                //индекс первой вершины треугольника в массиве треугольников
                int heStart = AddTriangle(e, vid, hullNext[e], -1, -1, hullTri[e]);

                // рекурсивная перестройки треугольников от точки к точке,
                // пока они не удовлетворят условию Делоне
                hullTri[vid] = Legalize(heStart + 2);
                // добавление треугольника в оболочку
                hullTri[e] = heStart;
                CountHullKnots++;

                // пройдите вперед по оболочке,
                // добавляя треугольники и переворачивая их рекурсивно
                int nextW = hullNext[e];
                int nextE = hullNext[nextW];

                //достраиваем треугольники к оболочке, пока контур невыпуклый
                while (Orient(vid, nextW, nextE) == true)
                {
                    // если nextW - hullNext[nextW] - на видимой границе оболочки
                    //  добавьте первый треугольник от точки i
                    //
                    //                 hullTri[nextW]
                    //                     |
                    //       ---- nextW ----- hullNext[nextW] --->
                    //               \         /
                    //    hullTri[i]  \       / -1
                    //                 \     /
                    //                  \   /
                    //                    i    
                    // добавить треугольник 
                    heStart = AddTriangle(
                        nextW, vid, nextE, hullTri[vid], -1, hullTri[nextW]);
                    //  проверка и перестройка по Делоне
                    hullTri[vid] = Legalize(heStart + 2);
                    // пометить как удаленный узел ущедщий из оболочки
                    hullNext[nextW] = nextW;
                    CountHullKnots--;
                    // следующее ребро оболочки (по ч.с.)
                    nextW = nextE;
                    nextE = hullNext[nextW];
                }

                //достраиваем оболочку при обходе в обратную сторону
                //так же, пока контур не станет выпуклым
                // пройдите назад с другой стороны,
                int prewE = e;
                if (prewE == start)
                {
                    int prewW = hullPrev[prewE];
                    while (Orient(vid, prewW, prewE) == true)
                    {
                        //  если prewW  - prewE - на видимой границе оболочки
                        //  добавьте первый треугольник от точки i
                        //
                        //                 hullTri[prewW]
                        //                     |
                        //       ----  nextW -----  prewE ---
                        //               \         /
                        //            -1  \       / hullTri[prewE]
                        //                 \     /
                        //                  \   /
                        //                    i    
                        // добавить треугольник 
                        heStart = AddTriangle(prewW, vid, prewE, -1, hullTri[prewE], hullTri[prewW]);
                        //  проверка и перестройка по Делоне
                        Legalize(heStart + 2);
                        hullTri[prewW] = heStart;
                        // пометить как удаленный узел ущедщий из оболочки
                        hullNext[prewE] = prewE;
                        CountHullKnots--;
                        // следующее ребро оболочки
                        prewE = prewW;
                        prewW = hullPrev[prewE];
                    }
                }
                // пометить как удаленный
                //связываем вершины выпуклой оболочки между собой
                hullStart = hullPrev[vid] = prewE;
                hullNext[prewE] = hullPrev[nextW] = vid;
                hullNext[vid] = nextW;
                // сохраните два новых ребра в хэш-таблице
                hullHash[HashKey(vid)] = vid;
                hullHash[HashKey(prewE)] = prewE;
            }

            //создаем массив граничных узлов выпуклой оболочки
            Hull = GetConvexHull;
            //удаление ссылок на временные массивы
            hullPrev = hullNext = hullTri = null;
            #endregion

            //обрезка триангуляционных массивов
            HalfEdges = HalfEdges.Take(triangleVertexCounter).ToArray();
            Triangles = Triangles.Take(triangleVertexCounter / 3).ToArray();

            //граничная оболочка не задана
            if (this.boundaryContainer is null)
            {
                this.boundaryEdges = new EdgePair[points.Length];

                //отмечаем граничные ребра
                //отмечаем точки, формирующие оболочку граничными
                for (int i = 0; i < Hull.Length; i++)
                {
                    //id вершины
                    int vid = Hull[i % Hull.Length];
                    pointStatuses[vid] = PointStatus.Boundary;

                    //id соседних вершин
                    int prevVid = Hull[(Hull.Length - 1 + i) % Hull.Length];
                    int nextVid = Hull[(i + 1) % Hull.Length];
                    this.boundaryEdges[vid] = new EdgePair(vid, prevVid, nextVid, 0);
                }
            }

            if (Config.RestoreBorder)
            {
                RestoreBorder();
                ClippingTriangles();

            }
            //удаление связей с внешними треугольниками в полуребрах
            if (!Config.IncludeExtTriangles)
                ErraseExternalTriangles();
        }

        /// <summary>
        /// текущая выпуклая оболочка, заданная по ч.с.
        /// </summary>
        int[] GetConvexHull
        {
            get
            {
                int[] currentHull = new int[CountHullKnots];
                int s = hullStart;
                for (int i = 0; i < CountHullKnots; i++)
                {
                    currentHull[i] = s;
                    s = hullNext[s];
                }
                return currentHull;
            }
        }

        #region Логика генерации триангуляции делоне по S-hull
        /// <summary>
        /// Рекурсивная перестройка треугольников сетки до выполнения условия Делоне
        /// </summary>
        /// <param name="a">проверяемое полуребро, которое мб перестроено (flip)</param>
        /// <returns></returns>
        private int Legalize(int a)
        {
            var i = 0;
            int ar;

            //рекурсия устранена стеком фикс размера
            while (true)
            {
                var b = HalfEdges[a];

                int tridA = a / 3;
                int tridB = b / 3;

                /* фактический обход по полуребрам не совпадает (при просмотре по внутреннему контуру)
                 * совпадают полуребер относительно связанных вершин, т.е. al связывает p0 и pl b и т.п.
                 *           pl                    pl
                 *          /||\                  /  \
                 *       al/ || \bl            al/    \a
                 *        /  ||  \              /      \
                 *       /  a||b  \    flip    /___ar___\
                 *     p0\   ||   /p1   =>   p0\---bl---/p1
                 *        \  ||  /              \      /
                 *       ar\ || /br             b\    /br
                 *          \||/                  \  /
                 *           pr                    pr
                 */
                int a0 = a - a % 3;
                ar = a0 + (a + 2) % 3;

                if (b == -1)
                {
                    //ребро выпуклой оболочки
                    if (i == 0) break;
                    a = EdgeStack[--i];
                    continue;
                }

                //если задан граничный контур, то избегаем легализации для ребер, входящих в него
                if (boundaryContainer != null)
                {
                    //ребро, которое будет развернуто
                    int edgeStart = HalfEdgeUtils.Origin(Triangles, a);
                    int edgeEnd = HalfEdgeUtils.Origin(Triangles, b);

                    //смежное ребро между треугольниками является граничным
                    if (pointStatuses[edgeStart] == PointStatus.Boundary &&
                         pointStatuses[edgeEnd] == PointStatus.Boundary &&
                         boundaryEdges[edgeStart].Adjacents.Contains(edgeEnd))
                    {
#if DEBUG
                        Log.Debug($"Легализация пропущена {nameof(tridA)}:{tridA} {nameof(tridB)}:{tridB}; " +
                            $"ребро ({edgeStart}-{edgeEnd}) - граничное");
#endif
                        if (!Config.RestoreBorder && !Config.IncludeExtTriangles)
                        {
                            var trStatus = TriangleState.External;
                            if (Triangles[tridB].flag == trStatus)
                                trStatus = TriangleState.Internal;
                            Triangles[tridA].flag = trStatus;
                        }

                        //берем следующий из стека
                        if (i == 0)
                            break;
                        a = EdgeStack[--i];
                        continue;
                    }
                    //если отключено восстановление границы,
                    //то используем отсечение треугольников в процессе построения триангуляции;
                    //также нужно, чтобы внешние треугольники не входили в триангуляцию
                    else if (!Config.RestoreBorder && !Config.IncludeExtTriangles)
                    {
                        Triangles[tridA].flag = Triangles[tridB].flag;
                    }
                }
                else
                {
                    Triangles[tridA].flag = TriangleState.Internal;
                }

                var b0 = b - b % 3;
                var al = a0 + (a + 1) % 3;
                var bl = b0 + (b + 2) % 3;
                var br = b0 + (b + 1) % 3;

                var p0 = HalfEdgeUtils.Origin(Triangles, ar);
                var pr = HalfEdgeUtils.Origin(Triangles, a);
                var pl = HalfEdgeUtils.Origin(Triangles, al);
                var p1 = HalfEdgeUtils.Origin(Triangles, bl);

                var illegal = InCircle(p0, pr, pl, p1);

                if (illegal)
                {
                    Triangles[a / 3][a % 3] = p1;
                    Triangles[b / 3][b % 3] = p0;

                    var hbl = HalfEdges[bl];

                    // ребро перевернуто в паре треугольников на обратной стороне оболочки (редко);
                    // поправить ссылку полуребер
                    if (hbl == -1)
                    {
                        var e = hullStart;
                        do
                        {
                            if (hullTri[e] == bl)
                            {
                                hullTri[e] = a;
                                break;
                            }
                            e = hullPrev[e];
                        } while (e != hullStart);
                    }
                    Link(a, hbl);
                    Link(b, HalfEdges[ar]);
                    Link(ar, bl);



                    //переполнение стека возможно только при вырожденном случае (напр. регулярная сетка)
                    if (i < EdgeStack.Length)
                    {
                        EdgeStack[i++] = br;
                    }
                    else
                    {
                        Log.Warning("Переполнение стека при проверке Делоне" +
                            " для добавленных треугольников!");
                        break;
                    }
                }
                else
                {
                    if (i == 0) break;
                    a = EdgeStack[--i];
                }
            }

            return ar;
        }

        /// <summary>
        /// Рассчитать квадрат расстояния между двумя точками
        /// </summary>
        /// <param name="i">id точки из <see cref="points"/></param>
        /// <param name="j">id точки из <see cref="points"/></param>
        /// <returns></returns>
        double DistanceSquare(int i, int j)
        {
            var dx = points[i].X - points[j].X;
            var dy = points[i].Y - points[j].Y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Рассчитать квадрат расстояния между двумя точками
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        double DistanceSquare(IHPoint p1, IHPoint p2)
        {
            var dx = p1.X - p2.X;
            var dy = p1.Y - p2.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Определить знак векторного произведения, построенного на касательных к двум граням.
        /// Используется для проверки угла на выпуклость
        /// </summary>
        /// <param name="i"></param>
        /// <param name="q"></param>
        /// <param name="r"></param>
        /// <returns>true - угол выпуклый (больше 180), иначе false 1</returns>
        bool Orient(int i, int q, int r)
        {
            return (coordsY[q] - coordsY[i]) * (coordsX[r] - coordsX[q]) -
                 (coordsX[q] - coordsX[i]) * (coordsY[r] - coordsY[q]) < 0;
        }

        #region Работа с окружностью
        /// <summary>
        /// принадлежность узла кругу проведенному через три точки
        /// </summary>
        /// <param name="i">V1</param>
        /// <param name="j">V2</param>
        /// <param name="k">V3</param>
        /// <param name="n">проверяемый узел, не должен входить в окружность</param>
        /// <returns></returns>
        private bool InCircle(int i, int j, int k, int n)
        {
            var dx = coordsX[i] - coordsX[n];
            var dy = coordsY[i] - coordsY[n];
            var ex = coordsX[j] - coordsX[n];
            var ey = coordsY[j] - coordsY[n];
            var fx = coordsX[k] - coordsX[n];
            var fy = coordsY[k] - coordsY[n];

            var ap = dx * dx + dy * dy;
            var bp = ex * ex + ey * ey;
            var cp = fx * fx + fy * fy;

            return dx * (ey * cp - bp * fy) -
                   dy * (ex * cp - bp * fx) +
                   ap * (ex * fy - ey * fx) < 0;
        }

        /// <summary>
        /// Определить центр окружности, проходящей через 3 точки
        /// </summary>
        /// <param name="i0"></param>
        /// <param name="i1"></param>
        /// <param name="i2"></param>
        /// <returns></returns>
        IHPoint CircumCenter(int i0, int i1, int i2)
        {
            //координаты вершин начального треугольника
            double ax = coordsX[i0];
            double ay = coordsY[i0];
            double dx = coordsX[i1] - coordsX[i0];
            double dy = coordsY[i1] - coordsY[i0];
            double ex = coordsX[i2] - coordsX[i0];
            double ey = coordsY[i2] - coordsY[i0];

            //расчет центра описанной окружности
            double bl = dx * dx + dy * dy;
            double cl = ex * ex + ey * ey;
            double d = 0.5 / (dx * ey - dy * ex);
            double x = ax + (ey * bl - dy * cl) * d;
            double y = ay + (dx * cl - ex * bl) * d;
            return new HPoint(x, y);
        }

        /// <summary>
        /// определение квадрата радиуса окружности проходящей через 3 точки
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private double CircumRadiusSquare(int i, int j, int k)
        {
            double dx = coordsX[j] - coordsX[k];
            double dy = coordsY[j] - coordsY[k];
            double ex = coordsX[i] - coordsX[k];
            double ey = coordsY[i] - coordsY[k];
            double bl = dx * dx + dy * dy;
            double cl = ex * ex + ey * ey;
            double d = 0.5 / (dx * ey - dy * ex);
            double x = (ey * bl - dy * cl) * d;
            double y = (dx * cl - ex * bl) * d;
            return x * x + y * y;
        }
        #endregion

        /// <summary>
        /// Добавление треугольника в список треугольников
        /// <see cref="Triangles"/>
        /// </summary>
        /// <param name="i0">индекс вершины 0</param>
        /// <param name="i1">индекс вершины 1</param>
        /// <param name="i2">индекс вершины 2</param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns>возвращает индекс вершины в <see cref="Triangles"/>. 
        /// Первая вершина нового треугольника.
        /// Индексация совпадает с <see cref="HalfEdges"/></returns>
        private int AddTriangle(int i0, int i1, int i2, int a, int b, int c)
        {
            //индекс треугольника
            int trid = triangleVertexCounter / 3;
            Triangles[trid].i = i0;
            Triangles[trid].j = i1;
            Triangles[trid].k = i2;
            if (Config.IncludeExtTriangles)
                Triangles[trid].flag = TriangleState.Internal;

            //индекс первой вершины, в крайнем треугольнике
            //относительно массива точек
            int triangleIndex = triangleVertexCounter;

            Link(triangleIndex, a);
            Link(triangleIndex + 1, b);
            Link(triangleIndex + 2, c);

            triangleVertexCounter += 3;

            return triangleIndex;
        }

        /// <summary>
        /// Связать 2 полуребра <see cref="HalfEdges"/>
        /// </summary>
        /// <param name="EdgesID"></param>
        /// <param name="b"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Link(int EdgesID, int b)
        {
            HalfEdges[EdgesID] = b;
            if (b != -1)
            {
                HalfEdges[b] = EdgesID;
            }
        }

        #region Хеширование
        /// <summary>
        /// Получение хеш индекса через псевдо угол точки относительно 
        /// начального центра триангуляции
        /// </summary>
        /// <param name="idx">индекс точки в исходном массиве</param>
        /// <returns></returns>
        int HashKey(int idx)
        {
            //разность координат между текущей точкой и центром триангуляции требуется для того,
            //чтобы принять центр триангуляции за центр координат
            return (int)(PseudoAngle(coordsX[idx] - pc.X,
                coordsY[idx] - pc.Y) * hashSize) % hashSize;
        }

        /// <summary>
        /// Вычисление псевдо угола точки 
        /// </summary>
        /// <param name="dx">отклонение точки от центра координат по оси Х</param>
        /// <param name="dy">отклонение точки от центра координат по оси Y</param>
        /// <returns>псевно угол (упрощенная альтернатива полярному углу)</returns>
        static double PseudoAngle(double dx, double dy)
        {
            var p = dx / (Math.Abs(dx) + Math.Abs(dy));
            return (dy > 0 ? 3 - p : 1 + p) / 4; // [0..1]
        }
        #endregion

        #region Сортировка точек по расстоянию от центра

        /// <summary>
        /// быстрая сортировка точек по расстоянию от центра окружности исходного треугольника
        /// </summary>
        /// <param name="ids">индексы сортируемых точек</param>
        /// <param name="dists">расстояния от центра до сортируемой точки</param>
        /// <param name="left">начальный номер узла сортируемых массивов</param>
        /// <param name="right">конечный номер узла сортируемых массивов</param>
        static void Quicksort(int[] ids, double[] dists, int left, int right)
        {
            if (right - left <= 20)
            {
                for (var i = left + 1; i <= right; i++)
                {
                    var temp = ids[i];
                    var tempDist = dists[temp];
                    var j = i - 1;
                    while (j >= left && dists[ids[j]] > tempDist) ids[j + 1] = ids[j--];
                    ids[j + 1] = temp;
                }
            }
            else
            {
                var median = (left + right) >> 1;
                var i = left + 1;
                var j = right;
                Swap(ids, median, i);
                if (dists[ids[left]] > dists[ids[right]]) Swap(ids, left, right);
                if (dists[ids[i]] > dists[ids[right]]) Swap(ids, i, right);
                if (dists[ids[left]] > dists[ids[i]]) Swap(ids, left, i);

                var temp = ids[i];
                var tempDist = dists[temp];
                while (true)
                {
                    do i++; while (dists[ids[i]] < tempDist);
                    do j--; while (dists[ids[j]] > tempDist);
                    if (j < i) break;
                    Swap(ids, i, j);
                }
                ids[left + 1] = ids[j];
                ids[j] = temp;

                if (right - i + 1 >= j - left)
                {
                    Quicksort(ids, dists, i, right);
                    Quicksort(ids, dists, left, j - 1);
                }
                else
                {
                    Quicksort(ids, dists, left, j - 1);
                    Quicksort(ids, dists, i, right);
                }
            }
        }
        /// <summary>
        /// Поменять местами элементы в массиве (сделать свап, смену)
        /// </summary>
        /// <param name="arr">массив с элементами</param>
        /// <param name="i">индекс 1 элемента</param>
        /// <param name="j">индекс 2 элемента</param>
        static void Swap(int[] arr, int i, int j)
        {
            var tmp = arr[i];
            arr[i] = arr[j];
            arr[j] = tmp;
        }
        #endregion

        #endregion

        /// <summary>
        /// Внешняя точка по отношению к области триангуляции,
        /// в частности ко внешней оболочке
        /// </summary>
        IHPoint externalPoint;
        /// <summary>
        /// Инициализация внешней точки <see cref="externalPoint"/>
        /// </summary>
        void InitializeExternalPoint()
        {
            //если точка уже инициализирована, то выходим
            if (externalPoint != null)
                return;

            //гарантированно внешняя точка
            var maxX = points.Max(x => x.X);
            externalPoint = new HPoint(maxX * 1.1, pc.Y);
        }


        /// <summary>
        /// Удалить связи с треугольником. По сути очистить его из <see cref="HalfEdges"/>
        /// </summary>
        /// <param name="trid"></param>
        void UnLinkTriangle(int trid)
        {
            //проходим по его полуребрам
            for (int he = trid * 3; he < trid * 3 + 3; he++)
            {
                //пара к этому полуребру
                int twinHe = HalfEdges[he];
                UnLink(he, twinHe);
            }
        }

        /// <summary>
        /// Разорвать связь между 2 полуребрами
        /// </summary>
        /// <param name="he1"></param>
        /// <param name="he2"></param>
        void UnLink(int he1, int he2)
        {
            if (he1 != -1)
                this.HalfEdges[he1] = -1;
            if (he2 != -1)
                this.HalfEdges[he2] = -1;
        }

        //TODO добавить очистку внешних треугольников
        /// <summary>
        /// Разорвать связи в полуребрах с внешними треугольниками
        /// </summary>
        /// <param name="removeExternalTriangles">true - очистка внешних треугольников</param>
        void ErraseExternalTriangles(bool removeExternalTriangles = false)
        {
            //удаление связей с внешними треугольниками
            for (int i = 0; i < Triangles.Length; i++)
            {
                if (Triangles[i].flag == TriangleState.External)
                    UnLinkTriangle(i);
            }
        }

        /// <summary>
        /// Отсечение точек <see cref="points"/>.
        /// Массив <see cref="points"/> расширяется засчет <see cref="boundaryContainer"/>,
        /// если такой определен
        /// </summary>
        /// <exception cref="ArgumentNullException">не задана внешняя оболочка</exception>
        /// <returns>Фактический размер <see cref="points"/></returns>
        int ClippingPoints()
        {
            if (this.boundaryContainer is null)
                throw new ArgumentNullException($"{nameof(boundaryContainer)} не должен быть null!");

            InitializeExternalPoint();

            //количество точек, входящих в область
            int inAreaPointCnt = 0;
            //определение принадлежности точек области


            //отсечение точек в параллель
            if (Config.ParallelClippingPoints)
                Parallel.For(
                    0, points.Length, (i, loopState) =>
                    {
                        bool isInArea = IsInArea(points[i]);
                        //устанавливаем текущую точку, как входящую в область marker == 1
                        if (isInArea)
                        {
                            pointStatuses[i] = PointStatus.Internal;
                            //требуется для корректного результата в рамках "гонки потоков"
                            Interlocked.Increment(ref inAreaPointCnt);
                        }
                        //точка не граничная
                        else
                        {
                            pointStatuses[i] = PointStatus.External;
                        }
                    }
                );
            else
                #region Однопоточное отсечение точек. Удобно для дебага
                for (int i = 0; i < points.Length; i++)
                {
                    bool isInArea = IsInArea(points[i]);
                    //устанавливаем текущую точку, как входящую в область marker == 1
                    if (isInArea)
                    {
                        pointStatuses[i] = PointStatus.Internal;
                        //требуется для корректного результата в рамках "гонки потоков"
                        Interlocked.Increment(ref inAreaPointCnt);
                    }
                    //точка не граничная
                    else
                    {
                        pointStatuses[i] = PointStatus.External;
                    }
                }
            #endregion

            //текущий индекс для перезаписи в массиве
            int curVid = 0;
            //оставляем в массиве только точки, входящие в область
            for (int i = 0; i < points.Length; i++)
            {
                if (pointStatuses[i] == PointStatus.Internal)
                {
                    points[curVid] = points[i];
                    pointStatuses[curVid] = pointStatuses[i];
                    curVid++;
                }
            }

            return inAreaPointCnt;
        }

        /// <summary>
        /// Объединить точки из <see cref="points"/> с точками из <see cref="boundaryContainer"/>.
        /// Усекает массив до размера суммы точек из этих множеств.
        /// </summary>
        /// <param name="notBorderPointCnt">
        /// количество точек, которое необходимо взять из <see cref="points"/> от начала массива
        /// </param>
        /// <exception cref="ArgumentNullException"></exception>
        void CombinePointSets(int notBorderPointCnt)
        {
            if (this.boundaryContainer is null)
                throw new ArgumentNullException($"{nameof(boundaryContainer)} не должен быть null!");

            //Изменяем размер массива до количества точек, входящих в область + граничных точек
            Array.Resize(ref points, notBorderPointCnt + boundaryContainer.AllBoundaryPoints.Length);
            //количество ребер совпадает с количеством точек
            MEM.Alloc(this.points.Length, ref boundaryEdges);

            //текущий свободный индекс для записи
            int curVid = notBorderPointCnt;
            //смещение по количеству точек до граничных точек
            int offset = curVid;
            //проход по каждой оболочке
            for (int boundId = 0; boundId < boundaryContainer.Count; boundId++)
            {
                //количество точек на текущем контуре
                int bndPointCnt = boundaryContainer[boundId].Points.Length;
                //проход по точкам внутри оболочки
                for (int i = 0; i < bndPointCnt; i++)
                {
                    //копируем граничную точку в общий массив точек
                    points[curVid] = boundaryContainer[boundId].Points[i];
                    pointStatuses[curVid] = PointStatus.Boundary;

                    //сосед 1
                    int leftNeighId = offset + (bndPointCnt + (curVid - offset) - 1) % bndPointCnt;
                    //сосед 2
                    int rightNeighId = offset + ((curVid - offset) + 1) % bndPointCnt;
                    //соседние точки для текущей точки
                    boundaryEdges[curVid] = new EdgePair(
                        curVid, //ID текущей точки
                        leftNeighId,
                        rightNeighId,
                        boundaryContainer[boundId].ID
                        );

                    curVid++;
                }
                //при переходе к следующему контуру
                //учитываем смещение по количеству точек в текущем контуре
                offset += boundaryContainer[boundId].Points.Length;
            }
        }


        #region Логика отсечения точек
        /// <summary>
        /// Определение принадлежности точки области
        /// </summary>
        /// <param name="point">точка, принадлежность которой требуется определить</param>
        /// <returns>true - точка входит в область, иначе false</returns>
        bool IsInArea(IHPoint point)
        {
            //количество пересечений с границей/оболочками
            int crossCount = 0;

            //проверка вхождения в прямоугольник, описанный около
            //внешней оболочки
            if (this.boundaryContainer.OuterBoundary.BaseVertexes.Length > 4)
            {
                crossCount = CountIntersections(point, this.boundaryContainer.OuterBoundary.OutRect);
                //четное - не принадлежит, нечетное - находится в области
                if (crossCount % 2 == 0)
                    return false;
            }

            //проверка вхождения во внешнюю оболочку
            crossCount = CountIntersections(point, this.boundaryContainer.OuterBoundary.BaseVertexes);
            //требуется принадлежность области
            if (crossCount % 2 == 0)
                return false;

            //проверка нахождения ЗА пределами прямоугольников, описанных около
            // внутренних оболочек
            foreach (BoundaryHull innerBoundary in boundaryContainer.InnerBoundaries)
            {
                //пропускаем, если количество опорных вершин оболочки
                //не больше, чем у прямоугольника (т.е. 4)
                if (innerBoundary.OutRect.Length < 5)
                    continue;

                crossCount = CountIntersections(point, innerBoundary.OutRect);
                //нужно, чтобы точка не входила в оболочку, т.к. innerBoundary является дыркой
                if (crossCount % 2 == 1)
                    return false;
            }

            //проверка нахождения ЗА пределами внутренних оболочек
            foreach (BoundaryHull innerBoundary in boundaryContainer.InnerBoundaries)
            {
                crossCount = CountIntersections(point, innerBoundary.BaseVertexes);
                //нужно, чтобы точка не входила в оболочку, т.к. innerBoundary является дыркой
                if (crossCount % 2 == 1)
                    return false;
            }

            return true;
        }


        /// <summary>
        /// Рассчитать количество пересечений луча с вершиной в <paramref name="point"/>
        /// с оболочкой, образованной при помощи <paramref name="boundaryVertexes"/>
        /// </summary>
        /// <param name="point"></param>
        /// <param name="boundaryVertexes"></param>
        /// <returns></returns>
        int CountIntersections(IHPoint point, IHPoint[] boundaryVertexes)
        {
            //количество пересечений
            int crossCount = 0;
            for (int i = 0; i < boundaryVertexes.Length; i++)
            {
                //поиск пересечения ребра оболочки и
                //отрезка с точками point и внешней точкой
                if (CrossLineUtils.IsCrossing(
                    (HPoint)boundaryVertexes[i],
                    (HPoint)boundaryVertexes[(i + 1) % boundaryVertexes.Length],
                    (HPoint)externalPoint,
                    (HPoint)point
                    ))
                    crossCount++;
            }
            return crossCount;
        }
        #endregion


        #region Отсечение треугольников
        /// <summary>
        /// Определить принадлежность треугольника области
        /// </summary>
        /// <param name="trid">id треугольника из <see cref="Triangles"/></param>
        /// <returns>true - принаделжит области, иначе - false</returns>
        bool IsTriangleInArea(int trid)
        {
            var triangle = Triangles[trid];
            (int i, int j, int k) = triangle.Get();

            //вычисляем принадлежность треугольника области
            double ctx = (coordsX[i] + coordsX[j] + coordsX[k]) / 3;
            double cty = (coordsY[i] + coordsY[j] + coordsY[k]) / 3;
            HPoint ctri = new HPoint(ctx, cty);

            bool isInArea = IsInArea(ctri);
            return isInArea;
        }

        /// <summary>
        /// Отсечение треугольников
        /// </summary>
        protected void ClippingTriangles()
        {
            //выход, если граница не задана
            if (this.boundaryContainer is null)
                return;
            //задана ли внешняя оболочка
            if (this.boundaryContainer.OuterBoundary is null)
                throw new ArgumentNullException($"контейнер границ передан, но не задана внешняя оболочка!");

            InitializeExternalPoint();

            //определение принадлежности области "нулевого" треугольника
            for (int triangleId = 0; triangleId < Triangles.Length; triangleId++)
            {
                //принадлежность треугольника области уже определена
                if (Triangles[triangleId].flag != TriangleState.None)
                    continue;
                int triangleInfectCnt = ClippingTriangles(triangleId);
#if DEBUG
                Log.Debug($"TriangleId:{triangleId};\tЗаражено: {triangleInfectCnt}");
#endif
            }
        }

        /// <summary>
        /// Определить принадлежность треугольников области на основе принадлежности
        /// <paramref name="trid"/>.
        /// </summary>
        /// <param name="trid">идентификатор треугольника</param>
        int ClippingTriangles(int trid)
        {
            //количество проверенных треугольников
            int clippedCnt = 0;

            //определение принадлежности области
            bool isInArea = IsTriangleInArea(trid);
            //значение для отсечения (по умолчанию треугольник - внешний)
            TriangleState clipValue = TriangleState.External;
            //входит в область
            if (isInArea)
                clipValue = TriangleState.Internal;
            Triangles[trid].flag = clipValue;

            //стек отсечения
            int[] clipStack = null;
            MEM.Alloc(this.Triangles.Length, ref clipStack, -1);
            //индекс текущей пустой ячейки стека
            int i = 0;
            //размещаем в стеке нулевой треугольник
            clipStack[i++] = trid;

            //начинаем заражение, счетчик может наращиваться внутри цикла
            for (i = 1; i > 0;)
            {
                //достаем из стека верхний (последний) треугольник
                trid = clipStack[--i];
                //зануляем значение в стеке
                clipStack[i] = -1;
                clipValue = Triangles[trid].flag;

                for (int he = trid * 3; he < trid * 3 + 3;)
                {
                    int vid = HalfEdgeUtils.Origin(Triangles, he);

                    int twinHe = HalfEdgeUtils.Twin(HalfEdges, he);
                    int twinTrid = twinHe / 3;
                    //нет смежного треугольника или смежный треугольник уже обработан
                    if (he == -1 || twinHe == -1 || Triangles[twinTrid].flag != TriangleState.None)
                    {
                        he++;
                        continue;
                    }

                    //помещаем текущий треугольник в стек отсечения
                    clipStack[i++] = trid;

                    int vid2 = HalfEdgeUtils.Origin(Triangles, twinHe);

                    //проверка - является ли смежное ребро граничным
                    if (
                        //обе точки являются граничными
                        (pointStatuses[vid] == PointStatus.Boundary ||
                        pointStatuses[vid2] == PointStatus.Boundary) &&
                        //первая точка имеет соседа - вторую точку
                        boundaryEdges[vid].Adjacents.Contains(vid2)
                    )
                    {
                        if (clipValue == TriangleState.External)
                            clipValue = TriangleState.Internal;
                        else
                            clipValue = TriangleState.External;
                    }
                    clippedCnt++;
                    Triangles[twinTrid].flag = clipValue;
                    //в качестве текущего треугольника устанавливаем смежный
                    trid = twinTrid;
                    he = trid * 3;
                }

                //удаляем связи с треугольником, не входящим в область
                if (Triangles[trid].flag == TriangleState.External)
                    UnLinkTriangle(trid);
            }
            return clippedCnt;
        }
        #endregion


        #region Восстановление граничного контура
        /// <summary>
        /// Восстановление граничного контура
        /// </summary>
        protected void RestoreBorder()
        {
            if (boundaryContainer is null)
                return;

            List<IHPoint> pointsLst = new List<IHPoint>((int)(Points.Length * 1.25));
            pointsLst.AddRange(Points);
            List<int> halfEdgesLst = new List<int>((int)(HalfEdges.Length * 1.25));
            halfEdgesLst.AddRange(HalfEdges);
            List<PointStatus> pointStatusesLst = new List<PointStatus>((int)(pointStatuses.Length * 1.25));
            pointStatusesLst.AddRange(pointStatuses);
            List<Troika> facesLst = new List<Troika>((int)(Triangles.Length * 1.25));
            facesLst.AddRange(Triangles);
            List<EdgePair> boundaryEdgesLst = new List<EdgePair>((int)(boundaryEdges.Length * 1.25));
            boundaryEdgesLst.AddRange(boundaryEdges);
            EdgeSplitter edgeSplitter = new EdgeSplitter(
                pointsLst,
                halfEdgesLst,
                pointStatusesLst,
                facesLst,
                boundaryEdgesLst
                );

            bool missAdj1, missAdj2;
            //проход по всем полуребрам в поисках тех, что указывают на граничную точку
            for (int he = 0; he < HalfEdges.Length; he++)
            {

                int vid = HalfEdgeUtils.Origin(facesLst, he);
                //точка неграничная - пропуск
                if (pointStatusesLst[vid] != PointStatus.Boundary)
                    continue;

                //смежные полуребра для he
                //(кроме последнего - последнее не смежное, но содержит смежную вершину!)
                int[] adjHes = HalfEdgeUtils.AdjacentEdgesVertex(halfEdgesLst, facesLst, he, true);

                //true - упущено одно из ребер
                missAdj1 = true;
                missAdj2 = true;

                //проверка существования ребер
                for (int i = 0; i < adjHes.Length; i++)
                {
                    int twinHe = adjHes[i];
                    int twinVid = HalfEdgeUtils.Origin(facesLst, twinHe);

                    if (boundaryEdgesLst[vid].adjacent1 == twinVid)
                        missAdj1 = false;
                    if (boundaryEdgesLst[vid].adjacent2 == twinVid)
                        missAdj2 = false;

                    //связи существуют, поэтому заканчиваем цикл
                    if (!missAdj1 && !missAdj2)
                        break;
                }

                if (missAdj1)
                    RestoreEdge(he, boundaryEdges[vid].adjacent1);
                //определение верное
                if (missAdj2)
                    RestoreEdge(he, boundaryEdges[vid].adjacent2);
            }

            points = pointsLst.ToArray();
            HalfEdges = halfEdgesLst.ToArray();
            pointStatuses = pointStatusesLst.ToArray();
            Triangles = facesLst.ToArray();
            boundaryEdges = boundaryEdgesLst.ToArray();

            void RestoreEdge(int H0, int missedVid)
            {

                int vid = HalfEdgeUtils.Origin(facesLst, H0);
                //полуребра, исходящие из вершины vid
                var HEs = HalfEdgeUtils.AdjacentEdgesVertex(halfEdgesLst, facesLst, H0, false);

                //обозначает - отсутствие ребра для деления
                const int STOP = -99;
                //полуребро, которое будет разделено надвое
                int splitHe = STOP;
                //точка деления полурерба
                IHPoint newPoint = null;

                //поиск первого ребра, которое будет разделено
                for (int i = 0; i < HEs.Length; i++)
                {
                    int twinHe = HEs[i];
                    //полуребро, которое указывает на vid
                    int he = HalfEdgeUtils.Twin(halfEdgesLst, twinHe);
                    //пропускаем (мб исходное граничное ребро может пересекает границу?)
                    if (he == -1)
                        continue;

                    int nextHe = HalfEdgeUtils.Next(he);
                    newPoint = IsIntersect(nextHe);
                    //нет пересечения
                    if (newPoint is null)
                        continue;

                    //пересечение есть
                    splitHe = HalfEdgeUtils.Twin(halfEdgesLst, nextHe);
                    break;
                }

                //исключение для выявления аномалий
                if (splitHe == STOP)
                    throw new ArgumentException($"{nameof(splitHe)} не определен, " +
                        $"хотя граничного ребра {(vid, missedVid)} фактически нет!");

                //поиск пересечения граничного ребра с ребром he
                //null - нет пересечения, иначе - точка пересечения
                IHPoint IsIntersect(int he)
                {
                    //TODO мб полуребро -1 ?
                    //вершины потенциального ребра для деления
                    int nextHeVid1 = HalfEdgeUtils.Origin(facesLst, he);
                    int nextHeVid2 = HalfEdgeUtils.Origin(facesLst, HalfEdgeUtils.Twin(halfEdgesLst, he));

                    //точка пересечения
                    IHPoint intersect = null;
                    bool isIntersect = CrossLineUtils.IsCrossing(
                        (HPoint)pointsLst[vid], (HPoint)pointsLst[missedVid],
                        (HPoint)pointsLst[nextHeVid1], (HPoint)pointsLst[nextHeVid2],
                        ref intersect);

                    //есть пересечение
                    if (isIntersect)
                        return intersect;
                    return null;
                }

                int nextSplitHe = STOP;
                IHPoint nextNewPoint = null;
                //предыдущая добавленная граничная вершина
                int exAddedVid = vid;
                //до тех пор, пока не дойдем до треугольника с пропущенной вершиной
                while (true)
                {
                    int trid = splitHe / 3;
                    if (facesLst[trid].Contains(missedVid))
                        nextSplitHe = STOP;
                    //следующее ребро тоже будем делить
                    else
                    {
                        int next = HalfEdgeUtils.Next(splitHe);
                        nextNewPoint = IsIntersect(next);
                        if (nextNewPoint != null)
                        {
                            nextSplitHe = HalfEdgeUtils.Twin(halfEdgesLst, next);
                        }
                        else
                        {
                            int prev = HalfEdgeUtils.Prev(splitHe);
                            nextNewPoint = IsIntersect(prev);
                            if (nextNewPoint != null)
                                nextSplitHe = HalfEdgeUtils.Twin(halfEdgesLst, prev);
                            else
                                throw new ArgumentException("Нет пересечений: ни prev, ни next!");
                        }
                    }
                    edgeSplitter.SplitEdge(splitHe, newPoint);
                    int newVidx = pointsLst.Count - 1;
                    pointStatusesLst[newVidx] = PointStatus.Boundary;
                    HalfEdgeUtils.LinkBoundaryEdge(boundaryEdgesLst, newVidx, missedVid, exAddedVid);
                    exAddedVid = newVidx;

                    //следующий треугольник содержит missedVid
                    if (nextSplitHe == STOP)
                        break;

                    splitHe = nextSplitHe;
                    newPoint = nextNewPoint;
                }
            }
        }
        #endregion

    }
}

