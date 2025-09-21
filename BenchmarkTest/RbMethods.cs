using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Toolchains;
using CommonLib.Areas;
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
    /// Сравнение восстановления граничного контура без отсечения точек
    /// </summary>
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, invocationCount: 1, iterationCount: 5, warmupCount: 0)]
    [MedianColumn]
    [Config(typeof(CustomConfig))]
    [JsonExporterAttribute.Full]
    public class RbMethods
    {
        protected class CustomConfig : ManualConfig
        {
            public CustomConfig()
                : base()
            {
                ArtifactsPath = $"восстановление контура {DateTime.Now:yyyyMMdd_HHmmss}";
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
                int startCnt = 25;
                int limit = 400;
                int increment = 25;

                for (int p = startCnt; p < limit + 1; p += increment)
                    values.Add(p);
                return values;
            }
        }

        [ParamsSource(nameof(PartAfterClipPointsValues))]
        public double PartAfterClipPoints { get; set; }
        public IEnumerable<double> PartAfterClipPointsValues
        {
            get
            {
                List<double> values = new List<double>();
                double startCnt = 0.5;
                double limit = 0.5;
                double increment = 0.5;

                for (double p = startCnt; p <= limit; p += increment)
                    values.Add(p);
                return values;
            }
        }
        #endregion

        #region Подготовка итерации

        //для отсечения треугольников требуется больше точек на ребре
        [IterationSetup(Targets = new string[] {
            nameof(NonRbNonCp),
        })]
        public void InitBoundaryWithGenerator()
        {
            int pointsPerEdge = (int)(0.025 * PointCount / BoundaryVertexCount);
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(pointsPerEdge), PartAfterClipPoints);
        }

        //без промежуточных вершин на ребрах
        [IterationSetup(Targets = new string[] {
            nameof(RbBaseRectangle)
        })]
        public void InitRectangle()
        {
            test = new Test(false);
            test.CreateBenchmarkTestArea(
                PointCount,
                BoundaryVertexCount,
                new GeneratorFixed(0),
                PartAfterClipPoints,
                Figure.RegularPolygon);
        }

        [IterationSetup(Targets = new string[] {
            nameof(RbBaseStar)
        })]
        public void InitStar()
        {
            test = new Test(false);
            test.CreateBenchmarkTestArea(
                PointCount,
                BoundaryVertexCount,
                new GeneratorFixed(0),
                PartAfterClipPoints,
                Figure.RegularStar);
        }

        //для стандартной триангуляции без ограничений
        [IterationSetup(Targets = new string[] {
            nameof(DefaultTriangulation),
        })]
        public void InitDefaultTriangulation()
        {
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, 0);
        }

        #endregion

        #region Варианты триангуляции

        [Benchmark(Description = "Стандартная триангуляция, без отсечений (точек/треугольников) и без восстановления границы", Baseline = true)]
        public void DefaultTriangulation()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = true,
                RestoreBorder = false,
                UseClippingPoints = false,
                ParallelClippingPoints = false
            };
            test.Run(showForm: false, config: delaunatorConfig);
        }


        [Benchmark(Description = "триангуляция с отсечением треугольников и с восстановлением границы (N-угольник)")]
        public void RbBaseRectangle()
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

        [Benchmark(Description = "триангуляция с отсечением треугольников и с восстановлением границы (N/2-конечная звезда)")]
        public void RbBaseStar()
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

        [Benchmark(Description = "триангуляция с отсечением треугольников, без отсечения точек и без восстановления границы")]
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

        #endregion
    }
}