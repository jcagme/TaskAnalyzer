namespace BuildLogClassifier
{
    using System;

    public class BuildSummaryItem
    {
        public DateTime BuildDate { get; set; }

        public string Branch { get; set; }

        public int SuccesfulBuildCount { get; set; }

        public int FailedBuildCount { get; set; }
    }
}
