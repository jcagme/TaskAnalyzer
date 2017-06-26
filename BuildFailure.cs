namespace BuildTaskAnalyzer
{
    using System;

    public class BuildFailure
    {
        public DateTime CreatedDate { get; set; }

        public int BuildNumber { get; set; }

        public string BuildDefinitionName { get; set; }

        public string Source { get; set; }

        public string Failure { get; set; }

        public string LogUri { get; set; }

        public string MatchedError { get; set; }

        public Guid EquivalenceClassId { get; set; } 
    }
}
