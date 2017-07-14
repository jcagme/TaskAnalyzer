namespace BuildTaskAnalyzer
{
    using System.Collections.Generic;

    public class Classification
    {
        public string Log { get; set; }

        public List<CategoryClassMap> Categories { get; set; }
    }

    public class CategoryClassMap
    {
        public string Category { get; set; }

        public int Class { get; set; }
    }
}
