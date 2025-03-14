using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Util;
using SearchBlazor.Components.Model;
using Directory = System.IO.Directory;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos;
using SearchBlazor.Components.CosmosDb;
using Lucene.Net.Store;
using System.Threading.Tasks;
using Lucene.Net.Search;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Analysis.Core;
using SearchBlazor.Components.BasicSearch.AnalyzerModel;

namespace SearchBlazor.Components.BasicSearch
{
    public class SearchEngineDB
    {
        public List<Skill> Data { get; set; } = new List<Skill>();
        private static FSDirectory _directory;
        public static IndexWriter? Writer { get; set; }
        public const string LUCENENET_DIRECTORY = "LuceneIndex";

        private readonly CosmosDbService _cosmosDbService;
        public SearchEngineDB(CosmosDbService cosmosDbService)
        {
            _cosmosDbService = cosmosDbService;
        }

        public async Task LoadDataFromCosmosDB()
        {
            try
            {
                var container = _cosmosDbService.GetContainer("SkillSets");
                var query = container.GetItemQueryIterator<Skill>("SELECT * FROM c");
                List<Skill> skills = new List<Skill>();

                while (query.HasMoreResults)
                {
                    FeedResponse<Skill> response = await query.ReadNextAsync();
                    skills.AddRange(response);
                }

                Data = skills;
                Console.WriteLine($"Loaded {Data.Count} skills from Cosmos DB.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching data from Cosmos DB: {ex.Message}");
                Data = new List<Skill>();
            }
        }
        public async Task EnsureIndex()
        {
            string indexPath = Path.Combine(Directory.GetCurrentDirectory(), LUCENENET_DIRECTORY);

            // Check if index directory exists and contains index files
            if (Directory.Exists(indexPath) && Directory.GetFiles(indexPath).Length > 0)
            {
                _directory = FSDirectory.Open(indexPath);
                Console.WriteLine("Lucene index already exists. Skipping indexing.");
                return;
            }

            Console.WriteLine("Index not found. Creating a new index...");
            await Index();
        }
        public async Task Index()
        {
            await LoadDataFromCosmosDB();
            const LuceneVersion lv = LuceneVersion.LUCENE_48;
            // Analyzer analyzer = new StandardAnalyzer(lv);
            Analyzer analyzer = new CustomAnalyzer(lv, '/', '+', '-', '.');
            // Analyzer analyzer = new StandardAnalyzer(lv);
            //   Analyzer analyzer = new EdgeNGramAnalyzer(lv);
            string indexPath = Path.Combine(Directory.GetCurrentDirectory(), LUCENENET_DIRECTORY);
            _directory = FSDirectory.Open(indexPath);

            var config = new IndexWriterConfig(lv, analyzer);
            Writer = new IndexWriter(_directory, config);

            foreach (var tech in Data)
            {
                var doc = new Document
                {
                    new TextField("Name", tech.Name, Field.Store.YES),
                    new TextField("Category", tech.Category, Field.Store.YES),
                    new TextField("Group", tech.Group, Field.Store.YES),
                    new TextField("Dependencies", string.Join(", ", tech.Dependencies), Field.Store.YES),
                    new TextField("RelatedSkills", string.Join(", ", tech.RelatedSkills), Field.Store.YES)
                };

                Writer.AddDocument(doc);
            }

            Writer.Commit();
        }
        public static void Dispose()
        {
            Writer?.Dispose();
            _directory?.Dispose();
        }

        public static SearchModel Search(string input, int page)
        {
            const LuceneVersion lv = LuceneVersion.LUCENE_48;
            Analyzer analyzer = new StandardAnalyzer(lv);

            var dirReader = DirectoryReader.Open(_directory);
            var searcher = new IndexSearcher(dirReader);

            string[] fields = ["Name", "Group", "Category", "Dependencies", "RelatedSkills"];
            var queryParser = new MultiFieldQueryParser(lv, fields, analyzer);
            queryParser.DefaultOperator = Operator.AND;

            string modifiedQuery = $"{input.Trim()}";

            //clean the search term
            string _input = EscapeSearchTerm(modifiedQuery);

            //normal search with the indexed data before
            Query query = queryParser.Parse(_input);

            ScoreDoc[] docs = searcher.Search(query, 1000).ScoreDocs;

            foreach (var doc in searcher.Search(query, 1000).ScoreDocs)
            {
                var document = searcher.Doc(doc.Doc);
                Console.WriteLine($"Indexed Name: {document.Get("Name")}");
            }

            var returnModel = new SearchModel();
            returnModel.SearchResults = new List<Skill>();
            returnModel.SearchText = input.Trim();
            returnModel.ResultsCount = docs.Length;
            returnModel.PageCount = (int)Math.Ceiling(docs.Length / 5.0);
            returnModel.CurrentPage = page;

            int first = (page - 1) * 5;
            int last = first + 5;

            for (int i = first; i < last && i < docs.Length; i++)
            {
                Document doc = searcher.Doc(docs[i].Doc);
                returnModel.SearchResults.Add(new Skill
                {
                    Name = doc.Get("Name"),
                    Group = doc.Get("Group"),
                    Category = doc.Get("Category"),
                    Dependencies = doc.Get("Dependencies")?.Split(", ").ToList() ?? new List<string>(),
                    RelatedSkills = doc.Get("RelatedSkills")?.Split(", ").ToList() ?? new List<string>()
                });
            }
            dirReader.Dispose();
            return returnModel;
        }

        public static List<string> SearchAhead(string input)
        {
            const LuceneVersion lv = LuceneVersion.LUCENE_48;
            List<string> returnModel = new List<string>();

            //    using (Analyzer analyzer = new StandardAnalyzer(lv))
            using (var dirReader = DirectoryReader.Open(_directory))
            {
                var searcher = new IndexSearcher(dirReader);
                var fieldName = "Name";
                var _input = input.Trim().ToLower();

                Query query;

                if (_input.Contains("#") || _input.Contains("+") || _input.Contains("."))
                {

                    BooleanQuery bq = new BooleanQuery();
                    TermQuery termQuery = new TermQuery(new Term(fieldName, _input));
                    bq.Add(termQuery, Occur.SHOULD);

                    string cleanInput = _input.ToLowerInvariant().Replace("#", "").Replace("+", "").Replace(".", "");
                    if (!string.IsNullOrEmpty(cleanInput))
                    {
                        FuzzyQuery fuzzyQuery = new FuzzyQuery(new Term(fieldName, cleanInput), maxEdits: 2, 0, 100, true);
                        bq.Add(fuzzyQuery, Occur.SHOULD);
                    }

                    query = bq;
                }
                else
                {
                    // Standard fuzzy query for regular terms
                    query = new FuzzyQuery(new Term(fieldName, _input.ToLowerInvariant()), maxEdits: 2, 0, 100, true);
                }

                var topDocs = searcher.Search(query, 9); // Get top 9 results

                foreach (var scoreDoc in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(scoreDoc.Doc);
                    returnModel.Add(doc.Get(fieldName));
                }
            }

            return returnModel;
        }

        private static string EscapeSearchTerm(string input)
        {
            input = Regex.Replace(input, @"<b>", "");
            input = Regex.Replace(input, @"</b>", "");
            input = Regex.Replace(input, @"\+", " ");
            input = Regex.Replace(input, @"\/", " ");
            input = Regex.Replace(input, @"\-", " ");
            input = Regex.Replace(input, @"\&", " ");
            input = Regex.Replace(input, @"\|", " ");
            input = Regex.Replace(input, @"\!", " ");
            input = Regex.Replace(input, @"\(", " ");
            input = Regex.Replace(input, @"\)", " ");
            input = Regex.Replace(input, @"\{", " ");
            input = Regex.Replace(input, @"\}", " ");
            input = Regex.Replace(input, @"\[", " ");
            input = Regex.Replace(input, @"\]", " ");
            input = Regex.Replace(input, @"\^", " ");
            input = Regex.Replace(input, @"\""", " ");
            //  input = Regex.Replace(input, @"\~", " ");
            input = Regex.Replace(input, @"\*", " ");
            input = Regex.Replace(input, @"\?", " ");
            input = Regex.Replace(input, @"\:", " ");
            input = Regex.Replace(input, @"\\", " ");
            return input;
        }
    }
}