// --- START OF NEW FILE: Domain/Common/BaseEntity.cs ---

namespace Domain.Common
{
    /// <summary>
    /// An abstract base class for all domain entities. It provides a common
    /// generic identifier property.
    /// </summary>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    public abstract class BaseEntity<TId>
    {
        /// <summary>
        /// Gets or sets the unique identifier for this entity.
        /// </summary>
        public TId Id { get; protected set; } = default!;

        // You can add other common properties here in the future, for example:
        // public DateTime CreatedAt { get; set; }
        // public DateTime? UpdatedAt { get; set; }
    }
}