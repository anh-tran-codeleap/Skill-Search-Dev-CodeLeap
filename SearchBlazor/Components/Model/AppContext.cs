using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SearchBlazor.Components.Model
{
    public class SkillContext : DbContext
    {
        public DbSet<Skill> Skills { get; set; }
        public DbSet<LuceneIndex> LuceneIndexes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LuceneIndex>()
                .HasOne(li => li.Skill)
                .WithMany()  // One skill may have one index (or more if history is kept)
                .HasForeignKey(li => li.SkillId)
                .OnDelete(DeleteBehavior.Cascade);  // Deletes index if skill is deleted
        }
    }
}