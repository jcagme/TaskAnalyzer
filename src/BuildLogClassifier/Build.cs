namespace BuildLogClassifier
{
    using System;

    public class Build
    {
        public DateTime CreatedDate { get; set; }

        public int VsoBuildId { get; set; }

        public string BuildNumber { get; set; }

        public string BuildDefinitionName { get; set; }

        public string Source { get; set; }

        public string FailedTask { get; set; }

        public string LogUri { get; set; }

        public string ErrorLog { get; set; }

        public string Category { get; set; }

        public int Class { get; set; }

        public int JobId { get; set; }
    }
}
