using BenchmarkDotNet.Running;

namespace FastDFS.Client.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            // Run all benchmarks
            var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
