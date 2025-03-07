using System.ComponentModel.DataAnnotations;
using SearchBlazor.Components.Model;

namespace SearchBlazor.Components.BasicSearch
{
    public class SearchModel
    {
        [Required]
        public string SearchText { get; set; } = string.Empty;
        public int ResultsCount { get; set; }
        //pagination 
        public int PageCount { get; set; }
        public int CurrentPage { get; set; }
        public List<Skill> SearchResults { get; set; } = new List<Skill>();
    }
}