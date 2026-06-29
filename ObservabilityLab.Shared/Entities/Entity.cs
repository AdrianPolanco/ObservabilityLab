
namespace ObservabilityLab.Shared.Entities
{
    public abstract class Entity
    {
        public Guid Id { get; private set; } = Guid.NewGuid();

        public override bool Equals(object? obj) =>
            obj is Entity other && GetType() == other.GetType() && Id == other.Id;

        public override int GetHashCode() => GetType().GetHashCode() ^ Id.GetHashCode();

        public static bool operator ==(Entity? left, Entity? right) =>
            left is null ? right is null : left.Equals(right);

        public static bool operator !=(Entity? left, Entity? right) => !(left == right);
    }
}
