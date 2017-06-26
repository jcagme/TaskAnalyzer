namespace BuildTaskAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.TeamFoundation.Build.WebApi;
    using Microsoft.VisualStudio.Services.Common;
    using Microsoft.VisualStudio.Services.WebApi;

    static class VsoBuildClient
    {
        private static VssConnection connection;

        static VsoBuildClient()
        {
            Uri vsoCollectionUri = new Uri("https://devdiv.visualstudio.com/DefaultCollection");
            VssBasicCredential basicCredential = new VssBasicCredential(string.Empty, "<--VSTS Personal Acces Token-->");
            connection = new VssConnection(vsoCollectionUri, basicCredential);
        }

        public static List<BuildFailure> GetFailedTaskDataFromBuildAsync(DateTime createdDate, int buildNumber, string source, List<string> patterns)
        {
            List<BuildFailure> failures = new List<BuildFailure>();
            BuildHttpClient buildClient = connection.GetClientAsync<BuildHttpClient>().Result;
            Build buildData = buildClient.GetBuildAsync("DevDiv", buildNumber).Result;
            string buildDefName = buildData.Definition.Name;
            List<TimelineRecord> failedBuildTasks = GetFailedTasks(buildNumber);

            if (failedBuildTasks.Count > 0)
            {
                foreach (TimelineRecord record in failedBuildTasks)
                {
                    BuildFailure buildFailure = new BuildFailure
                    {
                        BuildDefinitionName = buildDefName,
                        BuildNumber = buildNumber,
                        CreatedDate = createdDate,
                        Failure = record.Name,
                        Source = source
                    };

                    SqlClient.InsertNewTaskFailures(buildFailure);

                    if (record.Log != null)
                    {
                        failures.AddRange(GetBuildsAndLogs(buildClient, buildNumber, record, buildDefName, createdDate, source, patterns));
                    }
                }
            }

            return failures;
        }

        public static List<BuildFailure> GetNewLogEntries(List<BuildFailure> buildWithNoLogs)
        {
            List<BuildFailure> buildFailuresWithLogs = new List<BuildFailure>();
            BuildHttpClient buildClient = connection.GetClientAsync<BuildHttpClient>().Result;
            List<string> patterns = SqlClient.GetPatterns();

            foreach (BuildFailure build in buildWithNoLogs)
            {
                try
                {
                    List<TimelineRecord> failedBuildTasks = GetFailedTasks(build.BuildNumber);
                    if (failedBuildTasks.Count > 0)
                    {
                        foreach (TimelineRecord record in failedBuildTasks)
                        {
                            if (record.Log != null)
                            {
                                buildFailuresWithLogs.AddRange(
                                    GetBuildsAndLogs(
                                        buildClient, 
                                        build.BuildNumber, 
                                        record, 
                                        build.BuildDefinitionName, 
                                        build.CreatedDate, 
                                        build.Source, 
                                        patterns));
                            }
                        }
                    }
                }
                catch
                { }
            }

            return buildFailuresWithLogs;
        }

        public static List<BuildFailure> GetFailedBuildsAsync()
        {
            BuildHttpClient buildClient = connection.GetClientAsync<BuildHttpClient>().Result;
            List<BuildFailure> failedBuilds = new List<BuildFailure>();
            DateTime? minDate = new DateTime(2016, 06, 01);
            int count = 0;
            int top = 1000;

            do
            {
                List<Build> builds = buildClient.GetBuildsAsync("DevDiv", minFinishTime: minDate, top: top).Result;
                List<Build> failedBs = builds.Where(b => b.Result == BuildResult.Failed).ToList();

                foreach (Build failedBuild in failedBs)
                {
                    failedBuilds.Add(new BuildFailure
                    {
                        BuildDefinitionName = failedBuild.Definition.Name,
                        BuildNumber = failedBuild.Id
                    });
                }

                minDate = builds.Max(b => b.FinishTime);
                count = builds.Count;
            } while (count == top);

            return failedBuilds;            
        }

        public static void GetWarningsFromBuildAsync(BuildFailure buildWarning)
        {
            List<string> warnings = new List<string>();
            BuildHttpClient buildClient = connection.GetClientAsync<BuildHttpClient>().Result;
            Build buildData = buildClient.GetBuildAsync("DevDiv", buildWarning.BuildNumber).Result;
            string buildDefName = buildData.Definition.Name;
            buildWarning.BuildDefinitionName = buildDefName;
            Timeline buildTimeline = buildClient.GetBuildTimelineAsync("DevDiv", buildWarning.BuildNumber).Result;
            List<List<Issue>> issuesList = buildTimeline.Records.Where(b => b.WarningCount > 0).Select(w => w.Issues).ToList();

            foreach (List<Issue> issues in issuesList)
            {
                List<string> issueList = issues.Select(i => i.Message).ToList();

                if (issueList.Count > 0)
                {
                    warnings.AddRange(issueList);
                }
            }

            buildWarning.Failure = string.Join(" & ", warnings);
        }

        private static List<TimelineRecord> GetFailedTasks(int buildNumber)
        {
            BuildHttpClient buildClient = connection.GetClientAsync<BuildHttpClient>().Result;
            Timeline buildTimeline = buildClient.GetBuildTimelineAsync("DevDiv", buildNumber).Result;
            List<TimelineRecord> failedBuildTasks = buildTimeline.Records.Where(b => b.Result == TaskResult.Failed && b.Name != "Build").ToList();
            return failedBuildTasks;
        }

        private static List<BuildFailure> GetBuildsAndLogs(
            BuildHttpClient buildClient, 
            int buildNumber, 
            TimelineRecord record, 
            string buildDefName,
            DateTime createdDate,
            string source,
            List<string> patterns)
        {
            List<BuildFailure> buildsWithLogs = new List<BuildFailure>();
            List<string> logs = buildClient.GetBuildLogLinesAsync("DevDiv", buildNumber, record.Log.Id).Result;

            foreach (string log in logs)
            {
                foreach (string pattern in patterns)
                {
                    Match m = Regex.Match(log, pattern);

                    if (m.Success)
                    {
                        string parsedLog = log;
                        Guid equivalenceClassId = default(Guid);
                        Match parsedLogMatch = Regex.Match(log, @"[\d]{4}-[\d]{2}-[\d]{2}T[\d]{2}:[\d]{2}:[\d]{2}\.[\d]*Z[\s]*([\d\w\W]*)");

                        if (parsedLogMatch.Success)
                        {
                            parsedLog = parsedLogMatch.Groups[1].Value;
                            parsedLog = NormalizeLog(parsedLog);
                            equivalenceClassId = SqlClient.GetEquivalenceClassId(parsedLog);
                        }

                        BuildFailure buildFailureWithLogs = new BuildFailure
                        {
                            BuildDefinitionName = buildDefName,
                            BuildNumber = buildNumber,
                            CreatedDate = createdDate,
                            EquivalenceClassId = equivalenceClassId,
                            LogUri = record.Log.Url,
                            Failure = record.Name,
                            MatchedError = parsedLog,
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
