namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// Marker for entities that are genuinely meant to be removed from the database
/// (e.g. OAuth tokens rotated on reconnect, auto-generated rows that are rebuilt
/// rather than preserved). Entities implementing this bypass the global
/// <see cref="Interceptors.SoftDeleteInterceptor"/> and are hard-deleted as usual.
/// Everything else inheriting <see cref="BaseEntity"/> is soft-deleted automatically.
/// </summary>
public interface IHardDeletable;
