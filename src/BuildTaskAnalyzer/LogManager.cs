﻿namespace BuildTaskAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Microsoft.TeamFoundation.Build.WebApi;

    public class LogManager
    {
        public static void StoreTotalNumberOfBuilds()
        {
            DateTime? startDate = SqlClient.GetLastStoredBuild();

            if (startDate == null)
            {
                // MIN date on the failed builds table so we don't care about previous builds
                startDate = DateTime.Parse("2016-09-29 14:30:49.9066667");
            }

            List<Build> totalBuilds = SqlClient.GetBuildData(startDate, false);
            SqlClient.InsertNewBuilds(totalBuilds);
        }

        public static void StoreBuildErrorLogs()
        {
            DateTime? startDate = SqlClient.GetLastLogDate();
            List<Build> failedBuilds = SqlClient.GetBuildData(startDate, true);

            List<string> patterns = SqlClient.GetPatterns();

            foreach (Build failedBuild in failedBuilds)
            {
                try
                {
                    List<Build> failures = VsoBuildClient.GetFailedTaskDataFromBuild(
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
