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
    /// <summary>
    /// В этом бенче представлены методы, которые не зависят от Nb,
    /// создан для демонстрации этого факта.
    /// BaseLine - стандартная триангуляция
    /// </summary>
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, invocationCount: 1, iterationCount: 10, warmupCount: 0)]
    [MedianColumn]
    [Config(typeof(CustomConfig))]
    [JsonExporterAttribute.Full]
    public class MethodIndependence
    {
        protected class CustomConfig : ManualConfig
        {
            public CustomConfig()
                : base()
            {
                // Добавляем метку времени к пути с артефактами
                //изменяем путь к каталогу
                ArtifactsPath = $"независимость от Nb {DateTime.Now:yyyyMMdd_HHmmss}";
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


        /// <summary>
        /// Генерация тестовой оболочки.
        /// Внутри - генерация точек триангуляции, граничного контура
        /// </summary>
        /// <param name="pointsPerEdge"></param>
        /// <returns></returns>
        Test CreateTest(int pointsPerEdge)
        {
            var test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(pointsPerEdge));
            return test;
        }

        #region подготовка к итерации

        //для отсечения треугольников требуется больше точек на ребре
        [IterationSetup(Targets = new string[] {
            nameof(NonRbNonCp),
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
            //nameof(DefaultTriangulation),
            //nameof(OnlyClippingTrianglesTriangulation),
            nameof(RbBase)
        })]
        public void InitWithoutBetweenPoints()
        {
            test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(0), PartAfterClipPoints);
            //points.CopyTo(test.points, 0);
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

        #region Методы триангуляции

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
            //delaunator = new Delaunator(points, container, delaunatorConfig);
            //delaunator.Generate();
        }


        [Benchmark(Description = "триангуляция с отсечением треугольников и с восстановлением границы")]
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
            //delaunator = new Delaunator(points, container, delaunatorConfig);
            //delaunator.Generate();
        }

        #endregion
    }
}
