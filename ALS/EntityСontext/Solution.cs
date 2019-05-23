using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ALS.EntityСontext
{
    public class Solution
    {
        public int SolutionId { get; set; }
        public int VariantId { get; set; }
        [Range(3, 5)]
        public int? Mark { get; set; }
        [Required]
        public DateTime SendDate { get; set; }
        [Required]
        [Column(TypeName = "jsonb")]
        public string SourceCode { get; set; }
        public Variant Variant { get; set; }
        public List<TestRun> TestRuns { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public bool IsSolved { get; set; }
    }
}