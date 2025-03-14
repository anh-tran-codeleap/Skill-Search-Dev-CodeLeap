using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using SearchBlazor.Components.Model;
using Directory = System.IO.Directory;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Search.Spell;
using Lucene.Net.Search.Suggest.Analyzing;

namespace SearchBlazor.Components.BasicSearch
{
    public class SearchEngine
    {
        public static List<Skill> Data { get; set; } = new List<Skill>();
        /// <summary>
        /// Directory to store the index on disk
        /// if want to search on RAM, use RAMDirectory: _directory = new RAMDirectory();    and uncomment the third line in Index() method
        /// if want to search on Disk, use FSDirectory: _directory = FSDirectory.Open(indexPath);
        /// </summary>
        private static RAMDirectory _ramDirectory = new();
        private static FSDirectory _directory;
        public static IndexWriter? Writer { get; set; }
        public const string LUCENENET_DIRECTORY = "LuceneIndex";


        public static void LoadDataFromJson()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "json", "skills_database.json");
            try
            {
                string json = File.ReadAllText(filePath);
                Data = JsonConvert.DeserializeObject<List<Skill>>(json) ?? new List<Skill>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading JSON file: {ex.Message}");
                Data = new List<Skill>();
            }
        }

        #region Indexing the data that store on Disk
        public static void EnsureIndex()
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
            Index1();
        }

        /// <summary>
        /// this index using to save the indexed data on disk
        /// </summary>
        public static void Index1()
        {
            LoadDataFromJson();
            const LuceneVersion lv = LuceneVersion.LUCENE_48;
            Analyzer analyzer = new StandardAnalyzer(lv);
            //Analyzer analyzer = new EdgeNGramAnalyzer(lv);
            // Store index in a persistent directory
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
        #endregion

        /// <summary>
        /// this index using to save the indexed data on RAM
        /// </summary>
        #region Searching the data that store on RAM
        public static void Index()
        {
            LoadDataFromJson();
            const LuceneVersion lv = LuceneVersion.LUCENE_48;
            Analyzer analyzer = new StandardAnalyzer(lv);
            _ramDirectory = new RAMDirectory();


            var config = new IndexWriterConfig(lv, analyzer);
            Writer = new IndexWriter(_ramDirectory, config);


            var nameField = new TextField("Name", "", Field.Store.YES);
            var categoryField = new TextField("Category", "", Field.Store.YES);
            var groupField = new TextField("Group", "", Field.Store.YES);
            var dependenciesField = new TextField("Dependencies", "", Field.Store.YES);
            var relatedSkillsField = new TextField("RelatedSkills", "", Field.Store.YES);

            var d = new Document
                {
                    nameField,
                    categoryField,
                    groupField,
                    dependenciesField,
                    relatedSkillsField
                };
            foreach (var tech in Data)
            {
                nameField.SetStringValue(tech.Name);
                categoryField.SetStringValue(tech.Category);
                groupField.SetStringValue(tech.Group);
                dependenciesField.SetStringValue(string.Join(", ", tech.Dependencies));
                relatedSkillsField.SetStringValue(string.Join(", ", tech.RelatedSkills));

                Writer.AddDocument(d);
            }
            Writer.Commit();
        }
        #endregion

        public static void Dispose()
        {
            Writer?.Dispose();
            _ramDirectory?.Dispose();
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

            string modifiedQuery = $"{input.Trim()}~1";

            //clean the search term
            string _input = EscapeSearchTerm(modifiedQuery);

            // BooleanQuery query = new BooleanQuery();
            // foreach (var field in fields)
            // {
            //     var term = new Term(field, _input);
            //     var fuzzyQuery = new FuzzyQuery(term, 1); // Edit distance = 1 (you can increase to 2 if needed)
            //     query.Add(fuzzyQuery, Occur.SHOULD); // SHOULD makes it match any field
            // }

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

        public static List<string> SearchAhead2(string input)
        {
            const LuceneVersion lv = LuceneVersion.LUCENE_48;
            Analyzer a = new StandardAnalyzer(lv);
            var dirReader = DirectoryReader.Open(_ramDirectory);

            LuceneDictionary dictionary = new LuceneDictionary(dirReader, "Name");

            RAMDirectory _d = new RAMDirectory();
            AnalyzingInfixSuggester analyzingSuggester = new AnalyzingInfixSuggester(lv, _d, a);
            analyzingSuggester.Build(dictionary);

            string fuzzyInput = input.Trim();
            //Get initial suggestions (prefix search)
            var lookupResultList = analyzingSuggester.DoLookup(fuzzyInput, false, 9);

            List<string> returnModel = new List<string>();
            foreach (var result in lookupResultList)
            {
                returnModel.Add(result.Key);
            }

            dirReader.Dispose();
            return returnModel;
        }

        public static List<string> SearchAhead(string input)
        {
            const LuceneVersion lv = LuceneVersion.LUCENE_48;
            List<string> returnModel = new List<string>();

            using (Analyzer analyzer = new StandardAnalyzer(lv))
            using (var dirReader = DirectoryReader.Open(_ramDirectory))
            {
                var searcher = new IndexSearcher(dirReader);
                var fieldName = "Name";
                var _input = input.Trim().ToLower();


                //  var fuzzyQuery = new FuzzyQuery(new Term(fieldName, _input), maxEdits: 2, 0, 100, true);
                // Handle special characters properly
                Query query;

                if (_input.Contains("#") || _input.Contains("+") || _input.Contains("."))
                {
                    // For terms with special characters, use a TermQuery for exact matching
                    // and a FuzzyQuery for fuzzy matching
                    BooleanQuery bq = new BooleanQuery();

                    // Exact term match
                    TermQuery termQuery = new TermQuery(new Term(fieldName, _input));
                    bq.Add(termQuery, Occur.SHOULD);

                    // Fuzzy match on the cleaned input
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