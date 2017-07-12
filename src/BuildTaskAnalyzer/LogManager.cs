namespace BuildTaskAnalyzer
{
    using System;
    using System.Collections.Generic;

    public class LogManager
    {
        public static void SaveTaskFailures()
        {
            DateTime? startDate = SqlClient.GetMaxDate();
            List<BuildError> failedBuilds = SqlClient.GetFailedBuildData(startDate);

            List<string> patterns = SqlClient.GetPatterns();

            foreach (BuildError failedBuild in failedBuilds)
            {
                try
                {
                    List<BuildError> failures = VsoBuildClient.GetFailedTaskDataFromBuild(
                        failedBuild.CreatedDate, 
                        failedBuild.VsoBuildId,
                        failedBuild.BuildNumber, 
                        failedBuild.JobId,
                        failedBuild.Source, 
                        patterns);

                    if (failures.Count > 0)
                    {
                        SqlClient.InsertNewFailuresLogs(failures);
                    }
                }
                catch
                { }
            }
        }

        public static void UpdateUncategorizedLogs()
        {
            List<string> buildsWithNoLogs = SqlClient.GetUncategorizedLogs();

            if (buildsWithNoLogs.Count > 0)
            {
                SqlClient.UpdateUncategorizedLogs(buildsWithNoLogs);
            }
        }

        public static void UpdateCategories()
        {
            List<string> buildsWithNoLogs = SqlClient.GetUniqueFailureLogs();
            if (buildsWithNoLogs.Count > 0)
            {
                SqlClient.UpdateUncategorizedLogs(buildsWithNoLogs);
            }
        }

        public static void UpdateMiscategorizedLogs()
        {
            SqlClient.UpdateMiscategorizedLogs();
        }
    }
}
