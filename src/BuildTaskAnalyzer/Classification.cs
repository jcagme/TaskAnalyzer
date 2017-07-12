namespace BuildTaskAnalyzer
{
    using System.Collections.Generic;

    public class Classification
    {
        public string Log { get; set; }

        public List<Map> Categories { get; set; }
    }

    public class Map
    {
        public string Category { get; set; }

        public int Class { get; set; }
    }
}
