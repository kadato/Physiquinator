using Physiquinator.Data;
using Physiquinator.Models;
using System.Text.Json;

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

    /// <summary>
    /// Exports a workout plan to a JSON string.
    /// </summary>
    public async Task<string> ExportPlanToJsonAsync(Guid id)
    {
        var plan = await GetPlanAsync(id);
        if (plan == null)
            throw new InvalidOperationException($"Plan with ID {id} not found.");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        return JsonSerializer.Serialize(plan, options);
    }

    /// <summary>
    /// Exports all workout plans to a JSON string.
    /// </summary>
    public async Task<string> ExportAllPlansToJsonAsync()
    {
        var plans = await GetAllPlansAsync();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        return JsonSerializer.Serialize(plans, options);
    }

    /// <summary>
    /// Imports a workout plan from a JSON string.
    /// If the plan ID already exists, it will be updated; otherwise, a new plan is created.
    /// </summary>
    public async Task<WorkoutPlan> ImportPlanFromJsonAsync(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var plan = JsonSerializer.Deserialize<WorkoutPlan>(json);
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

        var plans = JsonSerializer.Deserialize<List<WorkoutPlan>>(json);
        if (plans == null)
            throw new InvalidOperationException("Failed to deserialize workout plans from JSON.");

        foreach (var plan in plans)
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
