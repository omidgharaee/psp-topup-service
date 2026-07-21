using FluentAssertions;
using NetArchTest.Rules;
using PSP.TopupService.Application;
using PSP.TopupService.Persistence.Context;
using PSP.TopupService.Worker.Options;

namespace PSP.TopupService.ArchitectureTests;

/// <summary>
/// Enforces the Clean Architecture dependency rule: dependencies may only
/// point inwards (toward the domain). Each layer is verified to depend only on
/// the layers it is allowed to. A violation fails the build.
///
/// The pattern uses NetArchTest's predicate (That/Should) plus a manual
/// <c>GetTypes().Should().BeEmpty()</c> assertion because it produces the
/// clearest failure message (the offending type names).
/// </summary>
[Trait("Category", "Architecture")]
public static class DependencyRuleTests
{
    private const string SharedKernel = "SharedKernel";
    private const string Domain = "Domain";
    private const string Contracts = "Contracts";
    private const string Application = "Application";
    private const string Infrastructure = "Infrastructure";
    private const string Persistence = "Persistence";
    private const string Api = "Api";
    private const string Worker = "Worker";

    [Fact]
    public static void SharedKernel_Should_Not_Reference_Other_Projects()
    {
        var offenders = Types.InAssembly(typeof(PSP.TopupService.SharedKernel.Results.Result).Assembly)
            .That().HaveDependencyOnAny(Domain, Contracts, Application, Infrastructure, Persistence, Api, Worker)
            .GetTypes();

        offenders.Should().BeEmpty("SharedKernel must be dependency-free");
    }

    [Fact]
    public static void Domain_Should_Reference_Only_SharedKernel()
    {
        var offenders = Types.InAssembly(typeof(PSP.TopupService.Domain.Topups.TopupTransaction).Assembly)
            .That().HaveDependencyOnAny(Contracts, Application, Infrastructure, Persistence, Api, Worker)
            .GetTypes();

        offenders.Should().BeEmpty("Domain may only depend on SharedKernel");
    }

    [Fact]
    public static void Contracts_Should_Not_Reference_Internal_Projects()
    {
        var offenders = Types.InAssembly(typeof(PSP.TopupService.Contracts.IIntegrationEvent).Assembly)
            .That().HaveDependencyOnAny(SharedKernel, Domain, Application, Infrastructure, Persistence, Api, Worker)
            .GetTypes();

        offenders.Should().BeEmpty("Contracts must be self-contained");
    }

    [Fact]
    public static void Application_Should_Reference_Only_Domain_And_SharedKernel()
    {
        var offenders = Types.InAssembly(typeof(DependencyInjection).Assembly)
            .That().HaveDependencyOnAny(Contracts, Infrastructure, Persistence, Api, Worker)
            .GetTypes();

        offenders.Should().BeEmpty("Application may only depend on Domain and SharedKernel");
    }

    [Fact]
    public static void Infrastructure_Should_Not_Reference_Persistence_Api_Or_Worker()
    {
        var offenders = Types.InAssembly(typeof(PSP.TopupService.Infrastructure.DependencyInjection).Assembly)
            .That().HaveDependencyOnAny(Persistence, Api, Worker)
            .GetTypes();

        offenders.Should().BeEmpty("Infrastructure may not depend on Persistence, Api or Worker");
    }

    [Fact]
    public static void Persistence_Should_Not_Reference_Infrastructure_Api_Or_Worker()
    {
        var offenders = Types.InAssembly(typeof(TopupDbContext).Assembly)
            .That().HaveDependencyOnAny(Contracts, Infrastructure, Api, Worker)
            .GetTypes();

        offenders.Should().BeEmpty("Persistence may not depend on Infrastructure, Api or Worker");
    }

    [Fact]
    public static void Api_Should_Not_Reference_Worker()
    {
        var offenders = Types.InAssembly(System.Reflection.Assembly.Load("PSP.TopupService.Api"))
            .That().HaveDependencyOn(Worker)
            .GetTypes();

        offenders.Should().BeEmpty("Api must not reference Worker");
    }

    [Fact]
    public static void Worker_Should_Not_Reference_Api()
    {
        var offenders = Types.InAssembly(typeof(OutboxPublisherOptions).Assembly)
            .That().HaveDependencyOn(Api)
            .GetTypes();

        offenders.Should().BeEmpty("Worker must not reference Api");
    }
}
