namespace BuildTaskAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            var logs = SqlClient.GetUniqueFailureLogs();
            SqlClient.UpdateUncategorizedLogs(logs);

            LogManager.SaveTaskFailures();
            LogManager.UpdateUncategorizedLogs();
            LogManager.UpdateMiscategorizedLogs();
        }
    }
}
