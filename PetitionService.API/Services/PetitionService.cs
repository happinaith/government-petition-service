using PetitionService.API.Models;

namespace PetitionService.API.Services;

public interface IPetitionService
{
    Task<IEnumerable<Petition>> GetPetitionsAsync(PetitionFilter filter);
    Task<Petition?> GetPetitionByIdAsync(int id);
    Task<Petition> CreatePetitionAsync(CreatePetitionRequest request);
    Task<bool> SignPetitionAsync(int id);
    Task<IEnumerable<string>> GetCategoriesAsync();
    Task<IEnumerable<string>> GetThemesAsync();
}

public class InMemoryPetitionService : IPetitionService
{
    private readonly List<Petition> _petitions;
    private int _nextId = 1;

    public InMemoryPetitionService()
    {
        _petitions = GenerateSamplePetitions();
    }

    public async Task<IEnumerable<Petition>> GetPetitionsAsync(PetitionFilter filter)
    {
        await Task.Delay(50); // Simulate async operation

        var query = _petitions.AsEnumerable();

        if (!string.IsNullOrEmpty(filter.Category))
            query = query.Where(p => p.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(filter.Theme))
            query = query.Where(p => p.Theme.Equals(filter.Theme, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(filter.Status))
            query = query.Where(p => p.Status.Equals(filter.Status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(filter.TargetGovernmentLevel))
            query = query.Where(p => p.TargetGovernmentLevel.Equals(filter.TargetGovernmentLevel, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(filter.SearchQuery))
            query = query.Where(p => p.Title.Contains(filter.SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                   p.Description.Contains(filter.SearchQuery, StringComparison.OrdinalIgnoreCase));

        // Simple pagination
        query = query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize);

        return query.OrderByDescending(p => p.CreatedDate).ToList();
    }

    public async Task<Petition?> GetPetitionByIdAsync(int id)
    {
        await Task.Delay(25); // Simulate async operation
        return _petitions.FirstOrDefault(p => p.Id == id);
    }

    public async Task<Petition> CreatePetitionAsync(CreatePetitionRequest request)
    {
        await Task.Delay(50); // Simulate async operation

        var petition = new Petition
        {
            Id = _nextId++,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Theme = request.Theme,
            CreatedBy = request.CreatedBy,
            TargetGovernmentLevel = request.TargetGovernmentLevel,
            CreatedDate = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            SignatureCount = 1, // Creator signs it first
            Status = "Active"
        };

        _petitions.Add(petition);
        return petition;
    }

    public async Task<bool> SignPetitionAsync(int id)
    {
        await Task.Delay(25); // Simulate async operation

        var petition = _petitions.FirstOrDefault(p => p.Id == id);
        if (petition != null && petition.Status == "Active")
        {
            petition.SignatureCount++;
            petition.LastUpdated = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync()
    {
        await Task.Delay(25); // Simulate async operation
        return _petitions.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();
    }

    public async Task<IEnumerable<string>> GetThemesAsync()
    {
        await Task.Delay(25); // Simulate async operation
        return _petitions.Select(p => p.Theme).Distinct().OrderBy(t => t).ToList();
    }

    private List<Petition> GenerateSamplePetitions()
    {
        var petitions = new List<Petition>
        {
            new Petition
            {
                Id = _nextId++,
                Title = "Improve Public Transportation Infrastructure",
                Description = "We petition for significant investment in modernizing our city's public transportation system, including new bus routes, light rail expansion, and improved accessibility features.",
                Category = "Transportation",
                Theme = "Infrastructure",
                CreatedBy = "Jane Smith",
                TargetGovernmentLevel = "Local",
                CreatedDate = DateTime.UtcNow.AddDays(-15),
                LastUpdated = DateTime.UtcNow.AddDays(-2),
                SignatureCount = 1247,
                Status = "Active"
            },
            new Petition
            {
                Id = _nextId++,
                Title = "Increase Funding for Public Schools",
                Description = "Our schools need more resources. This petition calls for increased federal funding to reduce class sizes, update textbooks, and improve technology in classrooms nationwide.",
                Category = "Education",
                Theme = "Funding",
                CreatedBy = "Michael Johnson",
                TargetGovernmentLevel = "Federal",
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                LastUpdated = DateTime.UtcNow.AddDays(-1),
                SignatureCount = 5683,
                Status = "Active"
            },
            new Petition
            {
                Id = _nextId++,
                Title = "Expand Community Healthcare Services",
                Description = "We need better access to healthcare in our community. This petition requests the establishment of more community health centers and extended clinic hours.",
                Category = "Healthcare",
                Theme = "Access",
                CreatedBy = "Dr. Sarah Wilson",
                TargetGovernmentLevel = "State",
                CreatedDate = DateTime.UtcNow.AddDays(-7),
                LastUpdated = DateTime.UtcNow.AddHours(-6),
                SignatureCount = 892,
                Status = "Active"
            },
            new Petition
            {
                Id = _nextId++,
                Title = "Create More Green Spaces and Parks",
                Description = "Our neighborhoods need more parks and green spaces for recreation and environmental benefits. This petition calls for the development of at least 10 new parks in underserved areas.",
                Category = "Environment",
                Theme = "Recreation",
                CreatedBy = "Green Community Coalition",
                TargetGovernmentLevel = "Local",
                CreatedDate = DateTime.UtcNow.AddDays(-45),
                LastUpdated = DateTime.UtcNow.AddDays(-3),
                SignatureCount = 2156,
                Status = "Under Review"
            },
            new Petition
            {
                Id = _nextId++,
                Title = "Implement Renewable Energy Initiative",
                Description = "We petition for a comprehensive renewable energy program that includes solar panel installations on public buildings and incentives for residential renewable energy adoption.",
                Category = "Environment",
                Theme = "Energy",
                CreatedBy = "Climate Action Network",
                TargetGovernmentLevel = "State",
                CreatedDate = DateTime.UtcNow.AddDays(-60),
                LastUpdated = DateTime.UtcNow.AddDays(-10),
                SignatureCount = 7234,
                Status = "Active"
            }
        };

        return petitions;
    }
}