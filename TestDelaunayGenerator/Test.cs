using CommonLib;
using CommonLib.Geometry;
using DelaunayGenerator;
using GeometryLib.Aalgorithms;
using GeometryLib.Vector;
using MeshLib;
using RenderLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using TestDelaunayGenerator.Areas;
using TestDelaunayGenerator.Boundary;

namespace TestDelaunayGenerator
{
    public class Test
    {
        IHPoint[] points = null;
        IHPoint[] Boundary = null;
        IHPoint[] Boundary2 = null;
        public Test() { }
        public void CreateRestArea(int idx)
        {
            const int N = 100;
            double h = 3.0 / (N - 1);
            switch (idx)
            {
                case 0:
                    Boundary = null;
                    points = new IHPoint[5]
                    {
                        new HPoint(0, 0),
                        new HPoint(1, 0),
                        new HPoint(1, 1),
                        new HPoint(0, 1),
                        new HPoint(0.5, 0.5)
                    };
                    break;
                case 1:
                    Boundary = null;
                    // массивы для псевдослучайного микро смещения координат узлов
                    double[] dxx = {0.0000001, 0.0000005, 0.0000002, 0.0000006, 0.0000002,
                            0.0000007, 0.0000003, 0.0000001, 0.0000004, 0.0000009,
                            0.0000000, 0.0000003, 0.0000006, 0.0000004, 0.0000008 };
                    double[] dyy = { 0.0000005, 0.0000002, 0.0000006, 0.0000002, 0.0000004,
                             0.0000007, 0.0000003, 0.0000001, 0.0000001, 0.0000004,
                             0.0000009, 0.0000000, 0.0000003, 0.0000006,  0.0000008 };
                    int idd = 0;
                    points = new IHPoint[N * N];
                    for (int i = 0; i < N; i++)
                        for (int j = 0; j < N; j++)
                        {
                            // тряска координат
                            points[i * N + j] = new HPoint(h * i + dxx[idd], h * j + dyy[idd]);
                            //  points[i * N + j] = new HPoint(h * i, h * j );
                            idd++;
                            idd = idd % dxx.Length;
                        }
                    Boundary = new IHPoint[4]
                        {
                            new HPoint(1,1),
                            new HPoint(1, 2),
                            new HPoint(2, 2),
                            new HPoint(2 ,1)
                        };
                    Boundary2 = new IHPoint[4]
                        {
                            new HPoint(1.5,1.5),
                            new HPoint(1.5, 1.7),
                            new HPoint(1.7, 1.7),
                            new HPoint(1.7 ,1.5)
                        };

                    break;
                case 2:

                    points = new IHPoint[N * N];
                    for (int i = 0; i < N; i++)
                    {
                        double hx = h - (h / 3 * i) / N;
                        for (int j = 0; j < N; j++)
                            points[i * N + j] = new HPoint(h * i, hx * j);
                    }
                    Boundary = null;
                    Boundary = new IHPoint[5]
                    {
                            new HPoint(-0.1,-0.1),
                            new HPoint(0.5,0.25),
                            new HPoint(1.1,-0.1),
                            new HPoint(1.1,0.7),
                            new HPoint(-0.1,0.7),
                            //new HPoint(-0.1,-0.1)
                     };
                    break;
                case 3:
                    {
                        Boundary = null;
                        var width = 100;
                        var height = 100;
                        List<Vector2> samples = CircleDelaunayGenerator.SampleCircle(new Vector2(width / 2, height / 3), 220, 3);
                        points = new IHPoint[samples.Count];
                        for (int i = 0; i < samples.Count; i++)
                            points[i] = new HPoint(samples[i].X, samples[i].Y);
                    }
                    break;
                case 4:
                    {
                        Boundary = null;
                        var width = 100;
                        var height = 100;
                        Boundary = new IHPoint[3]
                        {
                            new HPoint(0,0),
                            new HPoint(0,width),
                            new HPoint(height,width)
                        };
                        List<Vector2> samples = CircleDelaunayGenerator.SampleCircle(new Vector2(width / 2, height / 3), 220, 3);
                        points = new IHPoint[samples.Count];
                        for (int i = 0; i < samples.Count; i++)
                            points[i] = new HPoint(samples[i].X, samples[i].Y);
                    }
                    break;
                case 5:
                    {
                        var width = 100;
                        var height = 100;
                        Boundary = new IHPoint[4]
                        {
                            new HPoint(0,0),
                            new HPoint(0,height),
                            new HPoint(width,height),
                            new HPoint(width,0)
                        };
                        List<Vector2> samples = CircleDelaunayGenerator.SampleCircle(new Vector2(width / 2, height / 3), 220, 3);
                        points = new IHPoint[samples.Count];
                        for (int i = 0; i < samples.Count; i++)
                            points[i] = new HPoint(samples[i].X, samples[i].Y);
                    }
                    break;
            }
        }
        public void Run()
        {
            //старая триангуляция
            //DMeshGenerator delaunator = new DMeshGenerator();
            //delaunator.Generator(points, Boundary);


            IHPoint[] workingPoints = (IHPoint[])points.Clone();
            //новая триангуляция
            BoundaryContainer boundaryContainer = null;
            if (Boundary != null)
            {
                boundaryContainer = new BoundaryContainer();
                boundaryContainer.ReplaceOuterBoundary(Boundary, new GeneratorFixed());
                //boundaryContainer.AddInnerBoundary(Boundary2, new GeneratorFixed());
                IHPoint[] boundaryPoints = boundaryContainer.AllBoundaryPoints;

                // Объединяем points и boundaryPoints
                int exPointsLength = workingPoints.Length;
                Array.Resize(ref workingPoints, workingPoints.Length + boundaryPoints.Length);
                boundaryPoints.CopyTo(workingPoints, exPointsLength);
            }

            DelaunayMeshGenerator delaunator = new DelaunayMeshGenerator(workingPoints, boundaryContainer);
            delaunator.PreFilterPoints(true);
            delaunator.Generator();





            //IHPoint[] workingPoints = (IHPoint[])points.Clone();

            //// Создаем BoundaryContainer, если граница задана
            //BoundaryContainer boundaryContainer = null;
            //if (Boundary != null && Boundary.Length > 0)
            //{

            //    boundaryContainer = BoundaryContainer.CreateWithBoundary(Boundary, new GeneratorFixed(20)); // Используем новый статический метод
            //    boundaryContainer.Add(Boundary2);
            //    IHPoint[] boundaryPoints = boundaryContainer.AllBoundaryKnots;

            //    // Объединяем points и boundaryPoints
            //    int exPointsLength = workingPoints.Length;
            //    Array.Resize(ref workingPoints, workingPoints.Length + boundaryPoints.Length);
            //    boundaryPoints.CopyTo(workingPoints, exPointsLength);
            //}

            //// Создаем генератор триангуляции
            //DelaunayMeshGenerator delaunator = new DelaunayMeshGenerator(workingPoints, boundaryContainer);


            //delaunator.PreFilterPoints(true);
            //delaunator.Generator();





            IMesh mesh = delaunator.CreateMesh();

            IConvexHull ch = new ConvexHull();
            // ch.FindHull(points, )
            ShowMesh(mesh);
        }


        public void Run(AreaBase area, bool usePointsFilter = true, int count = 1, bool openForm = true)
        {
            //for (int i = 0; i < count; i++)
            //{
            //    IHPoint[] points = area.Points;
            //    BoundaryContainer_Old boundaryContainer = area.BoundaryContainer;
            //    //TODO переместить в AreaBase
            //    //если граница задана, то расширяем исходное множество узлов множеством граничных узлов
            //    if (boundaryContainer != null)
            //    {
            //        IHPoint[] boundary = boundaryContainer.AllBoundaryKnots;
            //        int exPointsLength = points.Length;
            //        Array.Resize(ref points, points.Length + boundary.Length);
            //        boundary.CopyTo(points, exPointsLength);
            //    }

            //    DelaunayMeshGenerator delaunator = new DelaunayMeshGenerator(points, boundaryContainer, usePointsFilter);
            //    //измерение времени предварительной фильтрации
            //    Stopwatch watch = Stopwatch.StartNew();
            //    delaunator.PreFilterPoints();
            //    double filterPointsSeconds = watch.Elapsed.TotalSeconds;

            //    //измерение времени генерации сетки
            //    watch = Stopwatch.StartNew();
            //    delaunator.Generator();
            //    double genSeconds = watch.Elapsed.TotalSeconds;

            //    //фильтрация треугольников
            //    watch = Stopwatch.StartNew();
            //    IMesh mesh = delaunator.CreateMesh();
            //    double filterSeconds = watch.Elapsed.TotalSeconds;

            //    //TriangulationLog log = new TriangulationLog(area, mesh, filterPointsSeconds, genSeconds, filterSeconds, usePointsFilter);
            //    //Log.Information(log.ToString());
            //    //if (specialLogger != null)
            //    //    specialLogger.Information("{@info}", log);

            //    //отобразить форму
            //    if (openForm)
            //        ShowMesh(mesh);
            //}

        }

        protected void ShowMesh(IMesh mesh)
        {
            if (mesh != null)
            {
                SavePoint data = new SavePoint();
                data.SetSavePoint(0, mesh);
                double[] xx = mesh.GetCoords(0);
                double[] yy = mesh.GetCoords(1);
                data.Add("Координата Х", xx);
                data.Add("Координата Y", yy);
                data.Add("Координаты ХY", xx, yy);
                Form form = new ViForm(data);
                form.ShowDialog();
            }
        }

    }
}
