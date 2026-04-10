using CatalogService.Common;
using CatalogService.Dtos;
using CatalogService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    /// <summary>
    /// Gets paginated list of products.
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 10)</param>
    /// <param name="search">Search in name, description, SKU</param>
    /// <param name="categoryId">Filter by category</param>
    /// <param name="minPrice">Minimum price filter</param>
    /// <param name="maxPrice">Maximum price filter</param>
    /// <param name="includeInactive">Include soft-deleted products</param>
    /// <param name="sort">
    /// Sorting options:
    /// newest | oldest | price_asc | price_desc | name_asc | name_desc | stock_asc | stock_desc
    /// </param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductResponseDto>>> GetAll(
        [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? search = null,
    [FromQuery] int? categoryId = null,
    [FromQuery] decimal? minPrice = null,
    [FromQuery] decimal? maxPrice = null,
    [FromQuery] bool includeInactive = false,
    [FromQuery] string sort = "newest")
    {
        var result = await _service.GetAllAsync(page, pageSize, search, categoryId, minPrice, maxPrice, includeInactive, sort);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponseDto>> GetById(
        [FromRoute] int id,
        [FromQuery] bool includeInactive = false)
    {
        var item = await _service.GetByIdAsync(id, includeInactive);
        return item is null
            ? NotFound(new { message = "Product not found." })
            : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductResponseDto>> Create([FromBody] ProductCreateDto dto)
    {
        var result = await _service.CreateAsync(dto);

        return result.Status switch
        {
            ResultStatus.Success =>
                CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data),

            ResultStatus.Conflict =>
                Conflict(new { message = result.Error }),

            ResultStatus.ValidationError =>
                BadRequest(new { message = result.Error }),

            ResultStatus.NotFound =>
                NotFound(new { message = result.Error }),

            _ => StatusCode(500, new { message = "Unexpected error." })
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductResponseDto>> Update(
        [FromRoute] int id,
        [FromBody] ProductUpdateDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);

        return result.Status switch
        {
            ResultStatus.Success =>
                Ok(result.Data),

            ResultStatus.NotFound =>
                NotFound(new { message = result.Error }),

            ResultStatus.Conflict =>
                Conflict(new { message = result.Error }),

            ResultStatus.ValidationError =>
                BadRequest(new { message = result.Error }),

            _ => StatusCode(500, new { message = "Unexpected error." })
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        var result = await _service.SoftDeleteAsync(id);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(new { message = result.Error }),
            _ => StatusCode(500, new { message = "Unexpected error." })
        };
    }
}