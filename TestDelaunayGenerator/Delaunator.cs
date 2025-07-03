using CommonLib;
using CommonLib.Geometry;
using GeometryLib;
using GeometryLib.Geometry;
using GeometryLib.Locators;
using MemLogLib;
using MeshLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestDelaunayGenerator.Boundary;
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
        #endregion

        /// <summary>
        /// Построитель триангуляции Делоне.
        /// </summary>
        /// <param name="points">Множество точек триангуляции</param>
        /// <param name="boundaryContainer">контейнер границ.
        /// Не требуется объединять с <paramref name="points"/></param>
        /// <exception cref="ArgumentException"></exception>
        public Delaunator(IHPoint[] points, BoundaryContainer boundaryContainer = null)
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
        /// Массив индексов выпуклой оболочки данных 
        /// по направлению движения часовой стрелке
        /// </summary>
        int[] hullPrev;
        /// <summary>
        /// Массив индексов выпуклой оболочки данных  
        /// направлению движения против часовой стрелки
        /// </summary>
        int[] hullNext;
        /// <summary>
        /// Массив индексов выпуклой оболочки данных против часовой стрелки
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
        int[] HalfEdges;

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
        Troika[] Triangles;

        /// <summary>
        /// Принадлежность точки области.
        /// Индексация совпадает с <see cref="points"/>
        /// </summary>
        PointStatus[] pointStatuses;

        /// <summary>
        /// Граничные ребра.
        /// Индексация внутри <see cref="EdgeIndex"/> используется из <see cref="points"/>
        /// </summary>
        EdgeIndex[] boundaryEdges;

        #endregion

        //разворачиваем структуру Triangles в сплошной массив
        int[] ToArrayTriangles
        {
            get
            {
                int[] intTriangles = new int[3 * Triangles.Length];
                for (int i = 0; i < Triangles.Length; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        intTriangles[3 * i + j] = Triangles[i][j];
                    }
                }
                return intTriangles;
            }
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
            TriangleInfect[] triangleInfects = new TriangleInfect[Triangles.Length * 3];
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
                triangleInfects[i] = (TriangleInfect)Triangles[i / 3].flag;
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

        public TriMesh ToMesh(bool debug = false)
        {

            //инициализация объекта сетки и выделение памяти
            TriMesh mesh = new ExtendedTriMesh(this.HalfEdges, this.pointStatuses, this.Triangles);

            var triangles = new List<Troika>(Triangles.Length);
            for (int i = 0; i < Triangles.Length; i++)
                if (Triangles[i].flag == (int)TriangleInfect.Internal)
                    triangles.Add(Triangles[i]);
            mesh.AreaElems = triangles.Select(triangle => triangle.GetTri).ToArray();

            mesh.CoordsX = this.coordsX;
            mesh.CoordsY = this.coordsY;

            //формирование граничных точек и ребер
            if (boundaryContainer != null)
            {
                //количество граничных точек
                int boundPointCnt = boundaryContainer.AllBoundaryPoints.Length;

                //ребра
                MEM.Alloc(boundPointCnt, ref mesh.BoundElems);
                MEM.Alloc(boundPointCnt, ref mesh.BoundElementsMark);

                //узлы
                MEM.Alloc(boundPointCnt, ref mesh.BoundKnots);
                MEM.Alloc(boundPointCnt, ref mesh.BoundKnotsMark);

                int meshPointId = 0;
                for (int i = this.points.Length - boundPointCnt; i < this.points.Length; i++)
                {
                    //заполняем ребра
                    mesh.BoundElems[meshPointId].Vertex1 = (uint)i;
                    mesh.BoundElems[meshPointId].Vertex2 = (uint)boundaryEdges[i].adjacent1;
                    mesh.BoundElementsMark[meshPointId] = boundaryEdges[i].BoundaryID;

                    //заполняем узлы
                    mesh.BoundKnots[meshPointId] = i;
                    mesh.BoundKnotsMark[meshPointId] = i;
                    meshPointId++;
                }
            }
            else
            {
                //выделение памяти
                MEM.Alloc(CountHullKnots, ref mesh.BoundElems);
                MEM.Alloc(CountHullKnots, ref mesh.BoundElementsMark);
                for (int i = 0; i < Hull.Length; i++)
                {
                    mesh.BoundElems[i].Vertex1 = (uint)Hull[i];
                    mesh.BoundElems[i].Vertex2 = (uint)Hull[(i + 1) % Hull.Length];
                    mesh.BoundElementsMark[i] = 0;
                }

                MEM.Alloc(CountHullKnots, ref mesh.BoundKnots);
                MEM.Alloc(CountHullKnots, ref mesh.BoundKnotsMark);
                for (int i = 0; i < Hull.Length; i++)
                {
                    mesh.BoundKnots[i] = Hull[i];
                    mesh.BoundKnotsMark[i] = 0;
                }
            }

            if (debug)
                mesh.Print();
            return mesh;
        }


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

                //отсечение точек
                int pointCnt = this.ClippingPoints();
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
            int maxTriangles = 2 * points.Length - 5;
            MEM.Alloc(maxTriangles * 3, ref HalfEdges);
            MEM.Alloc(points.Length, ref hullPrev);
            MEM.Alloc(points.Length, ref hullNext);
            MEM.Alloc(points.Length, ref hullTri);

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
            int triangleId = AddTriangle(i0, i1, i2, -1, -1, -1) / 3;

            //принадлежит треугольник области или нет
            if (boundaryContainer != null)
            {
                bool isInArea = IsTriangleInArea(triangleId);
                TriangleInfect isInAreaEnum = TriangleInfect.External;
                if (isInArea)
                    isInAreaEnum = TriangleInfect.Internal;
                Triangles[triangleId].flag = (int)isInAreaEnum;
            }
            else
            {
                Triangles[triangleId].flag = (int)TriangleInfect.Internal;
            }
                #endregion

                //выделение памяти для массива стека перестроения
                MEM.Alloc((int)Math.Sqrt(points.Length), ref EdgeStack);
            #region Поиск выпуклой оболочки и триангуляции

            //проход по всем узлам оболочки, за исключением тех, что уже в ней,
            //т.е. первых трех
            for (int k = 3; k < ids.Length; k++)
            {
                int pointId = ids[k];

                //ближайший узел к текущему на выпуклой оболочке
                int start = 0;

                // поиск  края видимой выпуклой оболочки, используя хэш ребра
                for (int j = 0; j < hashSize; j++)
                {
                    int key = HashKey(pointId);
                    start = hullHash[(key + j) % hashSize];
                    if (start != -1 && start != hullNext[start])
                        break;
                }
                start = hullPrev[start];
                int e = start;
                int q = hullNext[e];

                // проверка видимости найденного стартового узла и возможности
                // построения новых треугольников на оболочке
                //true - грань видима для добавляемой точки
                while (Orient(pointId, e, q) == false)
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
                int trVertId = AddTriangle(e, pointId, hullNext[e], -1, -1, hullTri[e]);

                // рекурсивная перестройки треугольников от точки к точке,
                // пока они не удовлетворят условию Делоне
                hullTri[pointId] = Legalize(trVertId + 2);
                // добавление треугольника в оболочку
                hullTri[e] = trVertId;
                CountHullKnots++;

                // пройдите вперед по оболочке,
                // добавляя треугольники и переворачивая их рекурсивно
                int nextW = hullNext[e];
                int nextE = hullNext[nextW];
                // проверка видимой грани (nextW,nextE) оболочки из i точки
                // при движении вперед по контуру 
                while (Orient(pointId, nextW, nextE) == true)
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
                    trVertId = AddTriangle(
                        nextW, pointId, nextE, hullTri[pointId], -1, hullTri[nextW]);
                    //  проверка и перестройка по Делоне
                    hullTri[pointId] = Legalize(trVertId + 2);
                    // пометить как удаленный узел ущедщий из оболочки
                    hullNext[nextW] = nextW;
                    CountHullKnots--;
                    // следующее ребро оболочки
                    nextW = nextE;
                    nextE = hullNext[nextW];
                }

                // пройдите назад с другой стороны,
                int prewE = e;
                if (prewE == start)
                {
                    int prewW = hullPrev[prewE];
                    while (Orient(pointId, prewW, prewE) == true)
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
                        trVertId = AddTriangle(prewW, pointId, prewE, -1, hullTri[prewE], hullTri[prewW]);
                        //  проверка и перестройка по Делоне
                        Legalize(trVertId + 2);
                        hullTri[prewW] = trVertId;
                        // пометить как удаленный узел ущедщий из оболочки
                        hullNext[prewE] = prewE;
                        CountHullKnots--;
                        // следующее ребро оболочки
                        prewE = prewW;
                        prewW = hullPrev[prewE];
                    }
                }
                // пометить как удаленный
                hullStart = hullPrev[pointId] = prewE;
                hullNext[prewE] = hullPrev[nextW] = pointId;
                hullNext[pointId] = nextW;
                // сохраните два новых ребра в хэш-таблице
                hullHash[HashKey(pointId)] = pointId;
                hullHash[HashKey(prewE)] = prewE;
            }

            //создаем массив граничных узлов выпуклой оболочки
            Hull = new int[CountHullKnots];
            int s = hullStart;
            for (int i = 0; i < CountHullKnots; i++)
            {
                Hull[i] = s;
                s = hullNext[s];
            }
            //удаление ссылок на временные массивы
            hullPrev = hullNext = hullTri = null;
            //обрезка триангуляционных массивов
            HalfEdges = HalfEdges.Take(triangleVertexCounter).ToArray();
            Triangles = Triangles.Take(triangleVertexCounter / 3).ToArray();

            //отсечение треугольников
            //ClippingTriangles();
            //если множество оболочек не задано, то полученную выпуклую оболочку помечаем граничной
            if (this.boundaryContainer is null)
                Array.ForEach(Hull, x => pointStatuses[x] = PointStatus.Boundary);
            #endregion
        }


        /// <summary>
        /// Идентифицирует граничные ребра, отсутствующие в HalfEdges, и возвращает их индексы вершин.
        /// </summary>
        /// <returns>Массив кортежей, содержащих индексы вершин пропущенных граничных рёбер.</returns>
        //public (int, int)[] FindMissingBoundaryEdges()
        //{
        //    // Список для хранения пропущенных рёбер (кортежи индексов вершин)
        //    var missingEdges = new List<(int, int)>(); 

        //    if (boundaryEdges == null || HalfEdges == null) 
        //        return missingEdges.ToArray(); // Возвращаем пустой массив, если данные отсутствуют

        //    // Цикл по всем граничным рёбрам
        //    for (int i = 0; i < boundaryEdges.Length; i++) 
        //    {
        //        // Индекс начальной вершины текущего граничного ребра
        //        int endIdx = boundaryEdges[i].PointID;
        //        // Индекс конечной вершины текущего граничного ребра 
        //        int startIdx = boundaryEdges[i].adjacent1;
        //        // Флаг, указывающий, найдено ли ребро в HalfEdges
        //        bool edgeFound = false;

        //        // Цикл по всем полуребрам в HalfEdges
        //        for (int j = 0; j < HalfEdges.Length; j++) 
        //        {
        //            // Индекс треугольника, к которому относится текущее полуребро
        //            int halfEdgeTriIdx = j / 3;
        //            // Локальный индекс вершины внутри треугольника (0, 1 или 2)
        //            int localVertex = j % 3;
        //            // Первая вершина текущего ребра треугольника
        //            int v0 = Triangles[halfEdgeTriIdx][localVertex];
        //            // Вторая вершина текущего ребра треугольника
        //            int v1 = Triangles[halfEdgeTriIdx][(localVertex + 1) % 3]; 

        //            // Проверка совпадения ребра в любом направлении
        //            if ((v0 == startIdx && v1 == endIdx) || (v0 == endIdx && v1 == startIdx))
        //            {
        //                edgeFound = true; // Ребро найдено, устанавливаем флаг
        //                break; // Выходим из внутреннего цикла
        //            }
        //        }

        //        if (!edgeFound &&
        //            pointStatuses[startIdx] == PointStatus.Boundary &&
        //                 pointStatuses[endIdx] == PointStatus.Boundary &&
        //                 boundaryEdges[startIdx].Adjacents.Contains(endIdx)) // Если ребро не найдено в HalfEdges
        //        {
        //            missingEdges.Add((startIdx, endIdx)); // Добавляем кортеж индексов пропущенного ребра в список
        //            //return missingEdges.ToArray();
        //        }
        //    }

        //    return missingEdges.ToArray(); // Возвращаем массив всех найденных пропущенных рёбер
        //}
        /// <summary>
        /// Идентифицирует первое граничное ребро, отсутствующее в HalfEdges, и возвращает его индексы вершин.
        /// </summary>
        /// <returns>Кортеж с индексами вершин первого пропущенного граничного ребра или null, если все ребра присутствуют.</returns>
        public (int, int)? FindMissingBoundaryEdges()
        {
            // Проверка на наличие данных
            if (boundaryEdges == null || HalfEdges == null || Triangles == null)
                return null;

            // Цикл по всем граничным ребрам
            for (int i = 0; i < boundaryEdges.Length; i++)
            {
                // Индекс начальной и конечной вершины текущего граничного ребра
                int endIdx = boundaryEdges[i].PointID;
                int startIdx = boundaryEdges[i].adjacent1;

                // Пропускаем, если вершины не граничные
                if (pointStatuses[startIdx] != PointStatus.Boundary || pointStatuses[endIdx] != PointStatus.Boundary)
                    continue;

                // Проверяем, что ребро указано в boundaryEdges
                if (!boundaryEdges[startIdx].Adjacents.Contains(endIdx))
                    continue;

                // Флаг, указывающий, найдено ли ребро в HalfEdges
                bool edgeFound = false;

                // Цикл по всем полуребрам в HalfEdges
                for (int j = 0; j < HalfEdges.Length; j++)
                {
                    // Индекс треугольника, к которому относится текущее полуребро
                    int halfEdgeTriIdx = j / 3;
                    // Локальный индекс вершины внутри треугольника (0, 1 или 2)
                    int localVertex = j % 3;
                    // Первая вершина текущего ребра треугольника
                    int v0 = Triangles[halfEdgeTriIdx][localVertex];
                    // Вторая вершина текущего ребра треугольника
                    int v1 = Triangles[halfEdgeTriIdx][(localVertex + 1) % 3];

                    // Проверка совпадения ребра в любом направлении
                    if ((v0 == startIdx && v1 == endIdx) || (v0 == endIdx && v1 == startIdx))
                    {
                        edgeFound = true; // Ребро найдено
                        break;
                    }
                }

                // Если ребро не найдено, возвращаем его
                if (!edgeFound)
                {
                    return (startIdx, endIdx);
                }
            }

            // Все граничные ребра присутствуют
            return null;
        }


















        /// <summary>
        /// Обрабатывает отсутствующее граничное ребро, находя пересечения с ребрами треугольников,
        /// собирая индексы пересеченных ребер и координаты точек пересечения на границе,
        /// удаляет пересеченные ребра и перестраивает триангуляцию в затронутой области.
        /// </summary>
        /// <param name="boundaryEdge">Отсутствующее граничное ребро в виде кортежа индексов вершин (начало, конец).</param>
        /// <returns>Кортеж, содержащий массив индексов пересеченных полуребер и список точек пересечения.</returns>
        //public (int[] intersectedEdges, List<IHPoint> intersectionPoints) ProcessMissingBoundaryEdge((int start, int end) boundaryEdge)
        //{
        //    // Инициализация списка для хранения индексов пересеченных полуребер
        //    var intersectedEdges = new List<int>();
        //    // Инициализация списка для хранения точек пересечения
        //    var intersectionPoints = new List<IHPoint>();
        //    // Начальная и конечная вершины граничного ребра
        //    int v0 = boundaryEdge.start;
        //    int v1 = boundaryEdge.end;

        //    // Текущая вершина для обработки
        //    int currentVertex = v0;
        //    // Идентификатор текущего треугольника
        //    int currentTriangleId = -1;

        //    // Множество затронутых треугольников для удаления
        //    var affectedTriangles = new HashSet<int>();
        //    // Множество затронутых точек для локальной триангуляции
        //    var affectedPoints = new HashSet<int> { v0, v1 };

        //    // Проверка, что Triangles инициализирован
        //    if (Triangles == null)
        //    {
        //        return (intersectedEdges.ToArray(), intersectionPoints);
        //    }

        //    // Поиск начального треугольника, содержащего v0
        //    for (int i = 0; i < Triangles.Length; i++)
        //    {
        //        if (Triangles[i].i == v0 || Triangles[i].j == v0 || Triangles[i].k == v0)
        //        {
        //            currentTriangleId = i;
        //            break;
        //        }
        //    }

        //    // Если треугольник не найден, возвращаем пустой результат
        //    if (currentTriangleId == -1)
        //    {
        //        return (intersectedEdges.ToArray(), intersectionPoints);
        //    }

        //    // Продолжаем обработку, пока не достигнем треугольника с v1
        //    while (currentTriangleId != -1 && currentVertex != v1)
        //    {
        //        // Получаем текущий треугольник
        //        var triangle = Triangles[currentTriangleId];
        //        int localVertexIndex = -1;

        //        // Определяем локальный индекс текущей вершины в треугольнике
        //        if (triangle.i == currentVertex)
        //            localVertexIndex = 0;
        //        else if (triangle.j == currentVertex)
        //            localVertexIndex = 1;
        //        else if (triangle.k == currentVertex)
        //            localVertexIndex = 2;

        //        // Если вершина не найдена, прерываем цикл (защита от ошибок)
        //        if (localVertexIndex == -1)
        //            break;

        //        // Получаем противоположное ребро (ребро напротив currentVertex)
        //        int e1Index = currentTriangleId * 3 + (localVertexIndex + 1) % 3;
        //        int v2 = Triangles[currentTriangleId][(localVertexIndex + 1) % 3];
        //        int v3 = Triangles[currentTriangleId][(localVertexIndex + 2) % 3];

        //        // Определяем точки противоположного ребра и граничного ребра
        //        HPoint p2 = (HPoint)points[v2];
        //        HPoint p3 = (HPoint)points[v3];
        //        HPoint boundaryStart = (HPoint)points[v0];
        //        HPoint boundaryEnd = (HPoint)points[v1];

        //        // Проверяем пересечение противоположного ребра с граничным
        //        IHPoint intersection = new HPoint();
        //        bool hasIntersection = CrossLineUtils.IsCrossing(p2, p3, boundaryStart, boundaryEnd, ref intersection);

        //        if (hasIntersection)
        //        {
        //            // Проверяем, что точка пересечения не совпадает с конечными точками ребра
        //            if (!HPoint.Equals(intersection, p2) && !HPoint.Equals(intersection, p3))
        //            {
        //                // Добавляем точку пересечения в список
        //                intersectionPoints.Add(intersection);
        //                // Добавляем индекс пересеченного ребра в список
        //                intersectedEdges.Add(e1Index);
        //                // Добавляем треугольник и его вершины в затронутые множества
        //                affectedTriangles.Add(currentTriangleId);
        //                affectedPoints.Add(v2);
        //                affectedPoints.Add(v3);

        //                // Находим смежный треугольник через пересеченное ребро
        //                int oppositeEdgeIndex = HalfEdges[e1Index];
        //                if (oppositeEdgeIndex != -1)
        //                {
        //                    affectedTriangles.Add(oppositeEdgeIndex / 3);
        //                }

        //                // Переходим к смежному треугольнику
        //                if (oppositeEdgeIndex == -1)
        //                {
        //                    // Нет смежного треугольника, прерываем обработку
        //                    break;
        //                }

        //                currentTriangleId = oppositeEdgeIndex / 3;
        //                currentVertex = v2; // Переходим к следующей вершине пересеченного ребра
        //            }
        //            else
        //            {
        //                // Пересечение в конечной точке, переходим к следующему ребру
        //                int nextEdgeIndex = currentTriangleId * 3 + (localVertexIndex + 2) % 3;
        //                currentTriangleId = HalfEdges[nextEdgeIndex] == -1 ? -1 : HalfEdges[nextEdgeIndex] / 3;
        //            }
        //        }
        //        else
        //        {
        //            // Нет пересечения, переходим к следующему ребру в треугольнике
        //            int nextEdgeIndex = currentTriangleId * 3 + (localVertexIndex + 2) % 3;
        //            currentTriangleId = HalfEdges[nextEdgeIndex] == -1 ? -1 : HalfEdges[nextEdgeIndex] / 3;
        //        }

        //        // Проверяем, достиг ли треугольник конечной вершины v1
        //        if (currentTriangleId != -1 && (Triangles[currentTriangleId].i == v1 || Triangles[currentTriangleId].j == v1 || Triangles[currentTriangleId].k == v1))
        //        {
        //            currentVertex = v1; // Достигли конечной вершины
        //            affectedTriangles.Add(currentTriangleId);
        //        }
        //    }

        //    // Удаляем пересеченные ребра и затронутые треугольники
        //    foreach (int edgeIndex in intersectedEdges)
        //    {
        //        int oppositeEdgeIndex = HalfEdges[edgeIndex];
        //        UnLink(edgeIndex, oppositeEdgeIndex);
        //    }

        //    foreach (int triangleId in affectedTriangles)
        //    {
        //        if (triangleId < Triangles.Length)
        //        {
        //            Triangles[triangleId] = new Troika(-1, -1, -1);
        //            HalfEdges[triangleId * 3] = -1;
        //            HalfEdges[triangleId * 3 + 1] = -1;
        //            HalfEdges[triangleId * 3 + 2] = -1;
        //        }
        //    }

        //    // Формируем массив точек для локальной триангуляции
        //    var localPoints = new List<IHPoint>();
        //    var localPointIndices = new List<int>();
        //    foreach (int pointIndex in affectedPoints)
        //    {
        //        if (pointIndex < points.Length)
        //        {
        //            localPoints.Add(points[pointIndex]);
        //            localPointIndices.Add(pointIndex);
        //        }
        //    }

        //    // Добавляем точки пересечения в конец массива точек
        //    int basePointIndex = points.Length;
        //    Array.Resize(ref points, points.Length + intersectionPoints.Count);
        //    Array.Resize(ref pointStatuses, pointStatuses.Length + intersectionPoints.Count);
        //    Array.Resize(ref coordsX, coordsX.Length + intersectionPoints.Count);
        //    Array.Resize(ref coordsY, coordsY.Length + intersectionPoints.Count);
        //    Array.Resize(ref boundaryEdges, boundaryEdges.Length + intersectionPoints.Count);

        //    for (int i = 0; i < intersectionPoints.Count; i++)
        //    {
        //        int newPointIndex = basePointIndex + i;
        //        points[newPointIndex] = intersectionPoints[i];
        //        pointStatuses[newPointIndex] = PointStatus.Boundary;
        //        coordsX[newPointIndex] = intersectionPoints[i].X;
        //        coordsY[newPointIndex] = intersectionPoints[i].Y;
        //        boundaryEdges[newPointIndex] = new EdgeIndex(
        //            pointID: newPointIndex,
        //            adjacent1: v0,
        //            adjacent2: v1,
        //            boundaryId: boundaryEdges[v0].BoundaryID
        //        );
        //        localPoints.Add(intersectionPoints[i]);
        //        localPointIndices.Add(newPointIndex);
        //    }

        //    // Создаем локальную триангуляцию
        //    if (localPoints.Count >= 3)
        //    {
        //        try
        //        {
        //            var localDelaunator = new Delaunator(localPoints.ToArray());
        //            localDelaunator.Generate(); // Явно вызываем генерацию триангуляции

        //            // Проверяем, что локальная триангуляция создана
        //            if (localDelaunator.Triangles == null)
        //            {
        //                Console.WriteLine("Локальная триангуляция не создана: Triangles равен null");
        //                return (intersectedEdges.ToArray(), intersectionPoints);
        //            }

        //            // Обновляем глобальную триангуляцию
        //            int baseTriangleIndex = Triangles.Length;
        //            Array.Resize(ref Triangles, Triangles.Length + localDelaunator.Triangles.Length);
        //            Array.Resize(ref HalfEdges, HalfEdges.Length + localDelaunator.HalfEdges.Length);

        //            for (int i = 0; i < localDelaunator.Triangles.Length; i++)
        //            {
        //                // Преобразуем локальные индексы точек в глобальные
        //                int globalI = localPointIndices[localDelaunator.Triangles[i].i];
        //                int globalJ = localPointIndices[localDelaunator.Triangles[i].j];
        //                int globalK = localPointIndices[localDelaunator.Triangles[i].k];

        //                Triangles[baseTriangleIndex + i] = new Troika(globalI, globalJ, globalK);

        //                // Устанавливаем флаг принадлежности области
        //                bool isInArea = IsTriangleInArea(baseTriangleIndex + i);
        //                Triangles[baseTriangleIndex + i].flag = isInArea ? (int)TriangleInfect.Internal : (int)TriangleInfect.External;

        //                // Обновляем HalfEdges с учетом глобальных индексов
        //                HalfEdges[(baseTriangleIndex + i) * 3] = localDelaunator.HalfEdges[i * 3] == -1 ? -1 : baseTriangleIndex * 3 + localDelaunator.HalfEdges[i * 3];
        //                HalfEdges[(baseTriangleIndex + i) * 3 + 1] = localDelaunator.HalfEdges[i * 3 + 1] == -1 ? -1 : baseTriangleIndex * 3 + localDelaunator.HalfEdges[i * 3 + 1];
        //                HalfEdges[(baseTriangleIndex + i) * 3 + 2] = localDelaunator.HalfEdges[i * 3 + 2] == -1 ? -1 : baseTriangleIndex * 3 + localDelaunator.HalfEdges[i * 3 + 2];
        //            }

        //            // Удаляем пустые треугольники из глобальной структуры
        //            var validTriangles = new List<Troika>();
        //            var validHalfEdges = new List<int>();
        //            for (int i = 0; i < Triangles.Length; i++)
        //            {
        //                if (Triangles[i].i != -1 && Triangles[i].j != -1 && Triangles[i].k != -1)
        //                {
        //                    validTriangles.Add(Triangles[i]);
        //                    validHalfEdges.Add(HalfEdges[i * 3]);
        //                    validHalfEdges.Add(HalfEdges[i * 3 + 1]);
        //                    validHalfEdges.Add(HalfEdges[i * 3 + 2]);
        //                }
        //            }

        //            Triangles = validTriangles.ToArray();
        //            HalfEdges = validHalfEdges.ToArray();

        //            // Проверяем и исправляем связи HalfEdges
        //            for (int i = 0; i < HalfEdges.Length; i++)
        //            {
        //                if (HalfEdges[i] != -1)
        //                {
        //                    int opposite = HalfEdges[i];
        //                    if (opposite >= HalfEdges.Length || HalfEdges[opposite] != i)
        //                    {
        //                        HalfEdges[i] = -1; // Удаляем некорректные связи
        //                    }
        //                }
        //            }
        //        }
        //        catch (ArgumentException ex)
        //        {
        //            Console.WriteLine($"Ошибка при создании локальной триангуляции: {ex.Message}");
        //            return (intersectedEdges.ToArray(), intersectionPoints);
        //        }
        //    }

        //    // Возвращаем кортеж с индексами пересеченных ребер и точками пересечения
        //    return (intersectedEdges.ToArray(), intersectionPoints);
        //}
        public (int[] intersectedEdges, List<IHPoint> intersectionPoints) ProcessMissingBoundaryEdge((int start, int end) boundaryEdge)
        {
            // Инициализация списка для хранения индексов пересеченных полуребер
            var intersectedEdges = new List<int>();
            // Инициализация списка для хранения точек пересечения
            var intersectionPoints = new List<IHPoint>();
            // Начальная и конечная вершины граничного ребра
            int v0 = boundaryEdge.start;
            int v1 = boundaryEdge.end;

            // Текущая вершина для обработки
            int currentVertex = v0;
            // Идентификатор текущего треугольника
            int currentTriangleId = -1;

            // Множество затронутых треугольников для удаления
            var affectedTriangles = new HashSet<int>();
            // Множество затронутых точек для локальной триангуляции
            var affectedPoints = new HashSet<int> { v0, v1 };

            // Проверка, что Triangles инициализирован
            if (Triangles == null)
            {
                return (intersectedEdges.ToArray(), intersectionPoints);
            }

            // Поиск начального треугольника, содержащего v0
            for (int i = 0; i < Triangles.Length; i++)
            {
                if ((Triangles[i].i == v0 || Triangles[i].j == v0 || Triangles[i].k == v0) && Triangles[i].flag == (int)TriangleInfect.Internal)
                {
                    currentTriangleId = i;
                    break;
                }
            }
            Console.WriteLine(currentTriangleId);
            // Если треугольник не найден, возвращаем пустой результат
            if (currentTriangleId == -1)
            {
                return (intersectedEdges.ToArray(), intersectionPoints);
            }

            // Продолжаем обработку, пока не достигнем треугольника с v1
            while (currentTriangleId != -1 && currentVertex != v1)
            {
                // Получаем текущий треугольник
                var triangle = Triangles[currentTriangleId];
                int localVertexIndex = -1;

                // Определяем локальный индекс текущей вершины в треугольнике
                if (triangle.i == currentVertex)
                    localVertexIndex = 0;
                else if (triangle.j == currentVertex)
                    localVertexIndex = 1;
                else if (triangle.k == currentVertex)
                    localVertexIndex = 2;

                // Если вершина не найдена, прерываем цикл 
                if (localVertexIndex == -1)
                    break;

                // Получаем противоположное ребро (ребро напротив currentVertex)
                int e1Index = currentTriangleId * 3 + (localVertexIndex + 1) % 3;
                int v2 = Triangles[currentTriangleId][(localVertexIndex + 1) % 3];
                int v3 = Triangles[currentTriangleId][(localVertexIndex + 2) % 3];

                // Определяем точки противоположного ребра и граничного ребра
                HPoint p2 = (HPoint)points[v2];
                HPoint p3 = (HPoint)points[v3];
                HPoint boundaryStart = (HPoint)points[v0];
                HPoint boundaryEnd = (HPoint)points[v1];

                // Проверяем пересечение противоположного ребра с граничным
                IHPoint intersection = new HPoint();
                bool hasIntersection = CrossLineUtils.IsCrossing(p2, p3, boundaryStart, boundaryEnd, ref intersection);

                if (hasIntersection)
                {
                    // Проверяем, что точка пересечения не совпадает с конечными точками ребра
                    if (!HPoint.Equals(intersection, p2) && !HPoint.Equals(intersection, p3))
                    {
                        // Добавляем точку пересечения в список
                        intersectionPoints.Add(intersection);
                        // Добавляем индекс пересеченного ребра в список
                        intersectedEdges.Add(e1Index);
                        // Добавляем треугольник и его вершины в затронутые множества
                        affectedTriangles.Add(currentTriangleId);
                        affectedPoints.Add(v2);
                        affectedPoints.Add(v3);

                        // Находим смежный треугольник через пересеченное ребро
                        int oppositeEdgeIndex = HalfEdges[e1Index];
                        if (oppositeEdgeIndex != -1)
                        {
                            affectedTriangles.Add(oppositeEdgeIndex / 3);
                        }

                        // Переходим к смежному треугольнику
                        if (oppositeEdgeIndex == -1)
                        {
                            // Нет смежного треугольника, прерываем обработку
                            break;
                        }

                        currentTriangleId = oppositeEdgeIndex / 3;
                        currentVertex = v2; // Переходим к следующей вершине пересеченного ребра
                    }
                    else
                    {
                        // Пересечение в конечной точке, переходим к следующему ребру
                        int nextEdgeIndex = currentTriangleId * 3 + (localVertexIndex + 2) % 3;
                        currentTriangleId = HalfEdges[nextEdgeIndex] == -1 ? -1 : HalfEdges[nextEdgeIndex] / 3;
                    }
                }
                else
                {
                    // Нет пересечения, переходим к следующему ребру в треугольнике
                    int nextEdgeIndex = currentTriangleId * 3 + (localVertexIndex + 2) % 3;
                    currentTriangleId = HalfEdges[nextEdgeIndex] == -1 ? -1 : HalfEdges[nextEdgeIndex] / 3;
                }

                // Проверяем, достиг ли треугольник конечной вершины v1
                if (currentTriangleId != -1 && (Triangles[currentTriangleId].i == v1 || Triangles[currentTriangleId].j == v1 || Triangles[currentTriangleId].k == v1))
                {
                    currentVertex = v1; // Достигли конечной вершины
                    affectedTriangles.Add(currentTriangleId);
                }
            }

            // Удаляем пересеченные ребра и затронутые треугольники
            foreach (int edgeIndex in intersectedEdges)
            {
                int oppositeEdgeIndex = HalfEdges[edgeIndex];
                UnLink(edgeIndex, oppositeEdgeIndex);
            }

            foreach (int triangleId in affectedTriangles)
            {
                if (triangleId < Triangles.Length)
                {
                    Triangles[triangleId] = new Troika(-1, -1, -1);
                    HalfEdges[triangleId * 3] = -1;
                    HalfEdges[triangleId * 3 + 1] = -1;
                    HalfEdges[triangleId * 3 + 2] = -1;
                }
            }

            // Формируем массив точек для локальной триангуляции
            var localPoints = new List<IHPoint>();
            var localPointIndices = new List<int>();
            foreach (int pointIndex in affectedPoints)
            {
                if (pointIndex < points.Length)
                {
                    localPoints.Add(points[pointIndex]);
                    localPointIndices.Add(pointIndex);
                }
            }

            // Добавляем точки пересечения в конец массива точек и обновляем связи boundaryEdges
            int basePointIndex = points.Length;
            Array.Resize(ref points, points.Length + intersectionPoints.Count);
            Array.Resize(ref pointStatuses, pointStatuses.Length + intersectionPoints.Count);
            Array.Resize(ref coordsX, coordsX.Length + intersectionPoints.Count);
            Array.Resize(ref coordsY, coordsY.Length + intersectionPoints.Count);
            Array.Resize(ref boundaryEdges, boundaryEdges.Length + intersectionPoints.Count);

            // Храним индексы новых точек для обновления boundaryEdges
            var newPointIndices = new List<int>();
            for (int i = 0; i < intersectionPoints.Count; i++)
            {
                int newPointIndex = basePointIndex + i;
                points[newPointIndex] = intersectionPoints[i];
                pointStatuses[newPointIndex] = PointStatus.Boundary;
                coordsX[newPointIndex] = intersectionPoints[i].X;
                coordsY[newPointIndex] = intersectionPoints[i].Y;

                // Создаем связи для новой точки
                int prevPoint = i == 0 ? v0 : (basePointIndex + i - 1);
                int nextPoint = i == intersectionPoints.Count - 1 ? v1 : (basePointIndex + i + 1);
                boundaryEdges[newPointIndex] = new EdgeIndex(
                    pointID: newPointIndex,
                    adjacent1: prevPoint,
                    adjacent2: nextPoint,
                    boundaryId: boundaryEdges[v0].BoundaryID
                );

                newPointIndices.Add(newPointIndex);
                localPoints.Add(intersectionPoints[i]);
                localPointIndices.Add(newPointIndex);
            }

            // Обновляем boundaryEdges для исходных вершин v0 и v1
            if (intersectionPoints.Count > 0)
            {
                // Обновляем связи для v0
                int firstNewPoint = newPointIndices[0];
                boundaryEdges[v0] = new EdgeIndex(
                    pointID: v0,
                    adjacent1: boundaryEdges[v0].adjacent1 == v1 ? firstNewPoint : boundaryEdges[v0].adjacent1,
                    adjacent2: boundaryEdges[v0].adjacent2 == v1 ? firstNewPoint : boundaryEdges[v0].adjacent2,
                    boundaryId: boundaryEdges[v0].BoundaryID
                );

                // Обновляем связи для v1
                int lastNewPoint = newPointIndices[newPointIndices.Count - 1];
                boundaryEdges[v1] = new EdgeIndex(
                    pointID: v1,
                    adjacent1: boundaryEdges[v1].adjacent1 == v0 ? lastNewPoint : boundaryEdges[v1].adjacent1,
                    adjacent2: boundaryEdges[v1].adjacent2 == v0 ? lastNewPoint : boundaryEdges[v1].adjacent2,
                    boundaryId: boundaryEdges[v1].BoundaryID
                );
            }

            // Создаем локальную триангуляцию
            if (localPoints.Count >= 3)
            {
                try
                {
                    var localDelaunator = new Delaunator(localPoints.ToArray());
                    localDelaunator.Generate(); // Явно вызываем генерацию триангуляции

                    // Проверяем, что локальная триангуляция создана
                    if (localDelaunator.Triangles == null)
                    {
                        Console.WriteLine("Локальная триангуляция не создана: Triangles равен null");
                        return (intersectedEdges.ToArray(), intersectionPoints);
                    }

                    // Обновляем глобальную триангуляцию
                    int baseTriangleIndex = Triangles.Length;
                    Array.Resize(ref Triangles, Triangles.Length + localDelaunator.Triangles.Length);
                    Array.Resize(ref HalfEdges, HalfEdges.Length + localDelaunator.HalfEdges.Length);

                    for (int i = 0; i < localDelaunator.Triangles.Length; i++)
                    {
                        // Преобразуем локальные индексы точек в глобальные
                        int globalI = localPointIndices[localDelaunator.Triangles[i].i];
                        int globalJ = localPointIndices[localDelaunator.Triangles[i].j];
                        int globalK = localPointIndices[localDelaunator.Triangles[i].k];

                        Triangles[baseTriangleIndex + i] = new Troika(globalI, globalJ, globalK);

                        // Устанавливаем флаг принадлежности области
                        bool isInArea = IsTriangleInArea(baseTriangleIndex + i);
                        Triangles[baseTriangleIndex + i].flag = isInArea ? (int)TriangleInfect.Internal : (int)TriangleInfect.External;

                        // Обновляем HalfEdges с учетом глобальных индексов
                        HalfEdges[(baseTriangleIndex + i) * 3] = localDelaunator.HalfEdges[i * 3] == -1 ? -1 : baseTriangleIndex * 3 + localDelaunator.HalfEdges[i * 3];
                        HalfEdges[(baseTriangleIndex + i) * 3 + 1] = localDelaunator.HalfEdges[i * 3 + 1] == -1 ? -1 : baseTriangleIndex * 3 + localDelaunator.HalfEdges[i * 3 + 1];
                        HalfEdges[(baseTriangleIndex + i) * 3 + 2] = localDelaunator.HalfEdges[i * 3 + 2] == -1 ? -1 : baseTriangleIndex * 3 + localDelaunator.HalfEdges[i * 3 + 2];
                    }

                    // Удаляем пустые треугольники из глобальной структуры
                    var validTriangles = new List<Troika>();
                    var validHalfEdges = new List<int>();
                    for (int i = 0; i < Triangles.Length; i++)
                    {
                        if (Triangles[i].i != -1 && Triangles[i].j != -1 && Triangles[i].k != -1)
                        {
                            validTriangles.Add(Triangles[i]);
                            validHalfEdges.Add(HalfEdges[i * 3]);
                            validHalfEdges.Add(HalfEdges[i * 3 + 1]);
                            validHalfEdges.Add(HalfEdges[i * 3 + 2]);
                        }
                    }

                    Triangles = validTriangles.ToArray();
                    HalfEdges = validHalfEdges.ToArray();

                    // Проверяем и исправляем связи HalfEdges
                    for (int i = 0; i < HalfEdges.Length; i++)
                    {
                        if (HalfEdges[i] != -1)
                        {
                            int opposite = HalfEdges[i];
                            if (opposite >= HalfEdges.Length || HalfEdges[opposite] != i)
                            {
                                HalfEdges[i] = -1; // Удаляем некорректные связи
                            }
                        }
                    }
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Ошибка при создании локальной триангуляции: {ex.Message}");
                    return (intersectedEdges.ToArray(), intersectionPoints);
                }
            }

            // Возвращаем кортеж с индексами пересеченных ребер и точками пересечения
            return (intersectedEdges.ToArray(), intersectionPoints);
        }






        /// <summary>
        /// Обрабатывает отсутствующее граничное ребро, находя пересечения с ребрами треугольников,
        /// добавляя точки пересечения в массив точек и собирая индексы пересеченных ребер.
        /// </summary>
        /// <param name="boundaryEdge">Отсутствующее граничное ребро в виде кортежа индексов вершин (начало, конец).</param>
        /// <returns>Массив индексов полуребер, пересекающихся с граничным ребром.</returns>
        //public int[] ProcessMissingBoundaryEdge((int start, int end) boundaryEdge)
        //{
        //    // Инициализация списка для хранения индексов пересеченных полуребер
        //    var intersectedEdges = new List<int>();
        //    // Начальная и конечная вершины граничного ребра
        //    int v0 = boundaryEdge.start;
        //    int v1 = boundaryEdge.end;

        //    // Текущая вершина для обработки
        //    int currentVertex = v0;
        //    // Идентификатор текущего треугольника
        //    int currentTriangleId = -1;

        //    // Поиск начального треугольника, содержащего v0
        //    for (int i = 0; i < Triangles.Length; i++)
        //    {
        //        if (Triangles[i].i == v0 || Triangles[i].j == v0 || Triangles[i].k == v0)
        //        {
        //            currentTriangleId = i;
        //            break;
        //        }
        //    }

        //    // Если треугольник не найден, возвращаем пустой массив
        //    if (currentTriangleId == -1)
        //    {
        //        return intersectedEdges.ToArray();
        //    }

        //    // Продолжаем обработку, пока не достигнем треугольника с v1
        //    while (currentTriangleId != -1 && currentVertex != v1)
        //    {
        //        // Получаем текущий треугольник
        //        var triangle = Triangles[currentTriangleId];
        //        int localVertexIndex = -1;

        //        // Определяем локальный индекс текущей вершины в треугольнике
        //        if (triangle.i == currentVertex)
        //            localVertexIndex = 0;
        //        else if (triangle.j == currentVertex)
        //            localVertexIndex = 1;
        //        else if (triangle.k == currentVertex)
        //            localVertexIndex = 2;

        //        // Если вершина не найдена, прерываем цикл (защита от ошибок)
        //        if (localVertexIndex == -1)
        //            break;

        //        // Получаем противоположное ребро (ребро напротив currentVertex)
        //        int e1Index = currentTriangleId * 3 + (localVertexIndex + 1) % 3;
        //        int v2 = Triangles[currentTriangleId][(localVertexIndex + 1) % 3];
        //        int v3 = Triangles[currentTriangleId][(localVertexIndex + 2) % 3];

        //        // Определяем точки противоположного ребра и граничного ребра
        //        HPoint p2 = (HPoint)points[v2];
        //        HPoint p3 = (HPoint)points[v3];
        //        HPoint boundaryStart = (HPoint)points[v0];
        //        HPoint boundaryEnd = (HPoint)points[v1];

        //        // Проверяем пересечение противоположного ребра с граничным
        //        IHPoint intersection = new HPoint();
        //        bool hasIntersection = CrossLineUtils.IsCrossing(p2, p3, boundaryStart, boundaryEnd, ref intersection);

        //        if (hasIntersection)
        //        {
        //            // Проверяем, что точка пересечения не совпадает с конечными точками ребра
        //            if (!HPoint.Equals(intersection, p2) && !HPoint.Equals(intersection, p3))
        //            {
        //                //Console.WriteLine($"X = {intersection.X}; Y = {intersection.Y}");
        //                // Добавляем точку пересечения в массив точек
        //                int newPointIndex = points.Length;
        //                Array.Resize(ref points, points.Length + 1);
        //                points[newPointIndex] = intersection;

        //                // Помечаем новую точку как граничную
        //                Array.Resize(ref pointStatuses, pointStatuses.Length + 1);
        //                pointStatuses[newPointIndex] = PointStatus.Boundary;

        //                // Обновляем массивы координат
        //                Array.Resize(ref coordsX, coordsX.Length + 1);
        //                Array.Resize(ref coordsY, coordsY.Length + 1);
        //                coordsX[newPointIndex] = intersection.X;
        //                coordsY[newPointIndex] = intersection.Y;

        //                // Обновляем boundaryEdges для новой точки
        //                Array.Resize(ref boundaryEdges, boundaryEdges.Length + 1);
        //                boundaryEdges[newPointIndex] = new EdgeIndex(
        //                    pointID: newPointIndex,
        //                    adjacent1: v0, // Соединяем с началом граничного ребра
        //                    adjacent2: v1, // Соединяем с концом граничного ребра
        //                    boundaryId: boundaryEdges[v0].BoundaryID
        //                );

        //                // Добавляем индекс пересеченного ребра в список
        //                intersectedEdges.Add(e1Index);

        //                // Находим смежный треугольник через пересеченное ребро
        //                int oppositeEdgeIndex = HalfEdges[e1Index];
        //                if (oppositeEdgeIndex == -1)
        //                {
        //                    // Нет смежного треугольника, прерываем обработку
        //                    break;
        //                }

        //                // Переходим к смежному треугольнику
        //                currentTriangleId = oppositeEdgeIndex / 3;
        //                currentVertex = v2; // Переходим к следующей вершине пересеченного ребра
        //            }
        //            else
        //            {
        //                // Пересечение в конечной точке, переходим к следующему ребру
        //                int nextEdgeIndex = currentTriangleId * 3 + (localVertexIndex + 2) % 3;
        //                currentTriangleId = HalfEdges[nextEdgeIndex] == -1 ? -1 : HalfEdges[nextEdgeIndex] / 3;
        //            }
        //        }
        //        else
        //        {
        //            // Нет пересечения, переходим к следующему ребру в треугольнике
        //            int nextEdgeIndex = currentTriangleId * 3 + (localVertexIndex + 2) % 3;
        //            currentTriangleId = HalfEdges[nextEdgeIndex] == -1 ? -1 : HalfEdges[nextEdgeIndex] / 3;
        //        }

        //        // Проверяем, достиг ли треугольник конечной вершины v1
        //        if (currentTriangleId != -1 && (Triangles[currentTriangleId].i == v1 || Triangles[currentTriangleId].j == v1 || Triangles[currentTriangleId].k == v1))
        //        {
        //            currentVertex = v1; // Достигли конечной вершины
        //        }
        //    }

        //    // Возвращаем массив индексов пересеченных ребер
        //    return intersectedEdges.ToArray();
        //}


        
























        #region Логика генерации триангуляции делоне по S-hull
        /// <summary>
        /// рекурсивная перестройки треугольников от точки к точке,
        /// пока они не удовлетворят условию Делоне 
        /// </summary>
        /// <param name="EdgeA_ID">индекс 3-ей вершины треугольника в массиве <see cref="Triangles"/></param>
        /// <returns>индекс 2-ой (средней) вершины треугольника</returns>
        private int Legalize(int EdgeA_ID)
        {
            //индекс текущей пустой ячейки стека
            var i = 0;
            int ar = -1;

            // рекурсия устранена с помощью стека фиксированного размера
            while (true)
            {

                //смежное полуребро для EdgeA_ID
                var EdgeB_ID = HalfEdges[EdgeA_ID];
                //если смежный треугольник не был найден (т.е. -1), то достаем следующий из стека
                if (EdgeB_ID == -1)
                {
                    // граница выпуклой оболочки
                    if (i == 0)
                        break;
                    EdgeA_ID = EdgeStack[--i];
                    continue;
                }

                // адрес - смешение для 1 треугольника (1-ый индекс в треугольнике)
                int triA_ID = EdgeA_ID - EdgeA_ID % 3;
                ar = triA_ID + (EdgeA_ID + 2) % 3;

                int al = triA_ID + (EdgeA_ID + 1) % 3;
                // адрес - смешение для 2 треугольника
                int triB_ID = EdgeB_ID - EdgeB_ID % 3;
                int bl = triB_ID + (EdgeB_ID + 2) % 3;

                //новый треугольник
                int idxElemA = triA_ID / 3;
                //треугольник, уже входящий в оболочку и смежный с новым
                int idxElemB = triB_ID / 3;
                int p0 = Triangles[idxElemA][(EdgeA_ID + 2) % 3];
                int pr = Triangles[idxElemA][(EdgeA_ID + 0) % 3];
                int pl = Triangles[idxElemA][(EdgeA_ID + 1) % 3];
                //вершина смежного треугольника
                int p1 = Triangles[idxElemB][(EdgeB_ID + 2) % 3];

                //если задан граничный контур
                if (boundaryContainer != null)
                {
                    // начало ребра
                    int localVertex = EdgeA_ID % 3;
                    int edgeIdStart = Triangles[idxElemA][localVertex];

                    // конец ребра
                    localVertex = EdgeB_ID % 3;
                    int edgeIdEnd = Triangles[idxElemB][localVertex];
                    
                    //смежное ребро между треугольниками является граничным
                    if (pointStatuses[edgeIdStart] == PointStatus.Boundary &&
                         pointStatuses[edgeIdEnd] == PointStatus.Boundary &&
                         boundaryEdges[edgeIdStart].Adjacents.Contains(edgeIdEnd))
                    {
#if DEBUG

                        Console.WriteLine($"Легализация пропущена для треугольников {idxElemA}(новый) {idxElemB}(в оболочке), " +
                            $"ребро ({edgeIdStart}-{edgeIdEnd}) является граничным");
#endif
                        //инвертируем принадлежность области для нового треугольника (треуг A)
                        //относительно смежного с ним (треуг B)

                        TriangleInfect newTriangleValue = TriangleInfect.External;
                        if (Triangles[idxElemB].flag == (int)newTriangleValue)
                            newTriangleValue = TriangleInfect.Internal;
                        Triangles[idxElemA].flag = (int)newTriangleValue;

                        //берем следующий из стека
                        if (i == 0)
                            break;
                        EdgeA_ID = EdgeStack[--i];
                        continue;
                    }
                    //смежное ребро не граничое => новый треугольник входит в область
                    else
                    {
                        Triangles[idxElemA].flag = Triangles[idxElemB].flag;
                    }
                }
                //граничный контур не задан => все треугольники входят в область
                else
                {
                    Triangles[idxElemA].flag = (int)TriangleInfect.Internal;
                }

                    bool illegal = InCircle(p0, pr, pl, p1);
                if (illegal)
                {
                    // Если пара треугольников не удовлетворяет условию Делоне
                    // (p1 находится внутри описанной окружности [p0, pl, pr]),
                    // переверните их против часовой стрелки.
                    // Выполните ту же проверку рекурсивно для новой пары
                    // треугольников
                    //                                    triA
                    //            pl                       pl
                    //           /||\                     /  \
                    //        al/ || \bl               al/    \EdgeA_ID
                    //         /  ||  \                 /      \
                    //    EdgeA_ID|| EdgeB_ID  flip    /___ar___\
                    //      p0\   ||   /p1      =>   p0\---bl---/p1
                    //         \  ||  /                 \      /
                    //        ar\ || /br         EdgeB_ID\    /br
                    //           \||/                     \  /
                    //            pr                       pr
                    //                                    triB
                    Triangles[idxElemA][(EdgeA_ID + 0) % 3] = p1;
                    Triangles[idxElemB][(EdgeB_ID + 0) % 3] = p0;

                    int hbl = HalfEdges[bl];
                    // ребро поменяно местами на другой стороне оболочки (редко);
                    // исправить ссылку ребра смежного треугольника
                    if (hbl == -1)
                    {
                        int e = hullStart;
                        do
                        {
                            if (hullTri[e] == bl)
                            {
                                hullTri[e] = EdgeA_ID;
                                break;
                            }
                            e = hullPrev[e];
                        }
                        while (e != hullStart);
                    }
                    Link(EdgeA_ID, hbl);
                    Link(EdgeB_ID, HalfEdges[ar]);
                    Link(ar, bl);
                    // не беспокойтесь о достижении предела: это может
                    // произойти только при крайне вырожденном вводе
                    if (i < EdgeStack.Length)
                    {
                        //помещаем середину второго треугольника полученного при флипе
                        EdgeStack[i++] = triB_ID + (EdgeB_ID + 1) % 3;
                    }
                    else
                    {
                        Console.WriteLine("Переполнение стека при проверке Делоне" +
                            " для добавленных треугольников!");
                        break;
                    }
                }
                else
                {
                    if (i == 0)
                        break;
                    EdgeA_ID = EdgeStack[--i];
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
        /// <returns>возвращает индекс вершины в <see cref="points"/>. Первая вершина треугольника.</returns>
        private int AddTriangle(int i0, int i1, int i2, int a, int b, int c)
        {
            //индекс треугольника
            int triangleId = triangleVertexCounter / 3;
            Triangles[triangleId].i = i0;
            Triangles[triangleId].j = i1;
            Triangles[triangleId].k = i2;

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
        void Link(int EdgesID, int b, bool isBoundary = false)
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
            int markedPointAmount = 0;
            //определение принадлежности точек области

            #region Однопоточное отсечение точек. Удобно для дебага
            //for (int i = 0; i < points.Length; i++)
            //{
            //    bool isInArea = IsInArea(points[i]);
            //    //устанавливаем текущую точку, как входящую в область marker == 1
            //    if (isInArea)
            //    {
            //        points[i].marker = (int)PointStatus.Internal;
            //        //требуется для корректного результата в рамках "гонки потоков"
            //        Interlocked.Increment(ref markedPointAmount);
            //    }
            //    //точка не граничная
            //    else
            //    {
            //        points[i].marker = (int)PointStatus.External;
            //    }
            //}
            #endregion

            //отсечение точек в параллель
            Parallel.For(
                0, points.Length, (i, loopState) =>
                {
                    bool isInArea = IsInArea(points[i]);
                    //устанавливаем текущую точку, как входящую в область marker == 1
                    if (isInArea)
                    {
                        pointStatuses[i] = PointStatus.Internal;
                        //требуется для корректного результата в рамках "гонки потоков"
                        Interlocked.Increment(ref markedPointAmount);
                    }
                    //точка не граничная
                    else
                    {
                        pointStatuses[i] = PointStatus.External;
                    }
                }
            );

            //текущий индекс для перезаписи в массиве
            int currentPointIndex = 0;
            //оставляем в массиве только точки, входящие в область
            for (int i = 0; i < points.Length; i++)
            {
                if (pointStatuses[i] == PointStatus.Internal)
                {
                    points[currentPointIndex] = points[i];
                    pointStatuses[currentPointIndex] = pointStatuses[i];
                    currentPointIndex++;
                }
            }

            return markedPointAmount;
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
            int curPointId = notBorderPointCnt;
            //смещение по количеству точек до граничных точек
            int offset = curPointId;
            //проход по каждой оболочке
            for (int boundId = 0; boundId < boundaryContainer.Count; boundId++)
            {
                //количество точек на текущем контуре
                int bndPointCnt = boundaryContainer[boundId].Points.Length;
                //проход по точкам внутри оболочки
                for (int i = 0; i < bndPointCnt; i++)
                {
                    //копируем граничную точку в общий массив точек
                    points[curPointId] = boundaryContainer[boundId].Points[i];
                    pointStatuses[curPointId] = PointStatus.Boundary;

                    //сосед 1
                    int leftNeighId = offset + (bndPointCnt + (curPointId - offset) - 1) % bndPointCnt;
                    //сосед 2
                    int rightNeighId = offset + ((curPointId - offset) + 1) % bndPointCnt;
                    //соседние точки для текущей точки
                    boundaryEdges[curPointId] = new EdgeIndex(
                        curPointId, //ID текущей точки
                        leftNeighId,
                        rightNeighId,
                        boundaryContainer[boundId].ID
                        );

                    curPointId++;
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
            foreach (BoundaryNew innerBoundary in boundaryContainer.InnerBoundaries)
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
            foreach (BoundaryNew innerBoundary in boundaryContainer.InnerBoundaries)
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

            int[] possibleValues = { (int)TriangleInfect.External, (int)TriangleInfect.Internal };
            //определение принадлежности области "нулевого" треугольника
            for (int triangleId = 0; triangleId < Triangles.Length; triangleId++)
            {
                //принадлежность треугольника области уже определена
                if (possibleValues.Contains(Triangles[triangleId].flag))
                    continue;
                int triangleInfectCnt = InfectTriangles(triangleId, possibleValues);
#if DEBUG
                Console.WriteLine($"TriangleId:{triangleId};\tЗаражено: {triangleInfectCnt}");
#endif
            }
        }

        /// <summary>
        /// Определить принадлежность треугольников области на основе принадлежности
        /// <paramref name="triangleId"/>.
        /// </summary>
        /// <param name="triangleId">идентификатор треугольника</param>
        /// <param name="possibleValues">возможные значения принадлежности области</param>
        int InfectTriangles(int triangleId, int[] possibleValues)
        {
            //количество зараженных треугольников (включая нулевой)
            int triangleInfectCnt = 1;

            //определение принадлежности области
            bool isInArea = IsTriangleInArea(triangleId);
            //значение для заражения (по умолчанию треугольник - внешний)
            TriangleInfect infectValue = TriangleInfect.External;
            //входит в область
            if (isInArea)
                infectValue = TriangleInfect.Internal;
            Triangles[triangleId].flag = (int)infectValue;

            //стек заражения
            int[] infectionStack = null;
            MEM.Alloc(this.Triangles.Length, ref infectionStack, -1);
            //индекс текущей пустой ячейки стека
            int currentStackId = 0;
            //размещаем в стеке нулевой треугольник
            infectionStack[currentStackId++] = triangleId;

            //начинаем заражение, счетчик может наращиваться внутри цикла
            for (currentStackId = 1; currentStackId > 0;)
            {
                //достаем из стека верхний (последний) треугольник
                triangleId = infectionStack[--currentStackId];
                //зануляем значение в стеке
                infectionStack[currentStackId] = -1;
                infectValue = (TriangleInfect)Triangles[triangleId].flag;

                //проход по вершинам (ребрам) треугольника
                for (int localVertex = 0; localVertex < 3;)
                {
                    //глобальный индекс вершины треугольника
                    int globalKnotId = Triangles[triangleId][localVertex];
                    int halfEdge = HalfEdges[triangleId * 3 + localVertex];
                    //смежный треугольник
                    int adjacentTriangleId = halfEdge / 3;

                    //пропуск, если треугольник уже был обработан
                    //или нет смежного треугольника
                    if (halfEdge == -1 || possibleValues.Contains(Triangles[adjacentTriangleId].flag))
                    {
                        localVertex++;
                        continue;
                    }

                    //помещаем текущий треугольник в стек заражения
                    infectionStack[currentStackId++] = triangleId;
                    //индексы вершин смежного ребра между двумя треугольниками
                    int vertexIdStart = Triangles[triangleId][localVertex];
                    int vertexIdEnd = Triangles[adjacentTriangleId][halfEdge % 3];

                    int halfEdgeStart = triangleId * 3 + localVertex;
                    int halfEdgeEnd = HalfEdges[halfEdgeStart];
                    //в качестве текущего треугольника устанавливаем смежный
                    triangleId = adjacentTriangleId;

                    //проверка - является ли смежное ребро граничным
                    if (
                        //обе точки являются граничными
                        (pointStatuses[vertexIdStart] == PointStatus.Boundary ||
                        pointStatuses[vertexIdEnd] == PointStatus.Boundary) &&
                        //первая точка имеет соседа - вторую точку
                        boundaryEdges[vertexIdStart].Adjacents.Contains(vertexIdEnd)
                    )
                    {
                        if (infectValue == TriangleInfect.External)
                            infectValue = TriangleInfect.Internal;
                        else
                            infectValue = TriangleInfect.External;
                    }
                    triangleInfectCnt++;
                    Triangles[triangleId].flag = (int)infectValue;
                    localVertex = 0;
                }
                //удаляем связи с треугольником, не входящим в область
                if (Triangles[triangleId].flag == (int)TriangleInfect.External)
                    UnLinkTriangle(triangleId);
            }
            return triangleInfectCnt;
        }

        /// <summary>
        /// Удалить связи с треугольником. По сути очистить его из <see cref="HalfEdges"/>
        /// </summary>
        /// <param name="triangleId"></param>
        void UnLinkTriangle(int triangleId)
        {
            //проходим по его полуребрам
            for (int halfEdge = triangleId * 3; halfEdge < triangleId * 3 + 3; halfEdge++)
            {
                //пара к этому полуребру
                int secondHalfEdge = HalfEdges[halfEdge];
                UnLink(halfEdge, secondHalfEdge);
            }
        }

        /// <summary>
        /// Разорвать связь между 2 полуребрами
        /// </summary>
        /// <param name="edge1"></param>
        /// <param name="edge2"></param>
        void UnLink(int edge1, int edge2)
        {
            if (edge1 != -1)
                this.HalfEdges[edge1] = -1;
            if (edge2 != -1)
                this.HalfEdges[edge2] = -1;
        }

        /// <summary>
        /// Определить принадлежность треугольника области
        /// </summary>
        /// <param name="triangleId">id треугольника из <see cref="Triangles"/></param>
        /// <returns>true - принаделжит области, иначе - false</returns>
        bool IsTriangleInArea(int triangleId)
        {
            var triangle = Triangles[triangleId];
            (int i, int j, int k) = triangle.Get();

            //вычисляем принадлежность треугольника области
            double ctx = (coordsX[i] + coordsX[j] + coordsX[k]) / 3;
            double cty = (coordsY[i] + coordsY[j] + coordsY[k]) / 3;
            HPoint ctri = new HPoint(ctx, cty);

            bool isInArea = IsInArea(ctri);
            return isInArea;
        }
    }
}
