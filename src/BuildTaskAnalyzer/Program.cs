namespace BuildTaskAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            LogManager.StoreBuildErrorLogs();
            LogManager.UpdateUncategorizedLogs();
            LogManager.UpdateMiscategorizedLogs();
        }
    }
}
