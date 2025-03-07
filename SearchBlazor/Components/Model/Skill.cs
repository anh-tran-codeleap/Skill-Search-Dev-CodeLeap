using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Facet;

namespace SearchBlazor.Components.Model
{
    public class Skill
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Versions { get; set; } = new List<string>();
        public string Group { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> Dependencies { get; set; } = new List<string>();
        public IList<FacetResult> FacetResults { get; set; } = new List<FacetResult>();
        public List<string> RelatedSkills { get; set; } = new List<string>();
    }
}