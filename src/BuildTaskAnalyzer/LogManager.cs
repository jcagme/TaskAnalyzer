namespace BuildTaskAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Microsoft.TeamFoundation.Build.WebApi;

    public class LogManager
    {
        public static void StoreBuildErrorLogs()
        {
            DateTime? startDate = SqlClient.GetLastLogDate();
            List<FailedBuild> failedBuilds = SqlClient.GetFailedBuildData(startDate);

            List<string> patterns = SqlClient.GetPatterns();

            foreach (FailedBuild failedBuild in failedBuilds)
            {
                try
                {
                    List<FailedBuild> failures = VsoBuildClient.GetFailedTaskDataFromBuild(
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
                // We only swallow VSTS' BuildNotFoundException since is the only know exception we know is thrown when we query for a build
                // in Helix DB which has been removed from VSTS
                catch (AggregateException exc) when 
                (exc.InnerException.GetType() == typeof(BuildNotFoundException))
                {}
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

        public static void UpdateMiscategorizedLogs()
        {
            SqlClient.UpdateMiscategorizedLogs();
        }
    }
}
