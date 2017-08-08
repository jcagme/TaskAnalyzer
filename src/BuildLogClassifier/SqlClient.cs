namespace BuildLogClassifier
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    static class SqlClient
    {
        private static string _analyzerConnectionString;
        private static string _helixProdConnectionString;
        private static readonly Lazy<Task<List<string>>> _supportedBranches;

        static SqlClient()
        {
            _analyzerConnectionString = SettingsManager.GetStagingSetting("LogAnalysisWriteDbConnectionString");
            _helixProdConnectionString = SettingsManager.GetProdSetting("HelixWriteDbConnectionString");
            _supportedBranches = new Lazy<Task<List<string>>>(() => Task.Run(SetSupportedBranchesAsync));
        }

        public static List<string> GetUniqueFailureLogs()
        {
            List<string> failureLogs = new List<string>();

            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                SqlCommand command = new SqlCommand("SELECT DISTINCT ErrorLog FROM BuildErrorLogs", connection);
                connection.Open();

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    failureLogs.Add(reader.GetString(0));
                }

                reader.Close();
            }

            return failureLogs;
        }

        public static DateTime? GetLastLogDate()
        {
            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                SqlCommand command = new SqlCommand("SELECT MAX(CreatedDate) FROM BuildErrorLogs", connection);
                command.CommandTimeout = 60 * 5;
                connection.Open();

                object dateTime = command.ExecuteScalar();

                if (dateTime != null)
                {
                    if (string.IsNullOrEmpty(dateTime.ToString()))
                    {
                        return null;
                    }

                    return DateTime.Parse(dateTime.ToString());
                }

                return null;
            }
        }

        public static List<string> GetUncategorizedLogs()
        {
            List<string> logs = new List<string>();

            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                string query = @"
SELECT DISTINCT ErrorLog 
FROM BuildErrorLogs
WHERE Category IS NULL 
OR Class IS NULL";

                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();

                command.CommandTimeout = 60 * 10;
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    logs.Add(reader.GetString(0));
                }

                reader.Close();
            }

            return logs;
        }

        public static async Task<List<Build>> GetBuildDataAsync(DateTime? startDate, bool isFailedBuildsData)
        {
            string baseQuery = @"
SELECT
	[Source],
    [Uri],
    [Created],
	JobId,
	Build
FROM (
	SELECT
		Jobs.JobId,
		Jobs.Build,
        Jobs.Source,
		Jobs.Created,
        EventData.Name as UriName,
        EventData.Value as UriValue,
        Counts.Name as CountName,
        Counts.Value as CountValue
    FROM EventData
    INNER JOIN Events ON Events.EventId = EventData.EventId
    LEFT OUTER JOIN Events AS CountEvents ON Events.WorkItemId = CountEvents.WorkItemId {0}
    LEFT OUTER JOIN EventData AS Counts ON CountEvents.EventId = Counts.EventId
    INNER JOIN WorkItems ON Events.WorkItemId = WorkItems.WorkItemId
	INNER JOIN Jobs ON Jobs.JobId = Events.JobId
	WHERE
        Events.Type = 'ExternalLink'
) AS Source
PIVOT
    (
        MAX(UriValue)
        FOR UriName IN ([Description], [Uri])
    ) AS uriPivot
PIVOT
    (
        MAX(CountValue)
        FOR CountName IN ([WarningCount], [ErrorCount])
    ) AS countPivot
{1} ";
            List<Build> builds = new List<Build>();

            if (isFailedBuildsData)
            {
                baseQuery = string.Format(baseQuery, "AND CountEvents.Type = 'VsoBuildWarningsAndErrors'", "WHERE ErrorCount > 0");
            }
            else
            {
                baseQuery = string.Format(baseQuery, string.Empty, string.Empty);
            }

            if (startDate != null)
            {
                baseQuery += isFailedBuildsData ? "AND " : "WHERE ";
                baseQuery += $"Created > '{startDate}'";
            }

            baseQuery += " ORDER BY Created";

            using (SqlConnection connection = new SqlConnection(_helixProdConnectionString))
            {
                SqlCommand command = new SqlCommand(baseQuery, connection);
                connection.Open();

                command.CommandTimeout = 60 * 15;
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    Match match = Regex.Match(reader.GetString(1), "buildId=([\\d]*)");

                    if (match.Success)
                    {
                        int vsoBuildNumber = 0;

                        if (int.TryParse(match.Groups[1].Value, out vsoBuildNumber))
                        {
                            builds.Add(new Build
                            {
                                CreatedDate = new DateTime(reader.GetDateTimeOffset(2).Ticks),
                                Source = reader.GetString(0),
                                VsoBuildId = vsoBuildNumber,
                                BuildNumber = reader.GetString(4),
                                JobId = reader.GetInt32(3)
                            });
                        }
                    }
                }

                reader.Close();
            }

            await RemoveNotSupportedBranchesAsync(builds);

            return builds;
        }

        public static List<string> GetPatterns()
        {
            List<string> patterns = new List<string>();

            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                SqlCommand command = new SqlCommand(@"SELECT [Pattern] from Regex", connection);
                connection.Open();

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    patterns.Add(reader.GetString(0));
                }
            }

            return patterns;
        }

        public static void InsertNewFailuresLogs(List<Build> buildFailures)
        {
            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                connection.Open();

                foreach (Build buildFailure in buildFailures)
                {
                    SqlCommand command =
                    new SqlCommand(@"
                    INSERT INTO [dbo].[BuildErrorLogs]
                               ([CreatedDate]
                               ,[VsoBuildId]
                               ,[BuildNumber] 
                               ,[JobId]
                               ,[Source]
                               ,[BuildDefinitionName]
                               ,[FailedTaskName]
                               ,[ErrorLog]
                               ,[CategoryMatchLevel]
                               ,[LogUri])
                         VALUES
                               (@CreatedDate,
                                @VsoBuildId,
                                @BuildNumber,
                                @JobId,
                                @Source,
                                @BuildDefinitionName,
                                @FailedTaskName,
                                @ErrorLog,
                                @CategoryMatchLevel,
                                @LogUri)", connection);
                    command.Parameters.AddWithValue("@CreatedDate", buildFailure.CreatedDate);
                    command.Parameters.AddWithValue("@VsoBuildId", buildFailure.VsoBuildId);
                    command.Parameters.AddWithValue("@BuildNumber", buildFailure.BuildNumber);
                    command.Parameters.AddWithValue("@JobId", buildFailure.JobId);
                    command.Parameters.AddWithValue("@Source", buildFailure.Source);
                    command.Parameters.AddWithValue("@BuildDefinitionName", buildFailure.BuildDefinitionName);
                    command.Parameters.AddWithValue("@FailedTaskName", buildFailure.FailedTask);
                    command.Parameters.AddWithValue("@ErrorLog", buildFailure.ErrorLog);
                    command.Parameters.AddWithValue("@CategoryMatchLevel", 0);
                    command.Parameters.AddWithValue("@LogUri", buildFailure.LogUri);
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void InsertNewBuilds(List<Build> totalBuilds)
        {
            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                connection.Open();

                foreach (Build build in totalBuilds)
                {
                    SqlCommand command =
                    new SqlCommand(@"
                    INSERT INTO [dbo].[BuildHistory]
                               ([BuildDate]
                               ,[Source]
                               ,[BuildNumber]
                               ,[VsoBuildId])
                         VALUES
                               (@BuildDate
                                ,@Source
                                ,@BuildNumber
                                ,@VsoBuildId)", connection);
                    command.Parameters.AddWithValue("@BuildDate", build.CreatedDate);
                    command.Parameters.AddWithValue("@Source", build.Source);
                    command.Parameters.AddWithValue("@BuildNumber", build.BuildNumber);
                    command.Parameters.AddWithValue("@VsoBuildId", build.VsoBuildId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public static async Task UpdateUncategorizedLogsAsync(List<string> logs)
        {
            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                connection.Open();

                List<Classification> classifications = await Classifier.GetClassificationsAsync(logs).ConfigureAwait(false);

                foreach (Classification classification in classifications)
                {
                    int level = 0;

                    SqlCommand command =
                        new SqlCommand(@"
                            UPDATE [dbo].[BuildErrorLogs]
                            SET [Category] = @Category, 
                                [Class] = @Class,
                                [CategoryMatchLevel] = @CategoryMatchLevel
                            WHERE ErrorLog = @Log", connection);
                    command.CommandTimeout = 60 * 10;
                    command.Parameters.AddWithValue("@Log", classification.Log);
                    command.Parameters.AddWithValue("@CategoryMatchLevel", level);

                    if (classification.Categories.Count > 0)
                    {
                        foreach (CategoryClassMap category in classification.Categories)
                        {
                            command.Parameters.AddWithValue("@Category", category.Category);
                            command.Parameters.AddWithValue("@Class", category.Class);
                            command.ExecuteNonQuery();
                            command.Parameters.RemoveAt(3);
                            command.Parameters.RemoveAt(2);
                            level++;
                        }
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@Category", "Unknown");
                        command.Parameters.AddWithValue("@Class", -1);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void UpdateMiscategorizedLogs()
        {
            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("usp_updatate_miscategorized_logs", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                command.CommandTimeout = 60 * 30;
                command.ExecuteNonQuery();
            }
        }

        public static DateTime? GetLastStoredBuildDate()
        {
            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                SqlCommand command = new SqlCommand("SELECT MAX(BuildDate) FROM BuildHistory", connection);
                connection.Open();
                object dateTime = command.ExecuteScalar();

                if (dateTime != null)
                {
                    if (string.IsNullOrEmpty(dateTime.ToString()))
                    {
                        return null;
                    }

                    return DateTime.Parse(dateTime.ToString());
                }

                return null;
            }
        }

        public static HashSet<int> GetStoredVsoBuilds()
        {
            HashSet<int> recordedBuilds = new HashSet<int>();

            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                SqlCommand command = new SqlCommand("SELECT DISTINCT(VsoBuildId) FROM BuildHistory", connection);
                connection.Open();

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    recordedBuilds.Add(reader.GetInt32(0));
                }
            }

            return recordedBuilds;
        }

        public static List<BuildSummaryItem> GetBuildSummaryItems(DateTime? startDate)
        {
            List<BuildSummaryItem> buildSummaryItems = new List<BuildSummaryItem>();
            string baseQuery = @"SELECT 
	H.BuildDate
	,H.[Source] AS Branch
	,COUNT(DISTINCT H.BuildNumber) AS SuccesfulBuildCount
	,COUNT(DISTINCT E.BuildNumber) AS FailedBuildCount

FROM BuildHistory H
LEFT JOIN BuildErrorLogs E
ON 
	E.[Source] = H.[Source]
	AND E.BuildNumber = H.BuildNumber
{0}
GROUP BY 
	H.BuildDate
	,H.[Source]";

            baseQuery = string.Format(baseQuery, startDate != null ? $"WHERE BuildDate >= '{startDate}'" : string.Empty);

            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                SqlCommand command = new SqlCommand(baseQuery, connection);
                command.CommandTimeout = 60 * 10;
                connection.Open();

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    buildSummaryItems.Add(new BuildSummaryItem
                    {
                        BuildDate = reader.GetDateTime(0),
                        Branch = reader.GetString(1),
                        SuccesfulBuildCount = reader.GetInt32(2),
                        FailedBuildCount = reader.GetInt32(3)
                    });
                }
            }

            return buildSummaryItems;
        }

        public static void UpsertNewBuildSummaries(List<BuildSummaryItem> buildSummaryItems)
        {
            List<BuildSummaryItem> itemsToUpdate = new List<BuildSummaryItem>();
            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                connection.Open();

                foreach (BuildSummaryItem summaryItem in buildSummaryItems)
                {
                    try
                    {
                        SqlCommand command =
                        new SqlCommand(@"
                    INSERT INTO [dbo].[BuildSummary]
                               ([BuildDate]
                               ,[Branch]
                               ,[SuccesfulBuildCount]
                               ,[FailedBuildCount])
                         VALUES
                               (@BuildDate
                                ,@Branch
                                ,@SuccesfulBuildCount
                                ,@FailedBuildCount)", connection);
                        command.Parameters.AddWithValue("@BuildDate", summaryItem.BuildDate);
                        command.Parameters.AddWithValue("@Branch", summaryItem.Branch);
                        command.Parameters.AddWithValue("@SuccesfulBuildCount", summaryItem.SuccesfulBuildCount);
                        command.Parameters.AddWithValue("@FailedBuildCount", summaryItem.FailedBuildCount);
                        command.ExecuteNonQuery();
                    }
                    // When we try to insert a PK combination which already exist we add the item to a list of items to update.
                    // 2627 is the exception number when we try to insert a duplicate key. https://docs.microsoft.com/en-us/sql/relational-databases/replication/mssql-eng002627
                    catch (SqlException e)
                    when (e.Number == 2627)
                    {
                        itemsToUpdate.Add(summaryItem);
                    }
                }
            }

            UpdateExistingBuildSummaries(itemsToUpdate);
        }

        private static void UpdateExistingBuildSummaries(List<BuildSummaryItem> itemsToUpdate)
        {
            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                connection.Open();

                foreach (BuildSummaryItem summaryItem in itemsToUpdate)
                {
                    SqlCommand command =
                        new SqlCommand(@"
                    UPDATE dbo.BuildSummary 
                    SET SuccesfulBuildCount = @SuccesfulBuildCount,
                        FailedBuildCount = @FailedBuildCount
                    WHERE BuildDate = @BuildDate
                    AND Branch = @Branch", connection);
                    command.Parameters.AddWithValue("@SuccesfulBuildCount", summaryItem.SuccesfulBuildCount);
                    command.Parameters.AddWithValue("@FailedBuildCount", summaryItem.FailedBuildCount);
                    command.Parameters.AddWithValue("@BuildDate", summaryItem.BuildDate);
                    command.Parameters.AddWithValue("@Branch", summaryItem.Branch);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static async Task RemoveNotSupportedBranchesAsync(List<Build> builds)
        {
            List<string> branches = await _supportedBranches.Value.ConfigureAwait(false);
            builds = builds.Where(b => branches.Contains(b.Source)).ToList();
        }

        private static async Task<List<string>> SetSupportedBranchesAsync()
        {
            string authToken = SettingsManager.GetStagingSetting("GitHubApiAccessToken");
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", authToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue(
                    "Build-Log-Classifier-Config",
                    typeof(SettingsManager).GetTypeInfo().Assembly.GetName().Version.ToString())
            );

            HttpResponseMessage response = client.GetAsync("https://api.github.com/repos/dotnet/core-eng/contents/build-log-classifier-config/supported-branches.json").Result;
            dynamic responseContent = await response.Content.ReadAsAsync<object>().ConfigureAwait(false);
            byte[] data = Convert.FromBase64String((string) responseContent.content);
            string decodedContent = System.Text.Encoding.UTF8.GetString(data);

            List<string> supportedBranches = JsonConvert.DeserializeObject<List<string>>(decodedContent);

            return supportedBranches;
        }
    }
}
