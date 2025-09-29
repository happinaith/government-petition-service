using Microsoft.AspNetCore.Mvc;
using PetitionService.API.Models;
using PetitionService.API.Services;

namespace PetitionService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PetitionsController : ControllerBase
{
    private readonly IPetitionService _petitionService;
    private readonly ILogger<PetitionsController> _logger;

    public PetitionsController(IPetitionService petitionService, ILogger<PetitionsController> logger)
    {
        _petitionService = petitionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Petition>>> GetPetitions([FromQuery] PetitionFilter filter)
    {
        try
        {
            var petitions = await _petitionService.GetPetitionsAsync(filter);
            return Ok(petitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving petitions");
            return StatusCode(500, "An error occurred while retrieving petitions");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Petition>> GetPetition(int id)
    {
        try
        {
            var petition = await _petitionService.GetPetitionByIdAsync(id);
            if (petition == null)
            {
                return NotFound();
            }
            return Ok(petition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving petition {PetitionId}", id);
            return StatusCode(500, "An error occurred while retrieving the petition");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Petition>> CreatePetition(CreatePetitionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.Description))
            {
                return BadRequest("Title and Description are required");
            }

            var petition = await _petitionService.CreatePetitionAsync(request);
            return CreatedAtAction(nameof(GetPetition), new { id = petition.Id }, petition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating petition");
            return StatusCode(500, "An error occurred while creating the petition");
        }
    }

    [HttpPost("{id}/sign")]
    public async Task<ActionResult> SignPetition(int id)
    {
        try
        {
            var success = await _petitionService.SignPetitionAsync(id);
            if (!success)
            {
                return NotFound("Petition not found or cannot be signed");
            }
            return Ok(new { message = "Petition signed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing petition {PetitionId}", id);
            return StatusCode(500, "An error occurred while signing the petition");
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories()
    {
        try
        {
            var categories = await _petitionService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, "An error occurred while retrieving categories");
        }
    }

    [HttpGet("themes")]
    public async Task<ActionResult<IEnumerable<string>>> GetThemes()
    {
        try
        {
            var themes = await _petitionService.GetThemesAsync();
            return Ok(themes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving themes");
            return StatusCode(500, "An error occurred while retrieving themes");
        }
    }
}