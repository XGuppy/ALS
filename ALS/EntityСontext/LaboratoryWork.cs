using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Antlr4.Runtime.Misc;

namespace ALS.EntityСontext
{
    public class LaboratoryWork
    {
        public int LaboratoryWorkId { get; set; }
        public int? TemplateLaboratoryWorkId { get; set; }
        [ForeignKey(nameof(Discipline))]
        [Required]
        public string DisciplineCipher { get; set; }
        [Required]
        [StringLength(150, MinimumLength=5)]
        public string Name { get; set; }
        [Required]
        [StringLength(256, MinimumLength=5)]
        public string Description { get; set; }
        public Evaluation Evaluation { get; set; }
        public string Cipher { get; set; }
        public int UserId { get; set; }
        [Required]
        [StringLength(256, MinimumLength=5)]
        [Column(TypeName = "jsonb")]
        public string Constraints { get; set; }
        public User User { get; set; }
        public Discipline Discipline { get; set; }
        public TemplateLaboratoryWork TemplateLaboratoryWork { get; set; }
        public List<Variant> Variants { get; set; }
    }
}