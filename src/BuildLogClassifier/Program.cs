namespace BuildLogClassifier
{
    using System;
    using System.Reflection;

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
