using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BenchmarkTest
{
    public class CustomColumn : IColumn
    {
        public string Id => "inffo";
        public string ColumnName => "Foo";

        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public UnitType UnitType => BenchmarkDotNet.Columns.UnitType.Dimensionless;

        public string Legend => $"Custom '{ColumnName}' tag column";

        public string GetValue(BenchmarkDotNet.Reports.Summary summary, BenchmarkDotNet.Running.BenchmarkCase benchmarkCase)
        {
            
            //return BenchmarkTestClass.foo.ToString();
            return benchmarkCase.DisplayInfo;
        }

        public bool IsAvailable(Summary summary) => true;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            //benchmarkCase.
            //summary.
            return GetValue(summary, benchmarkCase);
        }

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase)
        {
            return false;
        }
    }
}
