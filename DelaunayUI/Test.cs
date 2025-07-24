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

        public string Name = "н-д";

        public IHPoint[] points = null;
        //внешняя оболочка
        public IHPoint[] outerBoundary = null;
        //внутренняя оболочка
        public IHPoint[] innerBoundary = null;
        //генератор для граничных точек
        public IGeneratorBase generator = new GeneratorFixed();
        public Test() { }
        public void CreateRestArea(int idx)
        {
            const int N = 50;
            double h = 3.0 / (N - 1);
            switch (idx)
            {
                case 0:
                    Name = "simple_square";
                    outerBoundary = new IHPoint[]
                    {
                        new HPoint(0.1, 0.1),
                        //new HPoint(0.3, 0.61),
                        new HPoint(0.1, 0.91),
                        new HPoint(0.9, 0.91),
                        new HPoint(0.9, 0.1),
                    };
                    innerBoundary = new IHPoint[]
                    {
                        new HPoint(0.3, 0.6),
                        new HPoint(0.31, 0.8),
                        new HPoint(0.7, 0.8),
                        new HPoint(0.7, 0.6),
                    };
                    generator = new GeneratorFixed(1);
                    points = new IHPoint[]
                    {
                        new HPoint(0, 0),
                        new HPoint(1, 0),
                        new HPoint(1, 1),
                        new HPoint(0, 1),
                        //new HPoint(0.2, 0.7),
                        //new HPoint(0.25, 0.7),
                        //new HPoint(0.7, 0.27),
                        //new HPoint(0.7, 0.25),
                        new HPoint(0.5, 0.5),
                        new HPoint(0.4, 0.5),
                        new HPoint(0.7, 0.85),
                        //new HPoint(0.6, 0.83),
                        new HPoint(0.55, 0.54),
                        new HPoint(0.31, 0.58),
                        new HPoint(0.7, 0.3),
                    };
                    break;
                case 1:
                    Name = "bigger_square";
                    outerBoundary = null;
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
                    generator = new GeneratorFixed(15);
                    outerBoundary = new IHPoint[]
                        {
                            new HPoint(1.1,1.1),
                            new HPoint(1.3, 1.6),
                            new HPoint(1.1, 2.1),
                            new HPoint(2.1, 2.1),
                            new HPoint(2.1 ,1.1)
                        };
                    innerBoundary = new IHPoint[4]
                        {
                            new HPoint(1.5,1.5),
                            new HPoint(1.5, 1.7),
                            new HPoint(1.7, 1.7),
                            new HPoint(1.7 ,1.5)
                        };

                    break;
                //трапеция
                case 2:
                    Name = "trapezoid";
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
                        outerBoundary = null;
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
                        points = new IHPoint[N * N];
                        var rnd = new Random();
                        for (int i = 0; i < points.Length; i++)
                            points[i] = new HPoint(rnd.NextDouble(), rnd.NextDouble());
                    }
                    break;
            }
        }
        public Delaunator Run(bool showForm = true)
        {
            LoggerConfig();
            Log.Information($"Запуск {DateTime.Now}");
            Log.Information($"Количество точек:{points.Length}");
            Log.Information($"Внешняя граница:{outerBoundary != null}");
            Log.Information($"Внутренняя граница:{innerBoundary != null}");

            BoundaryContainer container = null;
            //инициализация границы, если заданы контура
            if (outerBoundary != null)
            {
                container = new BoundaryContainer();
                container.ReplaceOuterBoundary(outerBoundary, generator);
                Log.Information($"Внешняя граница. Количество точек:{container.OuterBoundary.Points.Length}");
                if (innerBoundary != null)
                {
                    container.AddInnerBoundary(innerBoundary, generator);
                    Log.Information($"Внутрення граница. Количество точек:{container.InnerBoundaries.Sum(b => b.Points.Length)}");
                }
            }
            Delaunator delaunator = new Delaunator(points, container);
            Stopwatch sw = Stopwatch.StartNew();
            delaunator.Generate();
            sw.Stop();
            var mesh = delaunator.ToMesh();
            Log.Information($"Общее количество точек:{delaunator.Points.Length}. Время выполнения:{sw.Elapsed.TotalSeconds}(c)");

            IRestrictedDCEL dcel = delaunator.ToRestrictedDCEL();
            //Console.WriteLine("Выполняется сериализация...");
            //string path = Path.Combine(SolutionDataPath, Name + ".dcel");
            //SerializerDCEL.SerializeXML((RestrictedDCEL)dcel, path);
            //Console.WriteLine("Сериализация выполнена!");

            if (showForm)
                ShowMesh(mesh);

            return delaunator;
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

        /// <summary>
        /// Конфигурация логгера
        /// </summary>
        protected void LoggerConfig()
        {
            string filePath = Path.Combine(ProjectPath, "logs.log");
            string logTemplate = "[{Timestamp:HH:mm:ss} {Level:u4}] {Message:lj}{NewLine}{Exception}";
            Log.Logger = new LoggerConfiguration()
                //уровень
                .MinimumLevel.Debug()
                //запись в консоль
                .WriteTo.Console(
                    outputTemplate: logTemplate,
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code
                )
                //запись в файл
                .WriteTo.File(
                    outputTemplate: logTemplate,
                    path: filePath,
                    fileSizeLimitBytes: 1*1024*1024*10 //10MB
                )
                .CreateLogger();
        }

    }
}
