namespace BuildTaskAnalyzer
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class Helpers
    {
        public static void GetPotentialKeywords()
        {
            HashSet<string> potentialKeywords = new HashSet<string>();
            var failures = SqlClient.GetUniqueFailureLogs();

            foreach (var failure in failures)
            {
                var words = failure.Split(' ').Where(w => w.Length > 3 && w.Length < 20);

                foreach (var w in words)
                {
                    if (!potentialKeywords.Contains(w))
                    {
                        potentialKeywords.Add(w);
                    }
                }
            }

            File.WriteAllLines(@".\Keywords.txt", potentialKeywords.ToList().OrderBy(w => w));
        }
    }
}
