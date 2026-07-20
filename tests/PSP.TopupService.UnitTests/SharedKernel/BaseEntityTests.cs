using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.UnitTests.SharedKernel;

/// <summary>
/// Verifies the BaseEntity audit/concurrency/event behaviour used by every aggregate.
/// </summary>
public class BaseEntityTests
{
    [Fact]
    public void Ctor_Default_Should_Assign_New_Id_And_Audit_Timestamps()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var entity = new TestEntity();
        var after = DateTime.UtcNow.AddSeconds(1);

        entity.Id.Should().NotBeEmpty();
        entity.CreatedOnUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        entity.ModifiedOnUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        entity.IsDeleted.Should().BeFalse();
        entity.DeletedOnUtc.Should().BeNull();
    }

    [Fact]
    public void Ctor_With_Empty_Guid_Should_Throw()
    {
        var act = () => new TestEntity(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoftDelete_Should_Mark_Entity_And_Set_Timestamp()
    {
        var entity = new TestEntity();

        entity.SoftDelete("operator-1");

        entity.IsDeleted.Should().BeTrue();
        entity.DeletedOnUtc.Should().NotBeNull();
        entity.ModifiedBy.Should().Be("operator-1");
    }

    [Fact]
    public void SoftDelete_Should_Be_Idempotent()
    {
        var entity = new TestEntity();
        entity.SoftDelete();
        var firstModified = entity.ModifiedOnUtc;

        Thread.Sleep(10);
        entity.SoftDelete();

        entity.ModifiedOnUtc.Should().Be(firstModified);
    }

    [Fact]
    public void Restore_Should_Clear_SoftDelete_State()
    {
        var entity = new TestEntity();
        entity.SoftDelete();

        entity.Restore();

        entity.IsDeleted.Should().BeFalse();
        entity.DeletedOnUtc.Should().BeNull();
    }

    [Fact]
    public void RaiseEvent_Should_Queue_DomainEvent()
    {
        var entity = new TestEntity();

        entity.RaiseTestEvent();

        entity.DomainEvents.Should().ContainSingle();
        entity.DomainEvents.First().Should().BeOfType<TestDomainEvent>();
    }

    [Fact]
    public void ClearDomainEvents_Should_Empty_Collection()
    {
        var entity = new TestEntity();
        entity.RaiseTestEvent();

        entity.ClearDomainEvents();

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Equals_Should_Compare_Type_And_Id()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id);
        var b = new TestEntity(id);
        var c = new TestEntity();

        a.Equals(b).Should().BeTrue();
        a.Equals(c).Should().BeFalse();
        (a == b).Should().BeTrue();
        (a != c).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_Should_Be_Stable_For_Same_Id()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id);
        var b = new TestEntity(id);

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    private sealed class TestEntity : BaseEntity
    {
        public TestEntity()
        {
        }

        public TestEntity(Guid id)
            : base(id)
        {
        }

        public void RaiseTestEvent() => RaiseEvent(new TestDomainEvent());
    }

    private sealed class TestDomainEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
    }
}
