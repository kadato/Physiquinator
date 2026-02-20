using Physiquinator.Data;
using Physiquinator.Models;

namespace Physiquinator.Services;

public class WorkoutPlanService
{
    private readonly WorkoutPlanRepository _repository;

    public WorkoutPlanService(WorkoutPlanRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<WorkoutPlan>> GetAllPlansAsync() => await _repository.GetAllPlansAsync();

    public async Task<WorkoutPlan?> GetPlanAsync(Guid id) => await _repository.GetPlanAsync(id);

    public async Task SavePlanAsync(WorkoutPlan plan) => await _repository.SavePlanAsync(plan);

    public async Task DeletePlanAsync(Guid id) => await _repository.DeletePlanAsync(id);

    public List<WorkoutPlan> GetAllPlans()
    {
        return GetAllPlansAsync().GetAwaiter().GetResult();
    }

    public WorkoutPlan? GetPlan(Guid id)
    {
        return GetPlanAsync(id).GetAwaiter().GetResult();
    }

    public void SavePlan(WorkoutPlan plan)
    {
        SavePlanAsync(plan).GetAwaiter().GetResult();
    }

    public void DeletePlan(Guid id)
    {
        DeletePlanAsync(id).GetAwaiter().GetResult();
    }
}
