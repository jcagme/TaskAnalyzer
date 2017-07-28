namespace BuildLogClassifier
{
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                LogManager.StoreTotalNumberOfBuilds();
                LogManager.StoreBuildErrorLogs();
                LogManager.UpdateBuildSummary();
                LogManager.UpdateUncategorizedLogs();
                LogManager.UpdateMiscategorizedLogs();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
