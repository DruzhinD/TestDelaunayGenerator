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
    /// В этом бенче - все методы кроме одиночного отсечения треугольников
    /// BaseLine - восстановление границы без отсечения точек
    /// </summary>
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, invocationCount: 1, iterationCount: 8, warmupCount: 0)]
    [MedianColumn]
    [Config(typeof(CustomConfig))]
    [JsonExporterAttribute.Full]
    public class AllMethods
    {
        protected class CustomConfig : ManualConfig
        {
            public CustomConfig()
                : base()
            {
                // Добавляем метку времени к пути с артефактами
                //изменяем путь к каталогу
                ArtifactsPath = $"все методы {DateTime.Now:yyyyMMdd_HHmmss}";
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
                int startCnt = 20;
                int limit = 200;
                int increment = 20;

                for (int p = startCnt; p < limit + 1; p += increment)
                    values.Add(p);
                return values;
            }
        }
        #endregion

        #region подготовка к итерации
        [IterationSetup(Targets = new string[] {
            nameof(NonRbClipPointSingleThread),
            nameof(NonRbClipPointMultiThread),
            nameof(NonRbNonCp),
        })]
        public void InitDataWithBetween()
        {
            int pointsPerEdge = (int)(0.025 * PointCount / BoundaryVertexCount);
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(0), pointsPerEdge);
        }


        [IterationSetup(Targets = new string[] {
            nameof(RbBase),
            nameof(RbClipPointSingleThread),
            nameof(RbClipPointMultiThread),
        })]
        public void InitData()
        {
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(0), 0);
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
        }


        [Benchmark(Description = "восстановление границы, отсечение треугольников, отсечение точек (однопоточное)")]
        public void RbClipPointSingleThread()
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

        [Benchmark(Description = "восстановление границы, отсечение треугольников, отсечение точек (многопоточное)")]
        public void RbClipPointMultiThread()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = true,
                ParallelClippingPoints = true
            };
            test.Run(showForm: false, config: delaunatorConfig);
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
        }

        [Benchmark(Description = "без восстановление границы, отсечение треугольников, отсечение точек (многопоточное)")]
        public void NonRbClipPointMultiThread()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = false,
                UseClippingPoints = true,
                ParallelClippingPoints = true
            };
            test.Run(showForm: false, config: delaunatorConfig);
        }

        #endregion
    }
}
