namespace BuildTaskAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            OutputManager.SaveTaskFailures();
            OutputManager.InsertLogsForUnmappedBuilds();
        }
    }
}
