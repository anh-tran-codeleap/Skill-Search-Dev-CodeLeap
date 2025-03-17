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

        public async Task ReadIndexDocument()
        {
            using var reader = DirectoryReader.Open(_directory);

            Console.WriteLine($"Total Documents: {reader.NumDocs}");

            for (int i = 0; i < reader.MaxDoc; i++)
            {
                var doc = reader.Document(i);
                Console.WriteLine($"Document {i}:");

                foreach (var field in doc.Fields)
                {
                    Console.WriteLine($"  {field.Name}: {doc.Get(field.Name)}");
                }
            }
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

        public static List<SearchResult> SearchAhead(string input, int maxResults = 30)
        {
            const LuceneVersion lv = LuceneVersion.LUCENE_48;
            List<SearchResult> returnModel = new List<SearchResult>();

            using (var dirReader = DirectoryReader.Open(_directory))
            {
                var searcher = new IndexSearcher(dirReader);
                string[] searchFields = { "Name", "Group" };
                string _input = input.Trim().ToLowerInvariant();

                BooleanQuery booleanQuery = new BooleanQuery();

                // Normalize input by removing special characters
                string cleanInput = _input.Replace("#", "").Replace("+", "").Replace(".", "");

                // If input contains special characters, add multiple query variations
                if (_input.Contains("#") || _input.Contains("+") || _input.Contains("."))
                {
                    if (!string.IsNullOrEmpty(cleanInput))
                    {
                        foreach (var field in searchFields)
                        {
                            booleanQuery.Add(new FuzzyQuery(new Term(field, cleanInput), maxEdits: 2), Occur.SHOULD);
                        }
                    }
                    booleanQuery.Add(new TermQuery(new Term("Name", _input)), Occur.SHOULD);
                }
                else
                {
                    // Apply fuzzy search to both Name and Group
                    foreach (var field in searchFields)
                    {
                        booleanQuery.Add(new FuzzyQuery(new Term(field, _input), maxEdits: 2), Occur.SHOULD);
                    }
                }

                var topDocs = searcher.Search(booleanQuery, maxResults);

                foreach (var scoreDoc in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(scoreDoc.Doc);
                    string name = doc.Get("Name");
                    string group = doc.Get("Group");

                    // Ensure unique results
                    if (!returnModel.Any(r => r.Name == name))
                    {
                        returnModel.Add(new SearchResult { Name = name, Group = group });
                    }
                }
            }

            // Sort results by group for proper display in Blazor
            return returnModel.OrderBy(r => r.Group).ToList();
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