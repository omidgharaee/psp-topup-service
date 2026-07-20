using System.Linq.Expressions;

namespace PSP.TopupService.SharedKernel.Specifications;

/// <summary>
/// A specification encapsulates a query intent against an aggregate in a
/// declarative, composable way. Implementations live in the Application layer;
/// repositories accept a specification and translate it to the underlying query.
/// Used only where a plain LINQ predicate is not expressive enough.
/// </summary>
/// <typeparam name="T">The aggregate root type the specification targets.</typeparam>
public abstract class Specification<T>
    where T : class
{
    /// <summary>The predicate that a candidate must satisfy to match.</summary>
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return ToExpression().Compile()(candidate);
    }

    public Specification<T> And(Specification<T> other) => new AndSpecification<T>(this, other);

    public Specification<T> Or(Specification<T> other) => new OrSpecification<T>(this, other);

    public Specification<T> Not() => new NotSpecification<T>(this);
}

internal sealed class AndSpecification<T> : Specification<T>
    where T : class
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var left = _left.ToExpression();
        var right = _right.ToExpression();
        var parameter = Expression.Parameter(typeof(T), "x");

        var body = Expression.AndAlso(
            Expression.Invoke(left, parameter),
            Expression.Invoke(right, parameter));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

internal sealed class OrSpecification<T> : Specification<T>
    where T : class
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var left = _left.ToExpression();
        var right = _right.ToExpression();
        var parameter = Expression.Parameter(typeof(T), "x");

        var body = Expression.OrElse(
            Expression.Invoke(left, parameter),
            Expression.Invoke(right, parameter));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

internal sealed class NotSpecification<T> : Specification<T>
    where T : class
{
    private readonly Specification<T> _inner;

    public NotSpecification(Specification<T> inner)
    {
        _inner = inner;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var inner = _inner.ToExpression();
        var parameter = Expression.Parameter(typeof(T), "x");
        var body = Expression.Not(Expression.Invoke(inner, parameter));
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}
