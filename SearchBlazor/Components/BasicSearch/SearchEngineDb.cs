using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Util;
using SearchBlazor.Components.Model;
using Directory = System.IO.Directory;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace SearchBlazor.Components.BasicSearch
{
    public class SearchEngineDB
    {
        public static List<Skill> Data { get; set; } = new List<Skill>();
        public static IndexWriter? Writer { get; set; }
        private readonly SkillContext _context = new();

        public SearchEngineDB(SkillContext context)
        {
            _context = context;
        }

        public static void LoadDataFromDatabase()
        {
            using var context = new SkillContext();
            Data = context.Skills.ToList();

            Console.WriteLine($"Loaded {Data.Count} skills from the database.");
        }

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

            //store on db
            using var context = new SkillContext();


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

                string indexedData = SerializeDocument(d);

                // Save to database
                var indexEntry = new LuceneIndex
                {
                    //TODO: check if this is needed
                    //  SkillId = tech.Id,
                    IndexedData = indexedData
                };

                context.LuceneIndexes.Add(indexEntry);
            }
        }

        private static string SerializeDocument(Document doc)
        {
            return string.Join("|", doc.Fields.Select(f => $"{f.Name}={f.GetStringValue()}"));
        }

        public static SearchModel Search(string input, int page)
        {
            using var dbContext = new SkillContext();
            input = EscapeSearchTerm(input);
            var query = dbContext.LuceneIndexes
                .Where(l => l.IndexedData.Contains(input))
                .Select(l => new
                {
                    l.SkillId,
                    l.Skill
                });

            int totalResults = query.Count();
            int pageSize = 5;
            int pageCount = (int)Math.Ceiling(totalResults / (double)pageSize);

            var results = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new SearchModel
            {
                SearchResults = results.Select(r => r.Skill).ToList(),
                SearchText = input.Trim(),
                ResultsCount = totalResults,
                PageCount = pageCount,
                CurrentPage = page
            };
        }


        public static List<string> SearchAhead(string input)
        {
            using var dbContext = new SkillContext();

            return dbContext.LuceneIndexes
                .Where(l => l.IndexedData.Contains(input))
                .Select(l => l.Skill.Name)
                .Distinct()
                .Take(9)
                .ToList();
        }

        public static void IndexDatabase()
        {
            using var dbContext = new SkillContext();

            if (dbContext.LuceneIndexes.Any())
            {
                Console.WriteLine("Index already exists in database. Skipping.");
                return;
            }

            Console.WriteLine("Creating database index...");

            var indexedSkills = Data.Select(skill => new LuceneIndex
            {
                //TODO:  SkillId = skill.Id, // Make sure Skill has an Id
                IndexedData = $"{skill.Name} {skill.Group} {skill.Category} {string.Join(", ", skill.Dependencies)} {string.Join(", ", skill.RelatedSkills)}"
            });

            dbContext.LuceneIndexes.AddRange(indexedSkills);
            dbContext.SaveChanges();

            Console.WriteLine("Index created successfully.");
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