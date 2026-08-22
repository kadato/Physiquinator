using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using System.Text.Json.Serialization;

namespace Physiquinator.Core.Serialization;

/// <summary>
/// Source-generated JSON metadata for every POCO serialized in the app.
/// Replaces reflection-based serialization on hot paths (rest snapshots,
/// profile persistence, plan snapshots) with compile-time metadata.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RestTimerSnapshot))]
[JsonSerializable(typeof(UserProfile))]
[JsonSerializable(typeof(List<UserProfile>))]
[JsonSerializable(typeof(WorkoutPlan))]
[JsonSerializable(typeof(List<WorkoutPlan>))]
[JsonSerializable(typeof(WorkoutHistoryBackup))]
[JsonSerializable(typeof(AllDataBackup))]
[JsonSerializable(typeof(List<WorkoutScheduleHistoryEntity>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(AiChatMessage))]
[JsonSerializable(typeof(List<AiChatMessage>))]
[JsonSerializable(typeof(List<AiToolCallInfo>))]
[JsonSerializable(typeof(AiBridgePayloadDto))]
[JsonSerializable(typeof(List<AiBridgeActionDto>))]
[JsonSerializable(typeof(AiBridgeActionDto))]
public partial class PhysiquinatorJsonContext : JsonSerializerContext;

