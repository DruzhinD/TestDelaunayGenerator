using CommonLib;
using CommonLib.Geometry;
using GeometryLib.Vector;
using MeshLib;
using RenderLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TestDelaunayGenerator;
using TestDelaunayGenerator.Boundary;
using TestDelaunayGenerator.DCELMesh;

namespace DelaunayUI
{
    public class Test
    {
        /// <summary>
        /// Каталог data/ в решении
        /// </summary>
        string ProjectPath => Path.Combine(Directory.GetCurrentDirectory(), @"..\..\", @"data\");

        /// <summary>
        /// Название сетки/области
        /// </summary>
        public string Name = "н-д";

        public IHPoint[] points = null;
        //внешняя оболочка
        public IHPoint[] outerBoundary = null;
        //внутренняя оболочка
        public List<IHPoint[]> innerBoundaries = new List<IHPoint[]>();
        //генератор для граничных точек
        public IGeneratorBase generator = new GeneratorFixed(0);
        /// <summary>
        /// граничный контур
        /// </summary>
        public BoundaryContainer container = null;

        /// <summary>
        /// true - логгировать построение (scope - в рамках класса Test)
        /// </summary>
        bool useLogger = true;
        public Test(bool useLogger = true)
        {
            this.useLogger = useLogger;
        }


        /// <summary>
        /// Инициализация конфигурации области построения на основе индекса
        /// </summary>
        public void CreateRestArea(int idx)
        {
            //true - использовать граничный контур
            bool useBoundary = false;
            int N = 50;
            double h = 3.0 / (N - 1);
            switch (idx)
            {
                case 0:
                    Name = "simple_square";
                    useBoundary = true;
                    //outerBoundary = new IHPoint[]
                    //{
                    //    new HPoint(0.1, 0.1),
                    //    new HPoint(0.1, 0.91),
                    //    new HPoint(0.9, 0.91),
                    //    new HPoint(0.9, 0.1),
                    //};
                    outerBoundary = new IHPoint[]
                    {
                        new HPoint(0.1, 0.5),
                        new HPoint(0.1, 0.91),
                        new HPoint(0.9, 0.91),
                        new HPoint(0.9, 0.5),
                    };
                    innerBoundaries.Add(
                        new IHPoint[]
                        {
                            new HPoint(0.3, 0.6),
                            new HPoint(0.31, 0.8),

                            new HPoint(0.7, 0.8),
                            new HPoint(0.7, 0.6),
                            new HPoint(0.54, 0.6),
                        }
                    );
                    generator = new GeneratorFixed(0);
                    points = new IHPoint[]
                    {
                        new HPoint(0.45, 0.85),
                        new HPoint(0.63, 0.83),
                        new HPoint(0, 0),
                        new HPoint(1, 0),
                        new HPoint(1, 1),
                        new HPoint(0, 1),
                        new HPoint(0.5, 0.5),
                        new HPoint(0.4, 0.5),
                        new HPoint(0.7, 0.85),
                        //new HPoint(0.55, 0.54),
                        new HPoint(0.31, 0.58),
                        new HPoint(0.7, 0.3),

                        //
                        //new HPoint(0.63, 0.83),
                        //new HPoint(0.42, 0.83),
                    };
                    break;
                case 1:
                    Name = "bigger_square";
                    useBoundary = true;
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
                    generator = new GeneratorFixed(0);
                    outerBoundary = new IHPoint[]
                        {
                            new HPoint(1.1,1.1),
                            new HPoint(1.3, 1.6),
                            new HPoint(1.1, 2.1),
                            new HPoint(2.1, 2.1),
                            new HPoint(2.1 ,1.1)
                        };
                    innerBoundaries.Add(
                        new IHPoint[4]
                        {
                            new HPoint(1.5,1.5),
                            new HPoint(1.5, 1.7),
                            new HPoint(1.7, 1.7),
                            new HPoint(1.7 ,1.5)
                        }
                    );

                    break;
                //трапеция
                case 2:
                    Name = "trapezoid";
                    useBoundary = true;
                    points = new IHPoint[N * N];
                    for (int i = 0; i < N; i++)
                    {
                        double hx = h - (h / 3 * i) / N;
                        for (int j = 0; j < N; j++)
                            points[i * N + j] = new HPoint(h * i, hx * j);
                    }
                    outerBoundary = null;
                    outerBoundary = new IHPoint[5]
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
                        Name = "circle";
                        useBoundary = false;
                        outerBoundary = null;
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
                        Name = "circle2";
                        useBoundary = true;
                        var width = 100;
                        var height = 100;
                        outerBoundary = new IHPoint[3]
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
                        Name = "circle3";
                        useBoundary = true;
                        var width = 100;
                        var height = 100;
                        outerBoundary = new IHPoint[4]
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
                case 6:
                    {
                        Name = "default";
                        useBoundary = false;
                        points = new IHPoint[N * N];
                        var rnd = new Random();
                        for (int i = 0; i < points.Length; i++)
                            points[i] = new HPoint(rnd.NextDouble(), rnd.NextDouble());
                    }
                    break;
            }

            if (useBoundary)
            {
                container = new BoundaryContainer();
                container.ReplaceOuterBoundary(outerBoundary, generator);
                foreach (var inner in innerBoundaries)
                    container.AddInnerBoundary(inner, generator);
            }
        }

        /// <summary>
        /// Инициализация области построения триангуляции для бенчмарков
        /// </summary>
        /// <param name="pointCnt">количество точек</param>
        /// <param name="boundVertexCnt">количество опорных точек граничного контура</param>
        /// <param name="generator">генератор промежуточных граничных вершин</param>
        /// <param name="percentPointsPerEdge">
        /// относительное количество (процент) промежуточных граничных точек от исходного количества точек
        /// </param>
        public void CreateBenchmarkTestArea(
            int pointCnt,
            int boundVertexCnt = 0,
            IGeneratorBase generator = null,
            double percentPointsPerEdge = 0.00)
        {
            // массивы для псевдослучайного микро смещения координат узлов
            double[] dxx = {0.0000001, 0.0000005, 0.0000002, 0.0000006, 0.0000002,
                            0.0000007, 0.0000003, 0.0000001, 0.0000004, 0.0000009,
                            0.0000000, 0.0000003, 0.0000006, 0.0000004, 0.0000008 };
            double[] dyy = { 0.0000005, 0.0000002, 0.0000006, 0.0000002, 0.0000004,
                             0.0000007, 0.0000003, 0.0000001, 0.0000001, 0.0000004,
                             0.0000009, 0.0000000, 0.0000003, 0.0000006,  0.0000008 };
            
            int N = (int)Math.Sqrt(pointCnt);
            double edgeLen = 1.0; //длина ребра квадрата
            double pointIncrement = edgeLen / N; //расстояние между точками по одной координате
            points = new IHPoint[N * N];
            
            int idd = 0;
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    // тряска координат
                    points[i * N + j] = new HPoint(pointIncrement * i + dxx[idd], pointIncrement * j + dyy[idd]);
                    idd++;
                    idd = idd % dxx.Length;
                }

            IHPoint center = new HPoint(0.5, 0.5);
            //граница
            if (boundVertexCnt < 2)
                return;

            //outerBoundary = TruePolygonVertices(edgeLen / 2, 4, center);
            outerBoundary = Star(boundVertexCnt / 2, edgeLen / 5, edgeLen / 4, 45, center);
            //outerBoundary = TruePolygonVertices(edgeLen / 4, boundVertexCnt, new HPoint(edgeLen/2, edgeLen/2));
            container = new BoundaryContainer();
            if (generator is null)
                generator = new GeneratorFixed(0);
            container.ReplaceOuterBoundary(outerBoundary, generator);
        }

        /// <summary>
        /// Генератор правильного многоугольника
        /// </summary>
        /// <param name="radius"></param>
        /// <param name="vertexesCnt"></param>
        /// <param name="center"></param>
        /// <returns></returns>
        static IHPoint[] TruePolygonVertices(double radius, int vertexesCnt, IHPoint center)
        {
            var vertexes = new IHPoint[vertexesCnt];

            for (int i = 0; i < vertexesCnt; i++)
            {
                double theta = 2 * Math.PI * i / vertexesCnt;
                double x = center.X + radius * Math.Cos(theta);
                double y = center.Y + radius * Math.Sin(theta);
                vertexes[i] = new HPoint(x, y);
            }
            return vertexes;
        }

        public IHPoint[] Star(int vertexCnt, double innerR, double externalR, double alpha, IHPoint center)
        {
            IHPoint[] vertexes = new IHPoint[2 * vertexCnt];
            double a = alpha;
            double da = Math.PI / vertexCnt;
            double l;
            for (int k = 0; k < 2 * vertexCnt ; k++)
            {
                l = k % 2 == 0 ? externalR : innerR;
                vertexes[k] = new HPoint(center.X + l * Math.Cos(a), center.Y + l * Math.Sin(a));
                a += da;
            }
            return vertexes;
        }

        public Delaunator Run(bool showForm = true, bool serialize = false, DelaunatorConfig config = null)
        {
            if (config is null)
                config = new DelaunatorConfig()
                {
                    IncludeExtTriangles = false,
                    RestoreBorder = true,
                    UseClippingPoints = false,
                    ParallelClippingPoints = false,
                };

            Console.Title = Name;
            if (useLogger)
            {

                LoggerConfig();
                Log.Information($"Запуск {DateTime.Now}");
                Log.Information($"Количество точек:{points.Length}");
                Log.Information($"Внешняя граница:{outerBoundary != null}");
                Log.Information($"Внутренняя граница:{innerBoundaries != null}");

                if (container != null)
                {
                    Log.Information($"Внешняя граница. " +
                        $"Количество точек:{container.OuterBoundary.Points.Length}");
                    Log.Information($"Внутренняя граница. " +
                        $"Количество контуров:{container.InnerBoundaries.Count}; " +
                        $"Количество точек:{string.Join(",", container.InnerBoundaries.Select(b => b.Points.Length))}");

                }
                Log.Information($"Конфигурация: {config}");
            }

            Delaunator delaunator = new Delaunator(points, container, config);
            Stopwatch sw = Stopwatch.StartNew();
            delaunator.Generate();
            sw.Stop();
            var mesh = delaunator.ToMesh();

            if (useLogger)
                Log.Information($"Общее количество точек:{delaunator.Points.Length}. Время выполнения:{sw.Elapsed.TotalSeconds}(c)");

            IRestrictedDCEL dcel = delaunator.ToRestrictedDCEL();
            if (serialize)
            {
                Console.WriteLine("Выполняется сериализация...");
                string path = Path.Combine(ProjectPath, Name + ".dcel");
                SerializerDCEL.SerializeXML((RestrictedDCEL)dcel, path);
                Console.WriteLine("Сериализация выполнена!");
            }

            if (showForm)
                ShowMesh(mesh);

            return delaunator;
        }

        /// <summary>
        /// Отрисовка сетки в визуализаторе, загруженной из xml файла (десериализация)
        /// </summary>
        /// <param name="path"></param>
        public void Run(string path)
        {
            Console.Title = $"XML+{path}";
            RestrictedDCEL dcel = SerializerDCEL.DeserializeXML(path);
            var mesh = dcel.ToDcelTriMesh();
            ShowMesh(mesh);
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
            Console.Title = "Delaunator Menu";
        }

        /// <summary>
        /// Конфигурация логгера
        /// </summary>
        protected void LoggerConfig()
        {
            string filePath = Path.Combine(ProjectPath, "logs.log");
            string logTemplate = "[{Timestamp:dd.MM.yy HH:mm:ss} {Level:u4}] {Message:lj}{NewLine}{Exception}";
            Log.Logger = new LoggerConfiguration()
                //уровень
                .MinimumLevel.Debug()
                //запись в консоль
                .WriteTo.Console(
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug,
                    outputTemplate: logTemplate,
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code

                )
                //запись в файл
                .WriteTo.File(
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                    outputTemplate: logTemplate,
                    path: filePath,
                    fileSizeLimitBytes: 1 * 1024 * 1024 * 10 //10MB

                )
                .CreateLogger();
        }
    }
}
