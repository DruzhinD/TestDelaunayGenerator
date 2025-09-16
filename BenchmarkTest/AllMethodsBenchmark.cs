using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Toolchains;
using CommonLib.Geometry;
using DelaunayUI;
using PseudoRegularGrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator;
using TestDelaunayGenerator.Boundary;

namespace BenchmarkTest
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, invocationCount: 1, iterationCount: 10, warmupCount: 0)]
    [MedianColumn]
    [Config(typeof(CustomConfig))]
    [JsonExporterAttribute.Full]

    public class AllMethodsBenchmark
    {
        protected class CustomConfig : ManualConfig
        {
            public CustomConfig()
                : base()
            {
                // Добавляем метку времени к пути с артефактами
                //изменяем путь к каталогу
                ArtifactsPath = $"{nameof(AllMethodsBenchmark)}_{DateTime.Now:yyyyMMdd_HHmmss}";
            }
        }

        public Test test;
        public Delaunator delaunator;
        public DelaunatorConfig delaunatorConfig;

        IHPoint[] points;
        BoundaryContainer container;
        double edgeLen = 1.0;
        #region Параметры
        [ParamsSource(nameof(PointCntValues))]
        public int PointCount { get; set; }

        public IEnumerable<int> PointCntValues
        {
            get
            {
                List<int> values = new List<int>();

                int startCnt = 100_000;
                int limit = 100_000;
                int increment = limit / 2;

                for (int p = startCnt; p <= limit; p += increment)
                    values.Add(p);
                return values;
            }
        }

        [ParamsSource(nameof(BoundaryVertexCountValues))]
        public int BoundaryVertexCount { get; set; }
        public IEnumerable<int> BoundaryVertexCountValues
        {
            get
            {
                List<int> values = new List<int>();
                int startCnt = 10;
                int limit = 100;
                int increment = 10;

                for (int p = startCnt; p < limit + 1; p += increment)
                    values.Add(p);
                return values;
            }
        }

        /// <summary>
        /// Часть от исходного количества точек, которая останется после отсечения точек
        /// </summary>
        [ParamsSource(nameof(PartAfterClipPointsValues))]
        public double PartAfterClipPoints { get; set; }
        public IEnumerable<double> PartAfterClipPointsValues
        {
            get
            {
                List<double> values = new List<double>();
                double startCnt = 0.5;
                double limit = 0.5;
                double increment = 0.1;

                for (double p = startCnt; p <= limit; p += increment)
                    values.Add(p);
                return values;
            }
        }
        #endregion

        Test CreateTest(int pointsPerEdge)
        {
            var test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(pointsPerEdge));
            return test;
        }

        #region подготовка к итерации
        [GlobalSetup()]
        public void PointsSetup()
        {
            test = new Test();
            int n = (int)Math.Sqrt(PointCount);
            points = new PseudoRegularGridGenerator()
                .GenerateGrid(n, n, (double)edgeLen / Math.Sqrt(PointCount), 0.05)
                .ToArray();
        }

        [IterationSetup(Targets = new string[] {
            nameof(OnlyClippingTriangles),
            nameof(ClippingPointsWithoutRestoreBorder)
        })]
        public void InitBoundaryWithGenerator()
        {
            int pointsPerEdge = (int)(0.025 * PointCount / BoundaryVertexCount);
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(pointsPerEdge), PartAfterClipPoints);
            //points.CopyTo(test.points, 0);
        }


        //без промежуточных вершин на ребрах
        [IterationSetup(Targets = new string[] {
            nameof(RestoreBorderWithoutClippingPoints),
            nameof(AllMethods)
        })]
        public void InitWithoutBetweenPoints()
        {
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(0), PartAfterClipPoints);
            //points.CopyTo(test.points, 0);
        }

        #endregion

        #region Методы

        [Benchmark(Description = "восстановление границы, отсечение треугольников, без отсечения точек", Baseline = true)]
        public void RbBase()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = false,
                ParallelClippingPoints = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
            //delaunator = new Delaunator(points, container, delaunatorConfig);
            //delaunator.Generate();
        }


        [Benchmark(Description = "без восстановление границы, отсечение треугольников, без отсечения точек")]

        public void NonRbNonCp()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = false,
                UseClippingPoints = false,
                ParallelClippingPoints = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
            //delaunator = new Delaunator(points, container, delaunatorConfig);
            //delaunator.Generate();
        }

        [Benchmark(Description = "восстановление границы, без отсечения точек")]
        public void RestoreBorderWithoutClippingPoints()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = false,
                ParallelClippingPoints = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
            //delaunator = new Delaunator(points, container, delaunatorConfig);
            //delaunator.Generate();
        }

        [Benchmark(Description = "отсечение точек (однопоточное), восстановление границы")]
        public void AllMethods()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = true,
                ParallelClippingPoints = true
            };
            test.Run(showForm: false, config: delaunatorConfig);
            //delaunator = new Delaunator(points, container, delaunatorConfig);
            //delaunator.Generate();
        }

        [Benchmark(Description = "без восстановление границы, отсечение треугольников, отсечение точек (однопоточное)")]
        public void NonRbClipPointSingleThread()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = false,
                UseClippingPoints = true,
                ParallelClippingPoints = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
            //delaunator = new Delaunator(points, container, delaunatorConfig);
            //delaunator.Generate();
        }

        [Benchmark(Description = "отсечение точек (однопоточное), без восстановления границы")]
        public void ClippingPointsWithoutRestoreBorder()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = false,
                UseClippingPoints = true,
                ParallelClippingPoints = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
            //delaunator = new Delaunator(points, container, delaunatorConfig);
            //delaunator.Generate();
        }
    }
}
