using FluentAssertions;
using NetArchTest.Rules;
using PSP.TopupService.Application;
using PSP.TopupService.Persistence.Context;

namespace PSP.TopupService.ArchitectureTests;

/// <summary>
/// Structural rules that complement the dependency rules: repositories live
/// only in Persistence, controllers stay thin, and the Domain/Application
/// layers stay free of EF Core / MediatR.
/// </summary>
[Trait("Category", "Architecture")]
public static class StructuralRuleTests
{
    [Fact]
    public static void Repository_Implementations_Should_Live_Only_In_Persistence()
    {
        // Any *Repository type that lives outside Persistence is a violation.
        var allAssemblies = new[]
        {
            typeof(PSP.TopupService.Domain.Topups.TopupTransaction).Assembly,
            typeof(PSP.TopupService.Infrastructure.DependencyInjection).Assembly,
            typeof(TopupDbContext).Assembly,
        };

        var inPersistence = Types.InAssembly(typeof(TopupDbContext).Assembly)
            .That().HaveNameEndingWith("Repository")
            .GetTypes();

        var everywhere = Types.InAssemblies(allAssemblies)
            .That().HaveNameEndingWith("Repository")
            .GetTypes();

        var offenders = everywhere.Where(t => !inPersistence.Contains(t)).ToList();
        offenders.Should().BeEmpty("Repository implementations must live in the Persistence namespace");
    }

    [Fact]
    public static void Controllers_Should_Not_Reference_Persistence()
    {
        var offenders = Types.InAssembly(System.Reflection.Assembly.Load("PSP.TopupService.Api"))
            .That().HaveNameEndingWith("Controller")
            .And().HaveDependencyOn("PSP.TopupService.Persistence")
            .GetTypes();

        offenders.Should().BeEmpty("Controllers must not reference Persistence (use MediatR)");
    }

    [Fact]
    public static void Domain_Should_Not_Use_EntityFrameworkCore()
    {
        var offenders = Types.InAssembly(typeof(PSP.TopupService.Domain.Topups.TopupTransaction).Assembly)
            .That().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetTypes();

        offenders.Should().BeEmpty("Domain must be EF-Core-free");
    }

    [Fact]
    public static void Domain_Should_Not_Use_MediatR()
    {
        var offenders = Types.InAssembly(typeof(PSP.TopupService.Domain.Topups.TopupTransaction).Assembly)
            .That().HaveDependencyOn("MediatR")
            .GetTypes();

        offenders.Should().BeEmpty("Domain must be MediatR-free");
    }

    [Fact]
    public static void Application_Should_Not_Use_EntityFrameworkCore()
    {
        var offenders = Types.InAssembly(typeof(DependencyInjection).Assembly)
            .That().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetTypes();

        offenders.Should().BeEmpty("Application must be EF-Core-free");
    }

    [Fact]
    public static void All_Layer_Assemblies_Should_Load()
    {
        // Sanity check that the per-layer dependency rules have something to run on.
        var allAssemblies = new[]
        {
            typeof(PSP.TopupService.SharedKernel.Results.Result).Assembly,
            typeof(PSP.TopupService.Domain.Topups.TopupTransaction).Assembly,
            typeof(PSP.TopupService.Contracts.IIntegrationEvent).Assembly,
            typeof(DependencyInjection).Assembly,
            typeof(PSP.TopupService.Infrastructure.DependencyInjection).Assembly,
            typeof(TopupDbContext).Assembly,
            typeof(PSP.TopupService.Worker.Options.OutboxPublisherOptions).Assembly,
        };

        foreach (var assembly in allAssemblies)
        {
            Types.InAssembly(assembly).GetTypes().Should().NotBeNull($"{assembly.GetName().Name} must load");
        }
    }
}
