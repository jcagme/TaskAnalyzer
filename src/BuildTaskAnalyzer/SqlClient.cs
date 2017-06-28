namespace BuildTaskAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Text.RegularExpressions;

    static class SqlClient
    {
        private const string LocalConnectionString = "";
        private const string HelixProdConnectionString = "";
        private static List<Cluster> equivalenceClasses = new List<Cluster>();

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

        public static List<BuildFailure> GetBuildWithNoLogs()
        {
            List<BuildFailure> builds = new List<BuildFailure>();

            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                string query = @"
SELECT 
	CreatedDate, 
	VsoBuildId, 
	BuildDefinitionName,
	FailedTaskName,
    Source
FROM TaskFailure
WHERE VsoBuildId not in
(
	SELECT DISTINCT(VsoBuildId)
	FROM TaskFailureLogs
)";

                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();

                command.CommandTimeout = 60 * 10;
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    builds.Add(new BuildFailure
                    {
                        CreatedDate = reader.GetDateTime(0),
                        BuildNumber = reader.GetInt32(1),
                        BuildDefinitionName = reader.GetString(2),
                        Failure = reader.GetString(3),
                        Source = reader.GetString(4)
                    });
                }

                reader.Close();
            }

            return builds;
        }

        public static List<BuildFailure> GetFailedBuildData(DateTime? startDate)
        {
            string baseQuery = @"
SELECT
	[Source],
    [Uri],
    [Created]
FROM (
	SELECT
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
            List<BuildFailure> buildNumbers = new List<BuildFailure>();

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
                        int buildNumber = int.Parse(match.Groups[1].Value);
                        buildNumbers.Add(new BuildFailure
                        {
                            CreatedDate = new DateTime(reader.GetDateTimeOffset(2).Ticks),
                            Source = reader.GetString(0),
                            BuildNumber = buildNumber
                        });
                    }
                }

                reader.Close();
            }

            return buildNumbers;
        }

        public static int GetEquivalenceClassId(string log)
        {
            int clusterId = -1;

            if (equivalenceClasses.Count == 0)
            {
                using (SqlConnection connection = new SqlConnection(LocalConnectionString))
                {
                    SqlCommand command = new SqlCommand("SELECT [Name], [ID] FROM Cluster", connection);
                    connection.Open();

                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        equivalenceClasses.Add(new Cluster
                        {
                            Name = reader.GetString(0),
                            Id = reader.GetInt32(1)
                        });
                    }

                    reader.Close();
                }
            }

            clusterId = equivalenceClasses.Where(e => e.Name == log).Select(e => e.Id).FirstOrDefault();

            return clusterId;
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

        public static void InsertNewTaskFailures(BuildFailure buildFailure)
        {
            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                connection.Open();

                SqlCommand command =
                new SqlCommand(@"
                    INSERT INTO [dbo].[TaskFailure]
                               ([CreatedDate],
                               [VsoBuildId],
                               [Source],
                               [BuildDefinitionName],
                               [FailedTaskName])
                         VALUES
                               (@CreatedDate,
                                @VsoBuildId,
                                @Source,
                                @BuildDefinitionName,
                                @FailedTaskName)", connection);
                command.Parameters.AddWithValue("@CreatedDate", buildFailure.CreatedDate);
                command.Parameters.AddWithValue("@VsoBuildId", buildFailure.BuildNumber);
                command.Parameters.AddWithValue("@Source", buildFailure.Source);
                command.Parameters.AddWithValue("@BuildDefinitionName", buildFailure.BuildDefinitionName);
                command.Parameters.AddWithValue("@FailedTaskName", buildFailure.Failure);
                command.ExecuteNonQuery();
            }
        }

        public static void InsertNewFailuresLogs(List<BuildFailure> buildFailures)
        {
            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                connection.Open();

                foreach (BuildFailure buildFailure in buildFailures)
                {
                    SqlCommand command =
                    new SqlCommand(@"
                    INSERT INTO [dbo].[TaskFailureLogs]
                               ([CreatedDate]
                               ,[VsoBuildId]
                               ,[Source]
                               ,[BuildDefinitionName]
                               ,[FailedTaskName]
                               ,[MatchedError]
                               ,[LogUri]
                               ,[ClusterID])
                         VALUES
                               (@CreatedDate,
                                @VsoBuildId,
                                @Source,
                                @BuildDefinitionName,
                                @FailedTaskName,
                                @MatchedError,
                                @LogUri,
                                @ClusterID)", connection);
                    command.Parameters.AddWithValue("@CreatedDate", buildFailure.CreatedDate);
                    command.Parameters.AddWithValue("@VsoBuildId", buildFailure.BuildNumber);
                    command.Parameters.AddWithValue("@Source", buildFailure.Source);
                    command.Parameters.AddWithValue("@BuildDefinitionName", buildFailure.BuildDefinitionName);
                    command.Parameters.AddWithValue("@FailedTaskName", buildFailure.Failure);
                    command.Parameters.AddWithValue("@MatchedError", buildFailure.MatchedError);
                    command.Parameters.AddWithValue("@LogUri", buildFailure.LogUri);
                    command.Parameters.AddWithValue("@ClusterID", buildFailure.ClusterId);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static Guid InsertNewEquivalenceClass(string log)
        {
            Guid equivalenceClassId = Guid.NewGuid();

            using (SqlConnection connection = new SqlConnection(LocalConnectionString))
            {
                connection.Open();

                SqlCommand command =
                new SqlCommand(@"
                    INSERT INTO [dbo].[LevsEquivalenceClass]
                               ([ClassName]
                               ,[EquivalenceClassID])
                         VALUES
                               (@ClassName,
                                @Id)", connection);
                command.Parameters.AddWithValue("@ClassName", log);
                command.Parameters.AddWithValue("@Id", equivalenceClassId);
                command.ExecuteNonQuery();
            }

            return equivalenceClassId;
        }
    }
}
