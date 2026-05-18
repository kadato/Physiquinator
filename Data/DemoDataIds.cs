namespace Physiquinator.Data;

/// <summary>Stable identifiers for first-run demo plans, exercises, sessions, and set rows.</summary>
public static class DemoDataIds
{
    public static readonly Guid PushPlan = Guid.Parse("dead0000-0000-4000-8000-000000000001");
    public static readonly Guid PullPlan = Guid.Parse("dead0000-0000-4000-8000-000000000002");
    public static readonly Guid LegPlan = Guid.Parse("dead0000-0000-4000-8000-000000000003");
    public static readonly Guid FullBodyPlan = Guid.Parse("dead0000-0000-4000-8000-000000000004");

    // Push exercises (order 0..5)
    public static readonly Guid PushBench = Guid.Parse("dead0000-0000-4000-8000-000000001001");
    public static readonly Guid PushOhp = Guid.Parse("dead0000-0000-4000-8000-000000001002");
    public static readonly Guid PushIncline = Guid.Parse("dead0000-0000-4000-8000-000000001003");
    public static readonly Guid PushLateral = Guid.Parse("dead0000-0000-4000-8000-000000001004");
    public static readonly Guid PushTriPush = Guid.Parse("dead0000-0000-4000-8000-000000001005");
    public static readonly Guid PushTriOver = Guid.Parse("dead0000-0000-4000-8000-000000001006");

    // Pull exercises
    public static readonly Guid PullDeadlift = Guid.Parse("dead0000-0000-4000-8000-000000002001");
    public static readonly Guid PullPullups = Guid.Parse("dead0000-0000-4000-8000-000000002002");
    public static readonly Guid PullRow = Guid.Parse("dead0000-0000-4000-8000-000000002003");
    public static readonly Guid PullFace = Guid.Parse("dead0000-0000-4000-8000-000000002004");
    public static readonly Guid PullCurl = Guid.Parse("dead0000-0000-4000-8000-000000002005");
    public static readonly Guid PullHammer = Guid.Parse("dead0000-0000-4000-8000-000000002006");

    // Leg exercises
    public static readonly Guid LegSquat = Guid.Parse("dead0000-0000-4000-8000-000000003001");
    public static readonly Guid LegRdl = Guid.Parse("dead0000-0000-4000-8000-000000003002");
    public static readonly Guid LegPress = Guid.Parse("dead0000-0000-4000-8000-000000003003");
    public static readonly Guid LegCurl = Guid.Parse("dead0000-0000-4000-8000-000000003004");
    public static readonly Guid LegCalf = Guid.Parse("dead0000-0000-4000-8000-000000003005");
    public static readonly Guid LegExt = Guid.Parse("dead0000-0000-4000-8000-000000003006");

    // Full-body exercises
    public static readonly Guid FbSquat = Guid.Parse("dead0000-0000-4000-8000-000000004001");
    public static readonly Guid FbBench = Guid.Parse("dead0000-0000-4000-8000-000000004002");
    public static readonly Guid FbRow = Guid.Parse("dead0000-0000-4000-8000-000000004003");
    public static readonly Guid FbOhp = Guid.Parse("dead0000-0000-4000-8000-000000004004");
    public static readonly Guid FbPullup = Guid.Parse("dead0000-0000-4000-8000-000000004005");
    public static readonly Guid FbPlank = Guid.Parse("dead0000-0000-4000-8000-000000004006");

    /// <summary>Deterministic session primary key (string for <see cref="WorkoutSessionLogEntity"/>).</summary>
    public static string SessionId(int sessionIndex)
    {
        Span<byte> b = stackalloc byte[16];
        b[0] = 0xde;
        b[1] = 0xad;
        b[2] = 0xbe;
        b[3] = 0xef;
        BitConverter.TryWriteBytes(b[8..], sessionIndex);
        return new Guid(b).ToString();
    }

    /// <summary>Deterministic set row primary key.</summary>
    public static string SetId(int sessionIndex, int exerciseIndex, int setIndex)
    {
        Span<byte> b = stackalloc byte[16];
        b[0] = 0xde;
        b[1] = 0xca;
        b[2] = 0xfe;
        b[3] = 0xba;
        BitConverter.TryWriteBytes(b[4..], sessionIndex);
        BitConverter.TryWriteBytes(b[8..], exerciseIndex);
        BitConverter.TryWriteBytes(b[12..], setIndex);
        return new Guid(b).ToString();
    }
}
