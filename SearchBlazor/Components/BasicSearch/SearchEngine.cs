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
        private static RAMDirectory _directory = new RAMDirectory();
        public static IndexWriter? Writer { get; set; }

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

        public static void Index()
        {
            const LuceneVersion lv = LuceneVersion.LUCENE_48;
            Analyzer analyzer = new StandardAnalyzer(lv);
            _directory = new RAMDirectory();
            var config = new IndexWriterConfig(lv, analyzer);
            Writer = new IndexWriter(_directory, config);


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

                // var name = tech.Name ?? string.Empty;
                // var category = tech.Category ?? string.Empty;
                // var group = tech.Group ?? string.Empty;
                // var dependencies = string.Join(", ", tech.Dependencies ?? new List<string>());
                // var relatedSkills = string.Join(", ", tech.RelatedSkills ?? new List<string>());

                // var nameField = new StringField("Name", name, Field.Store.YES);
                // var categoryField = new StringField("Category", category, Field.Store.YES);
                // var groupField = new StringField("Group", group, Field.Store.YES);
                // var dependenciesField = new TextField("Dependencies", dependencies, Field.Store.YES);
                // var relatedSkillsField = new TextField("RelatedSkills", relatedSkills, Field.Store.YES);

                // var d = new Document
                // {
                //     nameField,
                //     categoryField,
                //     groupField,
                //     dependenciesField,
                //     relatedSkillsField
                // };

                Writer.AddDocument(d);
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
            //clean the search term
            string _input = EscapeSearchTerm(input.Trim());
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
            Analyzer a = new StandardAnalyzer(lv);
            var dirReader = DirectoryReader.Open(_directory);

            LuceneDictionary dictionary = new LuceneDictionary(dirReader, "Name");


            RAMDirectory _d = new RAMDirectory();
            AnalyzingInfixSuggester analyzingSuggester = new AnalyzingInfixSuggester(lv, _d, a);
            analyzingSuggester.Build(dictionary);

            var lookupResultList = analyzingSuggester.DoLookup(input.Trim(), false, 9);

            List<string> returnModel = new List<string>();
            foreach (var result in lookupResultList)
            {
                returnModel.Add(result.Key);
            }

            dirReader.Dispose();
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
            input = Regex.Replace(input, @"\~", " ");
            input = Regex.Replace(input, @"\*", " ");
            input = Regex.Replace(input, @"\?", " ");
            input = Regex.Replace(input, @"\:", " ");
            input = Regex.Replace(input, @"\\", " ");
            return input;
        }
    }
}