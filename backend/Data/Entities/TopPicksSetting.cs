namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// The persisted on/off state of the public "Our Top Picks" surfaces, as set from the admin Top
/// Picks tab. Exactly one row is ever expected; <see cref="Services.TopPicksService"/> reads the
/// oldest and treats it as authoritative.
///
/// This exists so the gate can be flipped by an admin in the browser rather than only by a config
/// change and redeploy. <c>TopPicks:Enabled</c> stays in configuration as the seed default used
/// while no row exists, so switching the flag on in Fly and switching it on here mean the same
/// thing until someone actually uses the admin toggle — at which point the row wins.
///
/// Membership (which pieces are picked) lives in <see cref="TopPick"/>; only the gate lives here.
/// </summary>
public class TopPicksSetting : BaseEntity
{
    /// <summary>Whether the homepage strip, the /top-picks page and the nav link are public.</summary>
    public bool Enabled { get; set; }
}
