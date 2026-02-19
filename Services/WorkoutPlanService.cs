using Physiquinator.Models;

namespace Physiquinator.Services;

public class WorkoutPlanService
{
    private readonly List<WorkoutPlan> _plans = new();

    public IReadOnlyList<WorkoutPlan> GetAllPlans() => _plans.AsReadOnly();

    public WorkoutPlan? GetPlan(Guid id) => _plans.FirstOrDefault(p => p.Id == id);

    public void SavePlan(WorkoutPlan plan)
    {
        var existing = _plans.FirstOrDefault(p => p.Id == plan.Id);
        if (existing != null)
        {
            _plans.Remove(existing);
        }
        _plans.Add(plan);
    }
}
