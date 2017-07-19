namespace BuildTaskAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            LogManager.StoreTotalNumberOfBuilds();
            LogManager.StoreBuildErrorLogs();
            LogManager.UpdateUncategorizedLogs();
            LogManager.UpdateMiscategorizedLogs();
        }
    }
}
