namespace BuildLogClassifier
{
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                LogManager.StoreTotalNumberOfBuildsAsync().Wait();
                LogManager.StoreBuildErrorLogsAsync().Wait();
                LogManager.UpdateBuildSummary();
                LogManager.UpdateUncategorizedLogsAsync().Wait();
                LogManager.UpdateMiscategorizedLogs();
            }
            catch (Exception exc)
            {
                Console.WriteLine($"---------------------- Application failed on: {DateTime.Now} ----------------------");
                Console.WriteLine(exc);
            }
        }
    }
}
