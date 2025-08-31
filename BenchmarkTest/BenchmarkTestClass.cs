using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using DelaunayUI;
using Perfolizer.Horology;
using Perfolizer.Mathematics.OutlierDetection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestDelaunayGenerator;
using TestDelaunayGenerator.Boundary;

namespace BenchmarkTest
{
    /// <summary>
    /// Агрегирует бенчмарки для различных вариантов триангуляции
    /// </summary>
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, invocationCount: 1, iterationCount: 10, warmupCount: 0)]
    //[MemoryDiagnoser]
    [MedianColumn]
    [Config(typeof(CustomConfig))]
    //[RPlotExporter]
    //[Outliers(OutlierMode.DontRemove)] //не учитывает в статистике выбросы
    //[JsonExporter]
    [JsonExporterAttribute.Full]
    public class BenchmarkTestClass
    {
        protected class CustomConfig : ManualConfig
        {
            public CustomConfig()
                : base()
            {
                // Добавляем метку времени к пути с артефактами
                //изменяем путь к каталогу
                ArtifactsPath = $"BenchmarkResults_{DateTime.Now:yyyyMMdd_HHmmss}";
                //AddJob(Job.Dry);
                //AddColumn(new TagColumn("Kind", name => name.Substring(0, 3)));
                //AddColumn(new TagColumn("Number", name => foo.ToString()));
                //AddColumn(new ResPointCntColumn());
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

        Test CreateTest(int pointsPerEdge)
        {
            var test = new Test(false);
            test.CreateBenchmarkTestArea(PointCount, BoundaryVertexCount, new GeneratorFixed(pointsPerEdge));
            return test;
        }


        #region Только отсечение треугольников
        //для отсечения треугольников требуется больше точек на ребре
        //[IterationSetup(Targets = new string[] {
        //    nameof(OnlyClippingTriangles),
        //})]
        public void InitBoundaryWithGeneratorMore()
        {
            int pointsPerEdge = (int)(0.025 * PointCount / BoundaryVertexCount);
            test = CreateTest(pointsPerEdge);
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


        //без восстановления границы, требуется достаточное количество вершин на граничном контуре
        [IterationSetup(Targets = new string[] {
            nameof(TestDelaunayWithoutRestoreBorder),
            nameof(TestDelaunayWithoutClippingPoints),
            nameof(OnlyClippingTriangles)
        })]
        public void InitBoundaryWithGenerator()
        {
            int pointsPerEdge = (int)(0.1 * PointCount / BoundaryVertexCount);
            test = CreateTest(pointsPerEdge);
        }


        [Benchmark(Description = "Без восстановления границы, отсечение точек [1']")]
        public void TestDelaunayWithoutRestoreBorder()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = false,
                UseClippingPoints = true,
                ParallelClippingPoints = true,
            };
            test.Run(showForm: false, config: delaunatorConfig);
        }


        [IterationSetup(Targets = new string[] {
            //nameof(TestDelaunayWithoutClippingPoints),
            nameof(TestDelaunayWithAll)
                })]
        public void InitBoundary()
        {
            test = CreateTest(0);
        }

        [Benchmark(Description = "Восстановление границы, без отсечения точек")]
        public void TestDelaunayWithoutClippingPoints()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = false,
                ParallelClippingPoints = false,
            };
            test.Run(showForm: false, config: delaunatorConfig);
        }

        [Benchmark(Description = "Весь функционал")]
        public void TestDelaunayWithAll()
        {
            delaunatorConfig = new DelaunatorConfig()
            {
                IncludeExtTriangles = false,
                RestoreBorder = true,
                UseClippingPoints = true,
                ParallelClippingPoints = true,
            };
            test.Run(showForm: false, config: delaunatorConfig);
        }
    }

    public class ParamArray
    {
        public static Delaunator delaunator;
    }
}
