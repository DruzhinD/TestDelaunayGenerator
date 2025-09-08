using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Toolchains;
using DelaunayUI;
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

        #region Параметры
        [ParamsSource(nameof(PointCntValues))]
        public int PointCount { get; set; }

        public IEnumerable<int> PointCntValues
        {
            get
            {
                List<int> values = new List<int>();

                int startCnt = 200_000;
                int limit = 200_000;
                int increment = limit / 2;

                for (int p = startCnt; p < limit + 1; p += increment)
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
        #endregion

        Test CreateTest(int pointsPerEdge)
        {
            var test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(pointsPerEdge));
            return test;
        }

        #region подготовка к итерации
        //для отсечения треугольников требуется больше точек на ребре
        [IterationSetup(Targets = new string[] {
            nameof(OnlyClippingTriangles),
            nameof(ClippingPointsWithoutRestoreBorder)
        })]
        public void InitBoundaryWithGenerator()
        {
            int pointsPerEdge = (int)(0.025 * PointCount / BoundaryVertexCount);
            test = CreateTest(pointsPerEdge);
        }


        //без промежуточных вершин на ребрах
        [IterationSetup(Targets = new string[] {
            nameof(RestoreBorderWithoutClippingPoints),
            nameof(AllMethods)
        })]
        public void InitWithoutBetweenPoints()
        {
            test = CreateTest(0);
        }

        #endregion

        //только отсечение треугольников, без восстановления границы и без отсечения точек
        [Benchmark(Baseline = true, Description = "Только отсечение треугольников")]
        public void OnlyClippingTriangles()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = false,
                UseClippingPoints = false,
                ParallelClippingPoints = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
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
        }

        [Benchmark(Description = "отсечение точек (однопоточное), восстановление границы")]
        public void AllMethods()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = true,
                ParallelClippingPoints = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
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
        }
    }
}
