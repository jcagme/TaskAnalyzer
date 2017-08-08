namespace BuildLogClassifier
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.TeamFoundation.Build.WebApi;
    using System.Threading.Tasks;

    public class LogManager
    {
        public static async Task StoreTotalNumberOfBuildsAsync()
        {
            DateTime? startDate = SqlClient.GetLastStoredBuildDate();
            HashSet<int> recordedBuilds = SqlClient.GetStoredVsoBuilds();

            if (startDate == null)
            {
                // MIN date on the failed builds table so we don't care about previous builds
                startDate = DateTime.Parse("2016-09-29 14:30:49.9066667");
            }

            List<Build> totalBuilds = await SqlClient.GetBuildDataAsync(startDate, false);

            // Extra filter to avoid dups since we cannot only rely on BuildDate since it has not time
            totalBuilds = totalBuilds.Where(b => !recordedBuilds.Contains(b.VsoBuildId)).ToList();
            SqlClient.InsertNewBuilds(totalBuilds);
        }

        public static async Task StoreBuildErrorLogsAsync()
        {
            DateTime? startDate = SqlClient.GetLastLogDate();
            List<Build> failedBuilds = await SqlClient.GetBuildDataAsync(startDate, true);

            List<string> patterns = SqlClient.GetPatterns();

            foreach (Build failedBuild in failedBuilds)
            {
                try
                {
                    List<Build> failures = await VsoBuildClient.GetFailedTaskDataFromBuildAsync(
                        failedBuild.CreatedDate, 
                        failedBuild.VsoBuildId,
                        failedBuild.BuildNumber, 
                        failedBuild.JobId,
                        failedBuild.Source, 
                        patterns).ConfigureAwait(false);

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

        public static void UpdateBuildSummary()
        {
            DateTime? startDate = SqlClient.GetLastStoredBuildDate();
            List<BuildSummaryItem> buildSummaryItems = SqlClient.GetBuildSummaryItems(startDate);

            if (buildSummaryItems.Count > 0)
            {
                SqlClient.UpsertNewBuildSummaries(buildSummaryItems);
            }
        }

        public static async Task UpdateUncategorizedLogsAsync()
        {
            List<string> buildsWithNoLogs = SqlClient.GetUncategorizedLogs();

            if (buildsWithNoLogs.Count > 0)
            {
                await SqlClient.UpdateUncategorizedLogsAsync(buildsWithNoLogs).ConfigureAwait(false);
            }
        }

        public static void UpdateMiscategorizedLogs()
        {
            SqlClient.UpdateMiscategorizedLogs();
        }
    }
}
