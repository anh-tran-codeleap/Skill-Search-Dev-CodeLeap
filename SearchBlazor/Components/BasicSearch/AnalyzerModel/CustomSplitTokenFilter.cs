using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;

namespace SearchBlazor.Components.BasicSearch.AnalyzerModel
{
    public class CustomSplitTokenFilter : TokenFilter
    {
        private readonly ICharTermAttribute termAttr;
        private readonly IPositionIncrementAttribute posAttr;
        private readonly Queue<string> tokenQueue = new Queue<string>();
        private readonly HashSet<char> splitChars;// List of delimiters

        public CustomSplitTokenFilter(TokenStream input, params char[] splitChars) : base(input)
        {
            termAttr = AddAttribute<ICharTermAttribute>();
            posAttr = AddAttribute<IPositionIncrementAttribute>();
            this.splitChars = new HashSet<char>(splitChars); // Convert list to HashSet for fast lookup
        }

        public sealed override bool IncrementToken()
        {
            // If there are buffered tokens, return them first
            if (tokenQueue.Count > 0)
            {
                termAttr.SetEmpty().Append(tokenQueue.Dequeue());
                posAttr.PositionIncrement = 0; // Keep tokens at the same position
                return true;
            }

            // Read the next token
            if (!m_input.IncrementToken())
                return false;

            string token = termAttr.ToString();

            // If token contains any of the split characters, generate variations
            if (token.IndexOfAny(splitChars.ToArray()) != -1)
            {
                string replaced = token;
                foreach (char c in splitChars)
                {
                    replaced = replaced.Replace(c, '_'); // Replace all delimiters with `_`
                }

                string[] parts = token.Split(splitChars.ToArray(), System.StringSplitOptions.RemoveEmptyEntries);

                tokenQueue.Enqueue(replaced); // "CI_CD_ML" (if CI/CD+ML is input)
                foreach (var part in parts)
                {
                    tokenQueue.Enqueue(part); // "CI", "CD", "ML"
                }
            }

            return true;
        }
    }
}