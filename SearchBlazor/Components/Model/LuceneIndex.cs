using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SearchBlazor.Components.Model
{
    public class LuceneIndex
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("Skill")]
        public int SkillId { get; set; }

        public string IndexedData { get; set; }

        public Skill Skill { get; set; }
    }

}