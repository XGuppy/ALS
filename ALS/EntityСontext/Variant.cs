using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ALS.EntityСontext
{
    public class Variant
    {
        public int VariantId { get; set; }
        public int LaboratoryWorkId { get; set; }
        public int VariantNumber { get; set; }
        [StringLength(1024 ,MinimumLength=5)]
        public string Description { get; set; }
        public string LinkToModel { get; set; }
        [Column(TypeName = "jsonb")]
        public string InputDataRuns { get; set; }
        [Column(TypeName = "jsonb")]
        public string Constraints { get; set; }
        public List<AssignedVariant> AssignedVariants { get; set; }
        public LaboratoryWork LaboratoryWork { get; set; }
    }
}