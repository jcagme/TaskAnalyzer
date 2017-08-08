using System;
using System.Collections.Generic;

namespace BuildLogClassifier
{
    internal class AzyncLazy<T> : Lazy<List<string>>
    {
        private object p;

        public AzyncLazy(object p)
        {
            this.p = p;
        }
    }
}