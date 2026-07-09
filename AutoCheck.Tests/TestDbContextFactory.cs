using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Tests;

/// <summary>
/// Test double for <see cref="IDbContextFactory{TContext}"/>. Every context it hands
/// out is built from the SAME options instance, so they all share one in-memory store
/// (EF caches the internal provider per options instance) — exactly like the seeding
/// context in each test fixture.
/// </summary>
internal sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
    : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
}
