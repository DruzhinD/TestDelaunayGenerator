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
    public class RestoreBorderBetween
    {
        protected class CustomConfig : ManualConfig
        {
            public CustomConfig()
                : base()
            {
                // Добавляем метку времени к пути с артефактами
                //изменяем путь к каталогу
                ArtifactsPath = $"{nameof(RestoreBorderBetween)}_{DateTime.Now:yyyyMMdd_HHmmss}";
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
                int startCnt = 55;
                int limit = 55;
                int increment = 10;

                for (int p = startCnt; p < limit + 1; p += increment)
                    values.Add(p);
                return values;
            }
        }

        //процент промежуточных граничных вершин от исходного количества точек
        [ParamsSource(nameof(PercentFromPointsValues))]
        public double PercentFromPoints { get; set; }
        public IEnumerable<double> PercentFromPointsValues
        {
            get
            {
                List<double> values = new List<double>();
                double startCnt = 0;
                double limit = 1.2;
                double increment = 0.2;

                for (double p = startCnt; p < limit + 1; p += increment)
                    values.Add(p);
                return values;
            }
        }
        #endregion

        //для отсечения треугольников требуется больше точек на ребре
        [IterationSetup(Targets = new string[] {
            nameof(Rb_CpSingleThread),
            nameof(Rb_CpMultiThread),
        })]
        public void InitBoundaryWithGenerator()
        {
            int pointsPerEdge = (int)(PercentFromPoints / 100 * PointCount / BoundaryVertexCount);
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(pointsPerEdge));
        }

        //для отсечения треугольников требуется больше точек на ребре
        [IterationSetup(Targets = new string[] {
            nameof(OnlyClippingTriangles)
        })]
        public void SetupForOnlyClipTriangle()
        {
            int pointsPerEdge = (int)(2.5 / 100 * PointCount / BoundaryVertexCount);
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(pointsPerEdge));
        }
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
            ParamArray.delaunator = test.Run(showForm: false, config: delaunatorConfig);
        }

        [Benchmark(Description = "отсечение точек (однопоточное), восстановление границы")]
        public void Rb_CpSingleThread()
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

        [Benchmark(Description = "отсечение точек (многопоточное), восстановление границы")]
        public void Rb_CpMultiThread()
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
    }
}
