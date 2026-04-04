using CatalogService.Common;
using CatalogService.Data;
using CatalogService.Dtos;
using CatalogService.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Tests;

public class CategoryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CatalogDbContext _db;
    private readonly CategoryService _sut;

    public CategoryServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var opts = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new CatalogDbContext(opts);
        _db.Database.EnsureCreated();

        _sut = new CategoryService(_db);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_HappyPath_ReturnsSuccessAndPersists()
    {
        var result = await _sut.CreateAsync(new CategoryCreateDto { Name = "Electronics", Slug = "electronics" });

        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal("Electronics", result.Data!.Name);
        Assert.Equal("electronics", result.Data.Slug);
        Assert.True(result.Data.IsActive);
        Assert.Equal(1, await _db.Categories.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_DuplicateSlug_ReturnsConflict()
    {
        await _sut.CreateAsync(new CategoryCreateDto { Name = "Electronics", Slug = "electronics" });

        var result = await _sut.CreateAsync(new CategoryCreateDto { Name = "Electronics 2", Slug = "electronics" });

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task CreateAsync_UppercaseSlug_IsNormalizedToLowercase()
    {
        var result = await _sut.CreateAsync(new CategoryCreateDto { Name = "Electronics", Slug = "ELECTRONICS" });

        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal("electronics", result.Data!.Slug);
    }

    [Fact]
    public async Task CreateAsync_NameAndSlugWithWhitespace_AreTrimmed()
    {
        var result = await _sut.CreateAsync(new CategoryCreateDto { Name = "  Electronics  ", Slug = "  electronics  " });

        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal("Electronics", result.Data!.Name);
        Assert.Equal("electronics", result.Data.Slug);
    }

    [Fact]
    public async Task CreateAsync_NormalizedSlugMatchesExisting_ReturnsConflict()
    {
        await _sut.CreateAsync(new CategoryCreateDto { Name = "Electronics", Slug = "electronics" });

        var result = await _sut.CreateAsync(new CategoryCreateDto { Name = "Electronics 2", Slug = "  ELECTRONICS  " });

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
