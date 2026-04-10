using CatalogService.Common;
using CatalogService.Dtos;
using CatalogService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _service;

    public CategoriesController(ICategoryService service)
        => _service = service;

    // GET /api/Categories?includeInactive=false
    [HttpGet]
    public async Task<ActionResult<List<CategoryResponseDto>>> GetAll(
        [FromQuery] bool includeInactive = false)
    {
        var items = await _service.GetAllAsync(includeInactive);
        return Ok(items);
    }

    // GET /api/Categories/{id}?includeInactive=false
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryResponseDto>> GetById(
        [FromRoute] int id,
        [FromQuery] bool includeInactive = false)
    {
        var item = await _service.GetByIdAsync(id, includeInactive);
        return item is null
            ? NotFound(new { message = "Category not found." })
            : Ok(item);
    }

    // POST /api/Categories
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CategoryResponseDto>> Create([FromBody] CategoryCreateDto dto)
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

            // NotFound shouldn't happen on Create but we still handle it:
            ResultStatus.NotFound =>
                NotFound(new { message = result.Error }),

            _ => StatusCode(500, new { message = "Unexpected error." })
        };
    }

    // PUT /api/Categories/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CategoryResponseDto>> Update(
        [FromRoute] int id,
        [FromBody] CategoryUpdateDto dto)
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

    // DELETE /api/Categories/{id} (soft delete)
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        var result = await _service.SoftDeleteAsync(id);

        return result.Status switch
        {
            ResultStatus.Success =>
                NoContent(),

            ResultStatus.NotFound =>
                NotFound(new { message = result.Error }),

            _ => StatusCode(500, new { message = "Unexpected error." })
        };
    }
}