using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System;
using TestDelaunayGenerator;

namespace BenchmarkTest
{
    /// <summary>
    /// Custom column that displays the number of points returned by the delaunator
    /// </summary>
    public class ResPointCntColumn : IColumn
    {
        public string Id => nameof(ResPointCntColumn);
        public string ColumnName => "ResPointCnt";
        public string Legend => "Number of points returned by the delaunator";
        public UnitType UnitType => UnitType.Dimensionless;
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public bool IsAvailable(Summary summary) => true;
        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        {
            return GetValue(summary, benchmarkCase, SummaryStyle.Default);
        }

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            
            Console.WriteLine($"IS NULL {nameof(ParamArray.delaunator)}:{ParamArray.delaunator is null}");
            // Access the static field that was set in the benchmark method
            // This works because the benchmark runs before the column values are calculated
            if (ParamArray.delaunator != null)
            {
                return ParamArray.delaunator.Points.Length.ToString();
            }
            return "N/A";
        }

        public override string ToString() => ColumnName;
    }
}