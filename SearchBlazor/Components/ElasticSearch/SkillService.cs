using Nest;
using Newtonsoft.Json;
using SearchBlazor.Components.Model;

namespace SearchBlazor.Components.ElasticSearch
{
    public class SkillService
    {
        private readonly IElasticClient _elasticClient;
        private readonly IWebHostEnvironment _env;

        public SkillService(IElasticClient elasticClient, IWebHostEnvironment env)
        {
            _elasticClient = elasticClient;
            _env = env;
        }

        public async Task IndexSkillsFromJsonAsync()
        {
            string jsonDirectory = Path.Combine(_env.WebRootPath, "json-files");

            if (!Directory.Exists(jsonDirectory))
            {
                Console.WriteLine("JSON directory not found.");
                return;
            }

            var jsonFiles = Directory.GetFiles(jsonDirectory, "*.json"); // Get all JSON files
            List<Skill> allSkills = new();

            foreach (var file in jsonFiles)
            {
                try
                {
                    string jsonContent = await File.ReadAllTextAsync(file);
                    var skills = JsonConvert.DeserializeObject<List<Skill>>(jsonContent);

                    if (skills != null)
                    {
                        allSkills.AddRange(skills);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading file {file}: {ex.Message}");
                }
            }

            if (allSkills.Count > 0)
            {
                var bulkIndexResponse = await _elasticClient.IndexManyAsync(allSkills, "skills");
                if (!bulkIndexResponse.IsValid)
                {
                    Console.WriteLine($"Indexing failed: {bulkIndexResponse.DebugInformation}");
                }
                else
                {
                    Console.WriteLine($"Successfully indexed {allSkills.Count} skills.");
                }
            }
            else
            {
                Console.WriteLine("No skills found in JSON files.");
            }
        }


        public async Task<List<Skill>> SearchSkillsAsync(string query)
        {
            var response = await _elasticClient.SearchAsync<Skill>(s => s
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(f => f.Field(s => s.Name).Field(s => s.Category).Field(s => s.Versions).Field(s => s.Group).Field(s => s.Dependencies).Field(s => s.RelatedSkills))
                        .Query(query)
                        .Fuzziness(Fuzziness.Auto)
                    )
                )
            );

            return response.Documents.ToList();
        }
    }
}