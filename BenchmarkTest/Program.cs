using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BenchmarkTest
{
    public class Program
    {
        static void Main(string[] args)
        {
            //BenchmarkRunner.Run<BenchmarkTestClass>();
            BenchmarkRunner.Run<RestoreBorderBenchmark>();
        }
    }
}
