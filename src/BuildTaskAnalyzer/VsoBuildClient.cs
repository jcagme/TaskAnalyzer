namespace BuildTaskAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.TeamFoundation.Build.WebApi;
    using Microsoft.VisualStudio.Services.Common;
    using Microsoft.VisualStudio.Services.WebApi;

    static class VsoBuildClient
    {
        private static VssConnection _connection;

        static VsoBuildClient()
        {
            Uri vsoCollectionUri = new Uri("https://devdiv.visualstudio.com/DefaultCollection");
            string vstsAccessToken = SettingsManager.GetStagingSetting("LogAnalysisVstsPersonalAccessToken");
            VssBasicCredential basicCredential = new VssBasicCredential(string.Empty, vstsAccessToken);
            _connection = new VssConnection(vsoCollectionUri, basicCredential);
        }

        public static List<Build> GetFailedTaskDataFromBuild(DateTime createdDate, int vsoBuildId, string buildNumber, int jobId, string source, List<string> patterns)
        {
            List<Build> failures = new List<Build>();
            BuildHttpClient buildClient = _connection.GetClientAsync<BuildHttpClient>().Result;
            Microsoft.TeamFoundation.Build.WebApi.Build buildData = buildClient.GetBuildAsync("DevDiv", vsoBuildId).Result;
            string buildDefName = buildData.Definition.Name;
            List<TimelineRecord> failedBuildTasks = GetFailedTasks(vsoBuildId);
            
            if (failedBuildTasks!= null && failedBuildTasks.Count > 0)
            {
                foreach (TimelineRecord record in failedBuildTasks)
                {
                    if (record.Log != null)
                    {
                        failures.AddRange(GetLogsForBuild(buildClient, vsoBuildId, buildNumber, jobId, record, buildDefName, createdDate, source, patterns));
                    }
                }
            }

            return failures;
        }

        private static List<TimelineRecord> GetFailedTasks(int buildNumber)
        {
            BuildHttpClient buildClient = _connection.GetClientAsync<BuildHttpClient>().Result;
            Timeline buildTimeline = buildClient.GetBuildTimelineAsync("DevDiv", buildNumber).Result;

            if (buildTimeline != null)
            {
                List<TimelineRecord> failedBuildTasks = buildTimeline.Records.Where(b => b.Result == TaskResult.Failed && b.Name != "Build").ToList();
                return failedBuildTasks;
            }

            return null;
        }

        private static List<Build> GetLogsForBuild(
            BuildHttpClient buildClient,
            int vsoBuildId,
            string buildNumber,
            int jobId,
            TimelineRecord record,
            string buildDefName,
            DateTime createdDate,
            string source,
            List<string> patterns)
        {
            List<Build> buildsWithLogs = new List<Build>();
            List<string> logs = buildClient.GetBuildLogLinesAsync("DevDiv", vsoBuildId, record.Log.Id).Result;

            foreach (string log in logs)
            {
                foreach (string pattern in patterns)
                {
                    Match m = Regex.Match(log, pattern);

                    if (m.Success)
                    {
                        string parsedLog = log;
                        Match parsedLogMatch = Regex.Match(log, @"[\d]{4}-[\d]{2}-[\d]{2}T[\d]{2}:[\d]{2}:[\d]{2}\.[\d]*Z[\s]*([\d\w\W]*)");

                        if (parsedLogMatch.Success)
                        {
                            parsedLog = parsedLogMatch.Groups[1].Value;
                            parsedLog = NormalizeLog(parsedLog);
                        }

                        Build buildFailureWithLogs = new Build
                        {
                            BuildDefinitionName = buildDefName,
                            VsoBuildId = vsoBuildId,
                            BuildNumber = buildNumber,
                            CreatedDate = createdDate,
                            JobId = jobId,
                            LogUri = record.Log.Url,
                            FailedTask = record.Name,
                            ErrorLog = parsedLog,
                            Source = source
                        };

                        buildsWithLogs.Add(buildFailureWithLogs);
                        break;
                    }
                }
            }

            return buildsWithLogs;
        }

        private static string NormalizeLog(string log)
        {
            Regex d = new Regex(@"work\\[\d]*");
            log = d.Replace(log, @"work\0");
            d = new Regex(@"work/[\d]*");
            log = d.Replace(log, @"work/0");
            d = new Regex(@"\([\d]*,[\d]*\)");
            log = d.Replace(log, "(0,0)");
            d = new Regex(@"checked", RegexOptions.IgnoreCase);
            log = d.Replace(log, "platform");
            d = new Regex(@"debug", RegexOptions.IgnoreCase);
            log = d.Replace(log, "platform");
            d = new Regex(@"release", RegexOptions.IgnoreCase);
            log = d.Replace(log, "platform");
            return log;
        }
    }
}
