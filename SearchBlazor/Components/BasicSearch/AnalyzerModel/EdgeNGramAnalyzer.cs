using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.NGram;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;

namespace SearchBlazor.Components.BasicSearch.AnalyzerModel
{
    public class EdgeNGramAnalyzer : Analyzer
    {
        private LuceneVersion _version;

        public EdgeNGramAnalyzer(LuceneVersion version)
        {
            _version = version;
        }

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var tokenizer = new StandardTokenizer(_version, reader);
            TokenStream filter = new StandardFilter(_version, tokenizer);
            filter = new LowerCaseFilter(_version, filter);
            filter = new EdgeNGramTokenFilter(_version, filter, 1, 10); // Generates partial words
            return new TokenStreamComponents(tokenizer, filter);
        }
    }
}