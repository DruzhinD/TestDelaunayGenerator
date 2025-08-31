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
    /// <summary>
    /// Триангуляция с применением восстановления границы с опциональным использованием отсечения точек
    /// </summary>
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, invocationCount: 1, iterationCount: 10, warmupCount: 0)]
    [MedianColumn]
    [JsonExporterAttribute.Full]
    [Config(typeof(CustomConfig))]

    public class RestoreBorderBenchmark
    {
        protected class CustomConfig : ManualConfig
        {
            public CustomConfig()
                : base()
            {
                // Добавляем метку времени к пути с артефактами
                //изменяем путь к каталогу
                ArtifactsPath = $"RestoreBorderBenchmark_{DateTime.Now:yyyyMMdd_HHmmss}";
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

                int startCnt = 100_000;
                int limit = 100_000;
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

        #region Только отсечение треугольников
        //для отсечения треугольников требуется больше точек на ребре
        [IterationSetup(Targets = new string[] {
            nameof(OnlyClippingTriangles),
            //nameof(UseAllFeatures),
            //nameof(NonParallelClipping),
            //nameof(OnlyRestoreBorder),
        })]
        public void InitBoundaryWithGeneratorMore()
        {
            int pointsPerEdge = (int)(0.025 * PointCount / BoundaryVertexCount);
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(pointsPerEdge));
        }

        //только отсечение треугольников, без восстановления границы и без отсечения точек
        [Benchmark(Baseline = true, Description = "Только отсечение треугольников [2]")]
        public void OnlyClippingTriangles()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = false,
                UseClippingPoints = false,
                ParallelClippingPoints = false
            };
            ParamArray.delaunator = test.Run(showForm: false);
        }
        #endregion



        [IterationSetup(Targets = new string[]
        {
            nameof(UseAllFeatures),
            nameof(NonParallelClipping),
            nameof(OnlyRestoreBorder),
        }
        )]
        public void SetupTest()
        {
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(0));
        }

        [Benchmark(Description = "с параллельным отсечением точек")]
        public void UseAllFeatures()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = true,
                ParallelClippingPoints = true,
                IgnoreRestoreBorderException = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
        }


        [Benchmark(Description = "с однопоточным отсечением точек")]
        public void NonParallelClipping()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = true,
                ParallelClippingPoints = false,
                IgnoreRestoreBorderException = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
        }

        //неэффективен, поэтому вообще не учитываем метод
        //[Benchmark(Description = "только восстановление границы")]
        public void OnlyRestoreBorder()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = false,
                ParallelClippingPoints = false,
                IgnoreRestoreBorderException = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
        }
    }
}
