namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// A garment being listed through the seller tool, and the root of its evidence set.
///
/// Deliberately NOT keyed on a brand: a garment with the label cut out is a first-class
/// citizen here. Identity is the physical item plus whatever evidence was captured from
/// it; the maker may never be known and the record is still complete and sellable.
/// </summary>
public class Garment : BaseEntity
{
    /// <summary>Owning seller. Null for Eden Relics' own stock (beta tester zero).</summary>
    public Guid? SellerId { get; set; }
    public Seller? Seller { get; set; }

    /// <summary>The seller's own reference for the item, so they can find it again.</summary>
    public string Reference { get; set; } = "";

    /// <summary>Working title. Not a date claim.</summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// The era the seller believes it is from, if they have stated one. Checked against
    /// the evidence rather than trusted — a claim that contradicts a HARD bound is exactly
    /// what the tool exists to catch.
    /// </summary>
    public DateOnly? ClaimedEraStart { get; set; }
    public DateOnly? ClaimedEraEnd { get; set; }

    /// <summary>Evidence captured from the physical item.</summary>
    public List<GarmentEvidence> Evidence { get; set; } = [];

    /// <summary>Assessments produced from that evidence, newest last.</summary>
    public List<DatingAssessment> Assessments { get; set; } = [];
}
