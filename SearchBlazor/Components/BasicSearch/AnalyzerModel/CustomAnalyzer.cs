using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.CharFilters;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;

namespace SearchBlazor.Components.BasicSearch.AnalyzerModel
{
    public class CustomAnalyzer : Analyzer
    {
        private readonly LuceneVersion matchVersion;
        private readonly char[] splitChars;

        public CustomAnalyzer(LuceneVersion matchVersion, params char[] splitChars)
        {
            this.matchVersion = matchVersion;
            this.splitChars = splitChars;
        }

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var tokenizer = new StandardTokenizer(matchVersion, reader);
            TokenStream filter = new LowerCaseFilter(matchVersion, tokenizer);
            filter = new CustomSplitTokenFilter(filter, splitChars);
            return new TokenStreamComponents(tokenizer, filter);
        }

        // protected override TextReader InitReader(string fieldName, TextReader reader)
        // {
        //     var charMapBuilder = new NormalizeCharMap.Builder();
        //     charMapBuilder.Add("/", "_");  // Replace `/` with `_`
        //     charMapBuilder.Add("-", "_");  // Replace `-` with `_`
        //     NormalizeCharMap charMap = charMapBuilder.Build();

        //     return new MappingCharFilter(charMap, reader);
        // }
    }
}