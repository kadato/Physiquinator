using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Serialization;
using System.Text.Json;

namespace Physiquinator.Core.Services;

public sealed class WorkoutPlanService(WorkoutPlanRepository repository)
{
    private readonly WorkoutPlanRepository _repository = repository;
    private static readonly JsonSerializerOptions s_jsonOptions = new(PhysiquinatorJsonContext.Default.Options) { WriteIndented = true };
    private List<WorkoutPlan>? _plansCache;

    public async Task<List<WorkoutPlan>> GetAllPlansAsync()
    {
        if (_plansCache is { } cached)
            return cached;

        var plans = await _repository.GetAllPlansAsync();
        _plansCache = plans;
        return plans;
    }

    public void InvalidatePlanCache() => _plansCache = null;

    public Task<WorkoutPlan?> GetPlanAsync(Guid id) => _repository.GetPlanAsync(id);

    public async Task SavePlanAsync(WorkoutPlan plan)
    {
        await _repository.SavePlanAsync(plan);
        InvalidatePlanCache();
    }

    public async Task DeletePlanAsync(Guid id)
    {
        await _repository.DeletePlanAsync(id);
        InvalidatePlanCache();
    }

    /// <summary>
    /// Persists a new relative ordering for the given plan IDs (position 0 = first).
    /// </summary>
    public async Task ReorderPlansAsync(IReadOnlyList<Guid> orderedPlanIds)
    {
        ArgumentNullException.ThrowIfNull(orderedPlanIds);

        var order = new (Guid Id, int SortOrder)[orderedPlanIds.Count];
        for (var i = 0; i < orderedPlanIds.Count; i++)
            order[i] = (orderedPlanIds[i], i);

        await _repository.ReorderPlansAsync(order);
        InvalidatePlanCache();
    }

    /// <summary>
    /// Exports a workout plan to a JSON string.
    /// </summary>
    public static string SerializePlanToJson(WorkoutPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return JsonSerializer.Serialize(plan, s_jsonOptions);
    }

    public async Task<string> ExportPlanToJsonAsync(Guid id)
    {
        WorkoutPlan? plan = await GetPlanAsync(id) ?? throw new InvalidOperationException($"Plan with ID {id} not found.");
        return SerializePlanToJson(plan);
    }

    /// <summary>
    /// Exports all workout plans to a JSON string.
    /// </summary>
    public async Task<string> ExportAllPlansToJsonAsync()
    {
        List<WorkoutPlan> plans = await GetAllPlansAsync();
        return JsonSerializer.Serialize(plans, s_jsonOptions);
    }

    /// <summary>
    /// Imports a workout plan from a JSON string.
    /// If the plan ID already exists, it will be updated. Otherwise, a new plan is created.
    /// </summary>
    public async Task<WorkoutPlan> ImportPlanFromJsonAsync(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        WorkoutPlan? plan = JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.WorkoutPlan) ?? throw new InvalidOperationException("Failed to deserialize workout plan from JSON.");
        await _repository.SavePlanAsync(plan);
        InvalidatePlanCache();
        return plan;
    }

    /// <summary>
    /// Imports multiple workout plans from a JSON string.
    /// </summary>
    public async Task<List<WorkoutPlan>> ImportPlansFromJsonAsync(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        List<WorkoutPlan>? plans = JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.ListWorkoutPlan) ?? throw new InvalidOperationException("Failed to deserialize workout plans from JSON.");
        foreach (WorkoutPlan plan in plans)
        {
            await _repository.SavePlanAsync(plan);
        }
        InvalidatePlanCache();
        return plans;
    }

    /// <summary>
    /// Counts how many plans a JSON import file contains, split into new
    /// plans (unknown IDs) and overwrites (IDs already present), without
    /// touching the database.
    /// </summary>
    public async Task<PlanImportPreview> PreviewPlansImportAsync(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var plans = new List<WorkoutPlan>();
        try
        {
            List<WorkoutPlan>? list = JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.ListWorkoutPlan);
            if (list != null)
                plans.AddRange(list);
        }
        catch (JsonException)
        {
            WorkoutPlan? single = JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.WorkoutPlan);
            if (single != null)
                plans.Add(single);
        }

        if (plans.Count == 0)
            throw new InvalidOperationException("No workout plans found in the selected file.");

        var existing = await GetAllPlansAsync();
        var existingIds = existing.Select(p => p.Id).ToHashSet();
        var newCount = plans.Count(p => !existingIds.Contains(p.Id));

        return new PlanImportPreview(plans.Count, newCount, plans.Count - newCount);
    }
}

/// <summary>What a plan import would do: totals split into new vs overwritten plans.</summary>
public sealed record PlanImportPreview(int Total, int New, int Overwritten);
