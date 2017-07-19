namespace BuildTaskAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Text.RegularExpressions;

    static class SqlClient
    {
        private static string _analyzerConnectionString;
        private static string _helixProdConnectionString;

        static SqlClient()
        {
            _analyzerConnectionString = SettingsManager.GetStagingSetting("LogAnalysisWriteDbConnectionString");
            _helixProdConnectionString = SettingsManager.GetProdSetting("HelixWriteDbConnectionString");
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

        public static List<Build> GetBuildData(DateTime? startDate, bool isFailedBuildsData)
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
                        int vsoBuildNumber = int.Parse(match.Groups[1].Value);
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

                reader.Close();
            }

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
                               ,[Source])
                         VALUES
                               (@BuildDate,
                                @Source)", connection);
                    command.Parameters.AddWithValue("@BuildDate", build.CreatedDate);
                    command.Parameters.AddWithValue("@Source", build.Source);
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateUncategorizedLogs(List<string> logs)
        {
            using (SqlConnection connection = new SqlConnection(_analyzerConnectionString))
            {
                connection.Open();

                List<Classification> classifications = Classifier.GetClassifications(logs);

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

                command.CommandTimeout = 60 * 15;
                command.ExecuteNonQuery();
            }
        }

        public static DateTime? GetLastStoredBuild()
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
    }
}
