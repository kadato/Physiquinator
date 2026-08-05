using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Serialization;
using System.Text.Json;

namespace Physiquinator.Core.Services;

public sealed class WorkoutPlanService(WorkoutPlanRepository repository)
{
    private readonly WorkoutPlanRepository _repository = repository;
    private static readonly JsonSerializerOptions s_jsonOptions = new(PhysiquinatorJsonContext.Default.Options) { WriteIndented = true };

    public Task<List<WorkoutPlan>> GetAllPlansAsync() => _repository.GetAllPlansAsync();

    public Task<WorkoutPlan?> GetPlanAsync(Guid id) => _repository.GetPlanAsync(id);

    public Task SavePlanAsync(WorkoutPlan plan) => _repository.SavePlanAsync(plan);

    public Task DeletePlanAsync(Guid id) => _repository.DeletePlanAsync(id);

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
        WorkoutPlan? plan = await GetPlanAsync(id);
        if (plan == null)
            throw new InvalidOperationException($"Plan with ID {id} not found.");

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
    /// If the plan ID already exists, it will be updated; otherwise, a new plan is created.
    /// </summary>
    public async Task<WorkoutPlan> ImportPlanFromJsonAsync(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        WorkoutPlan? plan = JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.WorkoutPlan);
        if (plan == null)
            throw new InvalidOperationException("Failed to deserialize workout plan from JSON.");

        await SavePlanAsync(plan);
        return plan;
    }

    /// <summary>
    /// Imports multiple workout plans from a JSON string.
    /// </summary>
    public async Task<List<WorkoutPlan>> ImportPlansFromJsonAsync(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        List<WorkoutPlan>? plans = JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.ListWorkoutPlan);
        if (plans == null)
            throw new InvalidOperationException("Failed to deserialize workout plans from JSON.");

        foreach (WorkoutPlan plan in plans)
        {
            await SavePlanAsync(plan);
        }
        return plans;
    }

    /// <summary>
    /// Saves a workout plan to a JSON file.
    /// </summary>
    public async Task ExportPlanToFileAsync(Guid id, string filePath)
    {
        var json = await ExportPlanToJsonAsync(id);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Saves all workout plans to a JSON file.
    /// </summary>
    public async Task ExportAllPlansToFileAsync(string filePath)
    {
        var json = await ExportAllPlansToJsonAsync();
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads a workout plan from a JSON file.
    /// </summary>
    public async Task<WorkoutPlan> ImportPlanFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return await ImportPlanFromJsonAsync(json);
    }

    /// <summary>
    /// Loads multiple workout plans from a JSON file.
    /// </summary>
    public async Task<List<WorkoutPlan>> ImportPlansFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return await ImportPlansFromJsonAsync(json);
    }
}
