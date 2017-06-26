namespace BuildTaskAnalyzer
{
    using System;
    using System.Collections.Generic;

    public class OutputManager
    {
        public static void SaveTaskFailures()
        {
            DateTime? startDate = SqlClient.GetMaxDate();
            List<BuildFailure> failedBuilds = SqlClient.GetFailedBuildData(startDate);

            List<string> patterns = SqlClient.GetPatterns();

            foreach (BuildFailure failedBuild in failedBuilds)
            {
                try
                {
                    List<BuildFailure> failures = VsoBuildClient.GetFailedTaskDataFromBuildAsync(failedBuild.CreatedDate, failedBuild.BuildNumber, failedBuild.Source, patterns);

                    if (failures.Count > 0)
                    {
                        SqlClient.InsertNewFailuresLogs(failures);
                    }
                }
                catch
                { }
            }
        }

        public static void InsertLogsForUnmappedBuilds()
        {
            List<BuildFailure> buildsWithNoLogs = SqlClient.GetBuildWithNoLogs();
            List<BuildFailure> failures = VsoBuildClient.GetNewLogEntries(buildsWithNoLogs);

            if (failures.Count > 0)
            {
                SqlClient.InsertNewFailuresLogs(failures);
            }
        }
    }
}
