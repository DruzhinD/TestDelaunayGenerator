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
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, invocationCount: 1, iterationCount: 4, warmupCount: 0)]
    [MedianColumn]
    [Config(typeof(CustomConfig))]
    [JsonExporterAttribute.Full]
    public class TestClippingPoints
    {
        protected class CustomConfig : ManualConfig
        {
            public CustomConfig()
                : base()
            {
                // Добавляем метку времени к пути с артефактами
                //изменяем путь к каталогу
                ArtifactsPath = $"отсечение точек {DateTime.Now:yyyyMMdd_HHmmss}";
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

        #region подготовка к итерации


        [IterationSetup(Targets = new string[] {
            nameof(OldClippingPoints),
            nameof(OptimizedClippingPoints)
        })]
        public void InitData()
        {
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(0), 0);
        }
        #endregion

        [Benchmark(Description = "Старое отсечение точек (однопоток)", Baseline = true)]
        public void OldClippingPoints()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = true,
                RestoreBorder = false,
                UseClippingPoints = true,
                ParallelClippingPoints = false,
            };
            test.Run(false, false, delaunatorConfig);
        }

        [Benchmark(Description = "Оптимизированное отсечение точек (однопоток)")]
        public void OptimizedClippingPoints()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = true,
                RestoreBorder = false,
                UseClippingPoints = true,
                ParallelClippingPoints = false,
            };
            test.Run(false, false, delaunatorConfig);
        }
    }
}
