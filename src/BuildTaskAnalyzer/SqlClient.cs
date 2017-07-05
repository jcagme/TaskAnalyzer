namespace BuildTaskAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Text.RegularExpressions;

    static class SqlClient
    {
        private const string LocalConnectionString = "";
        private const string HelixProdConnectionString = "";
        private static List<Classification> equivalenceClasses = new List<Classification>();

        public static List<string> GetUniqueFailureLogs()
        {
            List<string> failureLogs = new List<string>();

            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                SqlCommand command = new SqlCommand("SELECT DISTINCT MatchedError FROM TaskFailureLogs", connection);
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

        public static DateTime? GetMaxDate()
        {
            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                SqlCommand command = new SqlCommand("SELECT MAX(CreatedDate) FROM TaskFailure", connection);
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

            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                string query = @"
SELECT DISTINCT MatchedError 
FROM TaskFailureLogs
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

        public static List<BuildError> GetFailedBuildData(DateTime? startDate)
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
    LEFT OUTER JOIN Events AS CountEvents ON Events.WorkItemId = CountEvents.WorkItemId AND CountEvents.Type = 'VsoBuildWarningsAndErrors'
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
WHERE ErrorCount > 0";
            List<BuildError> buildNumbers = new List<BuildError>();

            using (SqlConnection connection = new SqlConnection(HelixProdConnectionString))
            {
                if (startDate != null)
                {
                    baseQuery += $" AND Created > '{startDate}'";
                }

                SqlCommand command = new SqlCommand(baseQuery, connection);
                connection.Open();

                command.CommandTimeout = 60 * 10;
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    Match match = Regex.Match(reader.GetString(1), "buildId=([\\d]*)");

                    if (match.Success)
                    {
                        int vsoBuildNumber = int.Parse(match.Groups[1].Value);
                        buildNumbers.Add(new BuildError
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

            return buildNumbers;
        }

        public static List<string> GetPatterns()
        {
            List<string> patterns = new List<string>();

            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
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

        public static void InsertNewFailuresLogs(List<BuildError> buildFailures)
        {
            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                connection.Open();

                foreach (BuildError buildFailure in buildFailures)
                {
                    SqlCommand command =
                    new SqlCommand(@"
                    INSERT INTO [dbo].[TaskFailureLogs]
                               ([CreatedDate]
                               ,[VsoBuildId]
                               ,[BuildNumber] 
                               ,[JobId]
                               ,[Source]
                               ,[BuildDefinitionName]
                               ,[FailedTaskName]
                               ,[MatchedError]
                               ,[LogUri]
                               ,[Category]
                               ,[Class])
                         VALUES
                               (@CreatedDate,
                                @VsoBuildId,
                                @BuildNumber,
                                @JobId,
                                @Source,
                                @BuildDefinitionName,
                                @FailedTaskName,
                                @MatchedError,
                                @LogUri,
                                @Category,
                                @Class)", connection);
                    command.Parameters.AddWithValue("@CreatedDate", buildFailure.CreatedDate);
                    command.Parameters.AddWithValue("@VsoBuildId", buildFailure.VsoBuildId);
                    command.Parameters.AddWithValue("@BuildNumber", buildFailure.BuildNumber);
                    command.Parameters.AddWithValue("@JobId", buildFailure.JobId);
                    command.Parameters.AddWithValue("@Source", buildFailure.Source);
                    command.Parameters.AddWithValue("@BuildDefinitionName", buildFailure.BuildDefinitionName);
                    command.Parameters.AddWithValue("@FailedTaskName", buildFailure.FailedTask);
                    command.Parameters.AddWithValue("@MatchedError", buildFailure.MatchedError);
                    command.Parameters.AddWithValue("@LogUri", buildFailure.LogUri);
                    command.Parameters.AddWithValue("@Category", buildFailure.Category);
                    command.Parameters.AddWithValue("@Class", buildFailure.Class);
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateUncategorizedLogs(List<string> logs)
        {
            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                connection.Open();

                List<Classification> classifications = Classifier.GetClassifications(logs);

                foreach (Classification classification in classifications)
                {
                    SqlCommand command =
                    new SqlCommand(@"
                    UPDATE [dbo].[TaskFailureLogs]
                    SET [Category] = @Category, 
                        [Class] = @Class
                    WHERE MatchedError = @Log", connection);
                    command.Parameters.AddWithValue("@Category", classification.Category);
                    command.Parameters.AddWithValue("@Class", classification.ErrorClass);
                    command.Parameters.AddWithValue("@Log", classification.Log);
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateBuildIdAndJobId(List<BuildError> errors)
        {
            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                connection.Open();

                foreach (BuildError error in errors)
                {
                    SqlCommand command =
                    new SqlCommand(@"
                    UPDATE [dbo].[TaskFailureLogs]
                    SET [JobId] = @JobId, 
                        [BuildNumber] = @BuildNumber
                    WHERE CreatedDate = @CreatedDate
                    AND Source = @Source
                    AND VsoBuildId = @VsoBuildId", connection);
                    command.Parameters.AddWithValue("@JobId", error.JobId);
                    command.Parameters.AddWithValue("@BuildNumber", error.BuildNumber);
                    command.Parameters.AddWithValue("@CreatedDate", error.CreatedDate);
                    command.Parameters.AddWithValue("@Source", error.Source);
                    command.Parameters.AddWithValue("@VsoBuildId", error.VsoBuildId);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
