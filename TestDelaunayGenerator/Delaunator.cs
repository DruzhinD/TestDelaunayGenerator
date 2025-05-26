using CommonLib;
using CommonLib.Geometry;
using MemLogLib;
using MeshLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.Boundary;

namespace TestDelaunayGenerator
{
    public class Delaunator
    {
        #region Базовые поля, свойства
        /// <summary>
        /// Узлы триангуляции
        /// </summary>
        IHPoint[] points;

        /// <summary>
        /// Узлы триангуляции
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

        public Delaunator(IHPoint[] points, BoundaryContainer boundaryContainer = null)
        {
            //проверка на null
            if (points is null || points.Length < 3)
                throw new ArgumentException($"{nameof(points)} должен содержать минимум 3 точки!");

            this.points = points;
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
        /// Обход вершин всех треугольников направлен против ч.с.
        /// </summary>
        Troika[] Triangles;
        #endregion


        public IMesh ToMesh(bool debug = false)
        {
            //инициализация объекта сетки и выделение памяти
            TriMesh mesh = new TriMesh();
            //MEM.Alloc(Triangles.Length, ref mesh.AreaElems);

            //TODO вызов заражения
            mesh.AreaElems = this.Triangles.Select(triangle => triangle.GetTri).ToArray();
            mesh.CoordsX = this.coordsX;
            mesh.CoordsY = this.coordsY;

            //формирование граничных точек и ребер

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


            //TODO помечать граничные ребра и узлы

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
            AddTriangle(i0, i1, i2, -1, -1, -1);
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
            //TODO поправить массив граничных узлов
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
            //обрезка триангуляционны= массивов
            HalfEdges = HalfEdges.Take(triangleVertexCounter).ToArray();

            #endregion
        }

        #region Логика генерации триангуляции делоне по S-hull
        /// <summary>
        /// рекурсивная перестройки треугольников от точки к точке,
        /// пока они не удовлетворят условию Делоне 
        /// </summary>
        /// <param name="EdgeA_ID">индекс 3-ей вершины треугольника в массиве <see cref="Triangles"/></param>
        /// <returns>индекс 2-ой (средней) вершины треугольника</returns>
        private int Legalize(int EdgeA_ID)
        {
            var i = 0;
            int ar = -1;

            // рекурсия устранена с помощью стека фиксированного размера
            while (true)
            {
                //TODO Пропускаем легализацию, если ребро является граничным
                var EdgeB_ID = HalfEdges[EdgeA_ID];


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

                // адрес - смешение для 1 треугольника (1-ый индекс в треугольнике)
                int triA_ID = EdgeA_ID - EdgeA_ID % 3;
                ar = triA_ID + (EdgeA_ID + 2) % 3;

                //если смежный треугольник не был найден (т.е. -1), то достаем следующий из стека
                if (EdgeB_ID == -1)
                {
                    // граница выпуклой оболочки 
                    if (i == 0)
                        break;
                    EdgeA_ID = EdgeStack[--i];
                    continue;
                }

                int al = triA_ID + (EdgeA_ID + 1) % 3;
                // адрес - смешение для 2 треугольника
                int triB_ID = EdgeB_ID - EdgeB_ID % 3;
                int bl = triB_ID + (EdgeB_ID + 2) % 3;

                //индексы вершин двух смежных треугольников 
                int idxElemA = triA_ID / 3;
                int idxElemB = triB_ID / 3;
                int p0 = Triangles[idxElemA][(EdgeA_ID + 2) % 3];
                int pr = Triangles[idxElemA][(EdgeA_ID + 0) % 3];
                int pl = Triangles[idxElemA][(EdgeA_ID + 1) % 3];
                //вершина смежного треугольника
                int p1 = Triangles[idxElemB][(EdgeB_ID + 2) % 3];

                bool illegal = InCircle(p0, pr, pl, p1);
                if (illegal)
                {
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

                    //TODO Обновляем статус граничных ребер после флипа

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
        /// <returns>возвращает индекс треугольника в <see cref="Triangles"/></returns>
        private int AddTriangle(int i0, int i1, int i2, int a, int b, int c)
        {
            //индекс треугольника
            int triangleId = triangleVertexCounter / 3;
            Triangles[triangleId].i = i0;
            Triangles[triangleId].j = i1;
            Triangles[triangleId].k = i2;

            //индекс первой вершины, в крайнем треугольнике
            //относительно массива точек
            triangleId = triangleVertexCounter;

            //TODO Проверяем, являются ли ребра треугольника граничными

            //связываем новый треугольник со смежными ему
            Link(triangleId, a);
            Link(triangleId + 1, b);
            Link(triangleId + 2, c);
            triangleVertexCounter += 3;

            return triangleId;
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
                HalfEdges[b] = EdgesID;
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
    }
}
