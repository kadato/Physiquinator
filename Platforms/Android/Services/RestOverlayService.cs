using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using AndroidButton = Android.Widget.ImageButton;
using AndroidColor = Android.Graphics.Color;
using AndroidLinearLayout = Android.Widget.LinearLayout;
using AndroidTextButton = Android.Widget.Button;
using AndroidTextView = Android.Widget.TextView;
using AndroidView = Android.Views.View;

namespace Physiquinator.Platforms.Android.Services;

/// <summary>
/// Foreground service that hosts the floating workout bubble: a wide draggable
/// overlay showing the live rest countdown (or the upcoming set between
/// rests) with themed actions - add time, reset, skip - plus a close
/// button. Tapping the bubble body opens the app. The state is
/// read from <see cref="WorkoutSessionService"/> on a one-second ticker that
/// runs only while the bubble is visible, so it stays accurate regardless of
/// WebView suspension without waking the CPU for a workout held in the
/// foreground. The overlay view is only
/// shown while the app is backgrounded. The foreground service notification
/// stays up for the whole workout. Declared in AndroidManifest.xml
/// (foregroundServiceType specialUse). <see cref="RegisterAttribute"/> pins
/// the Java class name so the manifest entry resolves.
/// </summary>
[Register("physiquinator.RestOverlayService")]
public sealed class RestOverlayService : Service
{
    public const string ExtraEndUtcTicks = "endUtcTicks";
    public const string ExtraRemainingSeconds = "remainingSeconds";
    public const string ExtraTitle = "title";
    public const string ExtraNextExerciseName = "nextExerciseName";
    public const string ExtraNextExerciseIndex = "nextExerciseIndex";
    public const string ExtraNextSetIndex = "nextSetIndex";
    public const string ExtraNextSetTotal = "nextSetTotal";

    private const long TickerIntervalMs = 1000;

    /// <summary>MainActivity pings the service on app lifecycle changes so the
    /// bubble can be shown/hidden without a polling ticker running in the
    /// foreground.</summary>
    public const string ActionForegrounded = "physiquinator.overlay.foregrounded";
    public const string ActionBackgrounded = "physiquinator.overlay.backgrounded";

    private IWindowManager? _windowManager;
    private AndroidView? _overlayView;
    private AndroidTextView? _headerText;
    private AndroidTextView? _timerText;
    private AndroidTextView? _setInfoText;
    private AndroidTextView? _weightValue;
    private AndroidTextView? _repsValue;
    private AndroidView? _stepperRow;
    private AndroidButton? _logSetButton;
    private AndroidTextButton? _addTimeButton;
    private AndroidButton? _resetButton;
    private AndroidButton? _skipButton;
    private WindowManagerLayoutParams? _layoutParams;
    private Handler? _handler;
    private System.Action? _tickAction;
    private bool _tickerRunning;
    private bool _dismissed;
    private bool _wasForeground = true;
    private bool _stopping;

    // Current weight/reps being edited in the overlay
    private double _currentWeightKg;
    private int _currentReps;
    private int _trackedExerciseIndex = -1;
    private ExerciseLogType _currentLogType = ExerciseLogType.WeightAndReps;

    // Theme color palettes matching MainLayout.razor MudBlazor definitions
    private static readonly AndroidColor DarkBackground = AndroidColor.ParseColor("#0B0C10");
    private static readonly AndroidColor DarkSurface = AndroidColor.ParseColor("#151821");
    private static readonly AndroidColor DarkTextPrimary = AndroidColor.ParseColor("#F3F4F6");
    private static readonly AndroidColor DarkTextSecondary = AndroidColor.ParseColor("#9CA3AF");
    private static readonly AndroidColor DarkPrimary = AndroidColor.ParseColor("#10B981");
    private static readonly AndroidColor DarkWarning = AndroidColor.ParseColor("#F59E0B");
    private static readonly AndroidColor DarkError = AndroidColor.ParseColor("#EF4444");

    private static readonly AndroidColor LightBackground = AndroidColor.ParseColor("#F8F9FA");
    private static readonly AndroidColor LightSurface = AndroidColor.ParseColor("#FFFFFF");
    private static readonly AndroidColor LightTextPrimary = AndroidColor.ParseColor("#111827");
    private static readonly AndroidColor LightTextSecondary = AndroidColor.ParseColor("#6B7280");
    private static readonly AndroidColor LightPrimary = AndroidColor.ParseColor("#0F766E");
    private static readonly AndroidColor LightWarning = AndroidColor.ParseColor("#F59E0B");
    private static readonly AndroidColor LightError = AndroidColor.ParseColor("#EF4444");

    private Typeface? _outfitFont;

    public override void OnCreate()
    {
        base.OnCreate();

        try { _outfitFont = Typeface.CreateFromAsset(Assets, "fonts/outfit-latin.woff2"); }
        catch { _outfitFont = Typeface.Default; }

        _handler = new Handler(Looper.MainLooper!);
        System.Action tick = null!;
        tick = () =>
        {
            try
            {
                UpdateTicker();
            }
            catch (Exception ex)
            {
                // A single bad tick must not kill the reschedule, or the
                // bubble countdown freezes for the rest of the workout.
                System.Diagnostics.Debug.WriteLine($"RestOverlayService ticker failed: {ex}");
            }
            // Only reschedule while the ticker is still wanted. StopTicker
            // must win even when it runs mid-tick.
            if (_tickerRunning)
                _handler?.PostDelayed(tick, TickerIntervalMs);
        };
        _tickAction = tick;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Foreground/background lifecycle pings from MainActivity: show or
        // hide the bubble without rebuilding the notification. The ticker
        // runs only while the overlay is actually visible, so a workout held
        // in the foreground costs no wakeups.
        if (intent?.Action is ActionForegrounded or ActionBackgrounded)
        {
            UpdateOverlayVisibility();
            return StartCommandResult.NotSticky;
        }

        WorkoutTimerState state = intent != null ? ReadState(intent) : ReadSessionState();

        Notification notification = AndroidRestNotificationService.BuildWorkoutNotification(this, state, ResolveSettings()?.AddTimeSeconds ?? RestAlertSettingsService.DefaultAddTimeSeconds);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake)
            StartForeground(AndroidRestNotificationService.OngoingRestNotificationId, notification, ForegroundService.TypeSpecialUse);
        else
            StartForeground(AndroidRestNotificationService.OngoingRestNotificationId, notification);

        UpdateOverlayVisibility();

        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        _stopping = true;
        StopTicker();
        _handler = null;

        RemoveOverlayView();

        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    /// <summary>
    /// Starts the one-second ticker. Called only when the overlay view is
    /// visible (app backgrounded and bubble not dismissed), so a workout in
    /// the foreground does not wake the CPU for the whole session.
    /// </summary>
    private void StartTicker()
    {
        if (_tickerRunning || _stopping)
            return;

        _tickerRunning = true;
        _handler?.Post(_tickAction!);
    }

    /// <summary>Cancels any pending and future ticks (also wins mid-tick).</summary>
    private void StopTicker()
    {
        if (!_tickerRunning)
            return;

        _tickerRunning = false;
        _handler?.RemoveCallbacks(_tickAction!);
    }

    /// <summary>Shows the overlay only while the app is in the background and not dismissed by the user.</summary>
    private void UpdateOverlayVisibility()
    {
        if (_stopping)
            return;

        var foreground = MainActivity.IsInForeground;
        if (foreground)
        {
            // Reopening the app re-arms the bubble for the next backgrounding.
            if (!_wasForeground)
                _dismissed = false;

            _wasForeground = true;
            RemoveOverlayView();
        }
        else
        {
            _wasForeground = false;
            if (_dismissed || _overlayView != null)
                return;

            if (Settings.CanDrawOverlays(this))
                ShowOverlay();
        }
    }

    private void RemoveOverlayView()
    {
        StopTicker();

        if (_overlayView == null)
            return;

        try
        {
            _windowManager?.RemoveView(_overlayView);
        }
        catch (Exception)
        {
            // View may already be detached
        }

        _overlayView = null;
    }

    private Typeface OutfitFont() => _outfitFont ?? Typeface.Default!;

    private void ShowOverlay()
    {
        if (_overlayView != null)
            return;

        try
        {
            _windowManager = GetSystemService(Context.WindowService)?.JavaCast<IWindowManager>();
            var colors = GetThemeColors();
            var addSeconds = ResolveSettings()?.AddTimeSeconds ?? RestAlertSettingsService.DefaultAddTimeSeconds;

            var root = new AndroidLinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            var bg = new GradientDrawable();
            bg.SetColor(AndroidColor.Argb(0xF2, colors.Background.R, colors.Background.G, colors.Background.B));
            bg.SetCornerRadius(Dp(20));
            bg.SetStroke(Dp(2), AndroidColor.Argb(0x40, colors.Primary.R, colors.Primary.G, colors.Primary.B));
            root.Background = bg;
            root.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));

            // Row 1: exercise name left, timer right, close overlaid top-right
            var headerFrame = new FrameLayout(this);

            var headerRow = new AndroidLinearLayout(this)
            {
                Orientation = Orientation.Horizontal
            };
            headerRow.SetGravity(GravityFlags.CenterVertical);
            headerRow.SetPadding(0, 0, Dp(44), 0);

            _headerText = new AndroidTextView(this) { Text = string.Empty, Gravity = GravityFlags.Start | GravityFlags.CenterVertical };
            _headerText.SetMaxLines(1);
            _headerText.SetTextColor(colors.TextPrimary);
            _headerText.SetTextSize(ComplexUnitType.Sp, 15);
            _headerText.SetTypeface(OutfitFont(), TypefaceStyle.Bold);

            _timerText = new AndroidTextView(this) { Text = "00:00", Gravity = GravityFlags.End | GravityFlags.CenterVertical };
            _timerText.SetTextColor(colors.TextPrimary);
            _timerText.SetTypeface(OutfitFont(), TypefaceStyle.Bold);
            _timerText.SetTextSize(ComplexUnitType.Sp, 22);
            _timerText.SetMinWidth(Dp(56));

            _setInfoText = new AndroidTextView(this) { Text = string.Empty, Gravity = GravityFlags.End | GravityFlags.CenterVertical };
            _setInfoText.SetTextColor(colors.TextSecondary);
            _setInfoText.SetTextSize(ComplexUnitType.Sp, 12);
            _setInfoText.SetTypeface(OutfitFont(), TypefaceStyle.Normal);
            _setInfoText.SetMinWidth(Dp(28));

            headerRow.AddView(_headerText, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f));
            headerRow.AddView(_timerText, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WrapContent, LinearLayout.LayoutParams.WrapContent));
            headerRow.AddView(_setInfoText, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WrapContent, LinearLayout.LayoutParams.WrapContent));

            AndroidButton closeButton = CreateCloseButton(colors);
            closeButton.ContentDescription = "Close overlay";

            var headerRowParams = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MatchParent,
                FrameLayout.LayoutParams.WrapContent);
            var closeParams = new FrameLayout.LayoutParams(
                Dp(44), Dp(44))
            {
                Gravity = GravityFlags.End | GravityFlags.CenterVertical,
                RightMargin = Dp(2)
            };

            headerFrame.AddView(headerRow, headerRowParams);
            headerFrame.AddView(closeButton, closeParams);

            // Row 2: weight and reps steppers (hidden by default)
            _stepperRow = CreateStepperRow(colors);

            // Row 3: +Ns, Reset, and Log set (or Skip during rest) in one row
            var actionRow = new AndroidLinearLayout(this)
            {
                Orientation = Orientation.Horizontal
            };
            actionRow.SetGravity(GravityFlags.Center);

            _addTimeButton = CreateAddTimeButton(addSeconds, colors);
            _addTimeButton.ContentDescription = $"Add {addSeconds} seconds to rest";

            _resetButton = CreateIconButton(Resource.Drawable.ic_timer_reset, OnResetClicked, colors);
            _resetButton.ContentDescription = "Reset rest timer";

            _skipButton = CreateIconButton(Resource.Drawable.ic_timer_skip, OnSkipClicked, colors);
            _skipButton.ContentDescription = "Skip rest timer";

            _logSetButton = CreateLogSetButton(colors);
            _logSetButton.ContentDescription = "Log completed set";

            AddActionBtn(actionRow, _addTimeButton);
            AddActionBtn(actionRow, _resetButton);
            AddActionBtn(actionRow, _logSetButton);
            AddActionBtn(actionRow, _skipButton);

            root.AddView(headerFrame);
            root.AddView(_stepperRow);
            var actionRowParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WrapContent,
                LinearLayout.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.CenterHorizontal,
                TopMargin = Dp(8)
            };
            root.AddView(actionRow, actionRowParams);

            root.SetOnTouchListener(new OverlayDragListener(this));

            var width = Math.Min(ScreenWidth - Dp(24), Dp(380));
            _layoutParams = new WindowManagerLayoutParams(
                width,
                WindowManagerLayoutParams.WrapContent,
                WindowManagerTypes.ApplicationOverlay,
                WindowManagerFlags.NotFocusable | WindowManagerFlags.LayoutInScreen,
                Format.Translucent)
            {
                Gravity = GravityFlags.Top | GravityFlags.Start,
                X = Math.Max(0, (ScreenWidth - width) / 2),
                Y = StatusBarHeight + Dp(8)
            };

            _windowManager?.AddView(root, _layoutParams);
            _overlayView = root;

            StartTicker();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestOverlayService overlay failed: {ex}");
            StopSelf();
        }
    }

    private AndroidLinearLayout CreateStepperRow(
        (AndroidColor Background, AndroidColor Surface, AndroidColor TextPrimary, AndroidColor TextSecondary, AndroidColor Primary, AndroidColor Warning, AndroidColor Error) colors)
    {
        var row = new AndroidLinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        row.SetGravity(GravityFlags.Center);
        row.Visibility = ViewStates.Gone;

        // Weight stepper: [-] value kg
        AndroidTextButton weightMinus = CreateStepperBtn("-", colors, OnWeightMinus);
        _weightValue = new AndroidTextView(this) { Text = "0", Gravity = GravityFlags.Center };
        _weightValue.SetTextColor(colors.TextPrimary);
        _weightValue.SetTypeface(OutfitFont(), TypefaceStyle.Bold);
        _weightValue.SetTextSize(ComplexUnitType.Sp, 15);
        _weightValue.SetMinWidth(Dp(44));
        AndroidTextButton weightPlus = CreateStepperBtn("+", colors, OnWeightPlus);
        var weightLabel = new AndroidTextView(this) { Text = "kg", Gravity = GravityFlags.CenterVertical };
        weightLabel.SetTextColor(colors.TextSecondary);
        weightLabel.SetTextSize(ComplexUnitType.Sp, 11);

        var weightGroup = new AndroidLinearLayout(this) { Orientation = Orientation.Horizontal };
        weightGroup.SetGravity(GravityFlags.CenterVertical);
        AddStepperElement(weightGroup, weightMinus);
        weightGroup.AddView(_weightValue, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WrapContent, LinearLayout.LayoutParams.WrapContent));
        AddStepperElement(weightGroup, weightPlus);
        AddStepperElement(weightGroup, weightLabel);

        // Reps stepper: [-] value reps
        AndroidTextButton repsMinus = CreateStepperBtn("-", colors, OnRepsMinus);
        _repsValue = new AndroidTextView(this) { Text = "10", Gravity = GravityFlags.Center };
        _repsValue.SetTextColor(colors.TextPrimary);
        _repsValue.SetTypeface(OutfitFont(), TypefaceStyle.Bold);
        _repsValue.SetTextSize(ComplexUnitType.Sp, 15);
        _repsValue.SetMinWidth(Dp(36));
        AndroidTextButton repsPlus = CreateStepperBtn("+", colors, OnRepsPlus);
        var repsLabel = new AndroidTextView(this) { Text = "reps", Gravity = GravityFlags.CenterVertical };
        repsLabel.SetTextColor(colors.TextSecondary);
        repsLabel.SetTextSize(ComplexUnitType.Sp, 11);

        var repsGroup = new AndroidLinearLayout(this) { Orientation = Orientation.Horizontal };
        repsGroup.SetGravity(GravityFlags.CenterVertical);
        AddStepperElement(repsGroup, repsMinus);
        repsGroup.AddView(_repsValue, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WrapContent, LinearLayout.LayoutParams.WrapContent));
        AddStepperElement(repsGroup, repsPlus);
        AddStepperElement(repsGroup, repsLabel);

        var spacer = new AndroidView(this) { LayoutParameters = new LinearLayout.LayoutParams(0, 1, 1f) };

        row.AddView(weightGroup, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WrapContent, LinearLayout.LayoutParams.WrapContent));
        row.AddView(spacer);
        row.AddView(repsGroup, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WrapContent, LinearLayout.LayoutParams.WrapContent));

        return row;
    }

    /// <summary>Creates a stepper button matching the style of other overlay action buttons.</summary>
    private AndroidTextButton CreateStepperBtn(string text,
        (AndroidColor Background, AndroidColor Surface, AndroidColor TextPrimary, AndroidColor TextSecondary, AndroidColor Primary, AndroidColor Warning, AndroidColor Error) colors,
        Action onClick)
    {
        var btn = new AndroidTextButton(this)
        {
            Text = text
        };
        btn.SetTextColor(colors.Primary);
        btn.SetTypeface(OutfitFont(), TypefaceStyle.Bold);
        btn.SetTextSize(ComplexUnitType.Sp, 18);
        btn.SetAllCaps(false);
        btn.SetPadding(0, 0, 0, 0);
        btn.SetMinWidth(0);
        btn.SetMinHeight(0);
        btn.SetMinimumWidth(0);
        btn.SetMinimumHeight(0);
        btn.Gravity = GravityFlags.Center;

        var bg = new GradientDrawable();
        bg.SetColor(AndroidColor.Argb(0x1A, colors.Primary.R, colors.Primary.G, colors.Primary.B));
        bg.SetStroke(Dp(1), AndroidColor.Argb(0x4D, colors.Primary.R, colors.Primary.G, colors.Primary.B));
        bg.SetCornerRadius(Dp(10));
        btn.Background = bg;

        btn.Click += (s, e) =>
        {
            try { onClick(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"RestOverlayService stepper failed: {ex}"); }
        };
        return btn;
    }

    private static void AddStepperElement(AndroidLinearLayout row, AndroidView view)
    {
        var width = LinearLayout.LayoutParams.WrapContent;
        var height = DpStatic(row, 40);

        if (view is AndroidTextButton)
        {
            width = DpStatic(row, 36);
            height = DpStatic(row, 36);
        }

        var lp = new LinearLayout.LayoutParams(width, height)
        {
            LeftMargin = DpStatic(row, 2),
            RightMargin = DpStatic(row, 2),
            Gravity = GravityFlags.CenterVertical
        };
        row.AddView(view, lp);
    }

    private void OnWeightMinus() { _currentWeightKg = Math.Max(0, _currentWeightKg - 2.5); UpdateStepperDisplay(); }
    private void OnWeightPlus() { _currentWeightKg += 2.5; UpdateStepperDisplay(); }
    private void OnRepsMinus() { _currentReps = Math.Max(1, _currentReps - 1); UpdateStepperDisplay(); }
    private void OnRepsPlus() { _currentReps++; UpdateStepperDisplay(); }

    private void UpdateStepperDisplay()
    {
        _weightValue?.Text = _currentWeightKg % 1 == 0 ? $"{_currentWeightKg:0}" : $"{_currentWeightKg:0.#}";
        _repsValue?.Text = $"{_currentReps}";
    }

    private static void AddActionBtn(AndroidLinearLayout row, AndroidView button)
    {
        var lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WrapContent,
            DpStatic(row, 40))
        {
            LeftMargin = DpStatic(row, 4),
            RightMargin = DpStatic(row, 4)
        };
        row.AddView(button, lp);
    }

    private static int DpStatic(AndroidLinearLayout row, int value) =>
        (int)(value * row.Context!.Resources!.DisplayMetrics!.Density);

    /// <summary>Dedicated close button with subtle styling.</summary>
    private AndroidButton CreateCloseButton(
        (AndroidColor Background, AndroidColor Surface, AndroidColor TextPrimary, AndroidColor TextSecondary, AndroidColor Primary, AndroidColor Warning, AndroidColor Error) colors)
    {
        var button = new AndroidButton(this);
        button.SetImageResource(Resource.Drawable.ic_timer_close);
        button.SetScaleType(ImageView.ScaleType.FitCenter);
        button.SetAdjustViewBounds(true);
        button.SetPadding(Dp(6), Dp(6), Dp(6), Dp(6));
        button.SetColorFilter(colors.TextSecondary);

        var bg = new GradientDrawable();
        bg.SetColor(AndroidColor.Argb(0x0A, 0xFF, 0xFF, 0xFF));
        bg.SetCornerRadius(Dp(18));
        button.Background = bg;

        button.Click += (s, e) =>
        {
            try { OnCloseClicked(s, e); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"RestOverlayService close failed: {ex}"); }
        };
        return button;
    }

    private AndroidButton CreateIconButton(int iconRes, Action<object?, EventArgs> click,
        (AndroidColor Background, AndroidColor Surface, AndroidColor TextPrimary, AndroidColor TextSecondary, AndroidColor Primary, AndroidColor Warning, AndroidColor Error) colors)
    {
        var button = new AndroidButton(this);
        button.SetImageResource(iconRes);
        button.SetScaleType(ImageView.ScaleType.FitCenter);
        button.SetAdjustViewBounds(true);
        button.SetPadding(Dp(8), Dp(8), Dp(8), Dp(8));
        button.SetColorFilter(colors.TextSecondary);

        var bg = new GradientDrawable();
        bg.SetColor(AndroidColor.Argb(0x14, 0xFF, 0xFF, 0xFF));
        bg.SetCornerRadius(Dp(10));
        button.Background = bg;

        button.Click += (s, e) =>
        {
            try { click(s, e); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"RestOverlayService action failed: {ex}"); }
        };
        return button;
    }

    private AndroidButton CreateLogSetButton(
        (AndroidColor Background, AndroidColor Surface, AndroidColor TextPrimary, AndroidColor TextSecondary, AndroidColor Primary, AndroidColor Warning, AndroidColor Error) colors)
    {
        var button = new AndroidButton(this);
        button.SetImageResource(Resource.Drawable.ic_timer_check);
        button.SetScaleType(ImageView.ScaleType.FitCenter);
        button.SetAdjustViewBounds(true);
        button.SetPadding(Dp(8), Dp(8), Dp(8), Dp(8));
        button.SetColorFilter(colors.Primary);

        var bg = new GradientDrawable();
        bg.SetColor(AndroidColor.Argb(0x1A, colors.Primary.R, colors.Primary.G, colors.Primary.B));
        bg.SetStroke(Dp(1), AndroidColor.Argb(0x4D, colors.Primary.R, colors.Primary.G, colors.Primary.B));
        bg.SetCornerRadius(Dp(10));
        button.Background = bg;

        button.Click += (s, e) =>
        {
            try { OnLogSetClicked(s, e); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"RestOverlayService log set failed: {ex}"); }
        };
        return button;
    }

    private AndroidTextButton CreateAddTimeButton(int seconds,
        (AndroidColor Background, AndroidColor Surface, AndroidColor TextPrimary, AndroidColor TextSecondary, AndroidColor Primary, AndroidColor Warning, AndroidColor Error) colors)
    {
        var label = seconds < 60 ? $"+{seconds}s" : $"+{seconds / 60}:{seconds % 60:D2}";

        var button = new AndroidTextButton(this)
        {
            Text = label
        };
        button.SetTextColor(colors.Primary);
        button.SetTypeface(OutfitFont(), TypefaceStyle.Bold);
        button.SetTextSize(ComplexUnitType.Sp, 12);
        button.SetAllCaps(false);
        button.SetSingleLine(true);
        button.SetMaxLines(1);
        button.SetMinWidth(Dp(56));
        button.SetPadding(Dp(10), Dp(6), Dp(10), Dp(6));

        var bg = new GradientDrawable();
        bg.SetColor(AndroidColor.Argb(0x1A, colors.Primary.R, colors.Primary.G, colors.Primary.B));
        bg.SetStroke(Dp(1), AndroidColor.Argb(0x4D, colors.Primary.R, colors.Primary.G, colors.Primary.B));
        bg.SetCornerRadius(Dp(10));
        button.Background = bg;

        button.Click += (s, e) =>
        {
            try { OnAddClicked(s, e); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"RestOverlayService add time failed: {ex}"); }
        };
        return button;
    }

    private void OnCloseClicked(object? sender, EventArgs e)
    {
        // Dismiss the bubble. It comes back after the app is reopened.
        _dismissed = true;
        RemoveOverlayView();
    }

    private static void OnAddClicked(object? sender, EventArgs e)
    {
        WorkoutSessionService? session = ResolveSession();
        if (session == null)
            return;

        var addSeconds = ResolveSettings()?.AddTimeSeconds ?? RestAlertSettingsService.DefaultAddTimeSeconds;

        if (session.IsResting)
        {
            session.AddRestSeconds(addSeconds);
        }
        else
        {
            // Between sets: start a fresh rest of exactly the add-time amount
            if (addSeconds > 0)
                session.StartRest(addSeconds);
        }
    }

    private void OnLogSetClicked(object? sender, EventArgs e)
    {
        WorkoutQuickActionService? quickAction = ResolveQuickAction();
        if (quickAction == null)
            return;

        double? weight = _currentLogType != ExerciseLogType.Duration ? _currentWeightKg : null;
        int? reps = _currentLogType != ExerciseLogType.Duration ? _currentReps : null;
        _ = LogSetAsync(quickAction, weight, reps);
    }

    private static async Task LogSetAsync(WorkoutQuickActionService quickAction, double? weightKg, int? reps)
    {
        try
        {
            QuickActionResult result = weightKg != null || reps != null
                ? await quickAction.LogNextSetAsync(weightKg, reps)
                : await quickAction.LogNextSetAsync();

            if (result.Status == QuickActionStatus.NothingToLog)
                return;

            if (result.Status == QuickActionStatus.WorkoutCompleted)
            {
                var vibration = IPlatformApplication.Current?.Services.GetService(typeof(IVibrationService)) as IVibrationService;
                vibration?.Vibrate(TimeSpan.FromMilliseconds(800));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestOverlayService log set failed: {ex}");
        }
    }

    private static void OnSkipClicked(object? sender, EventArgs e)
    {
        WorkoutSessionService? session = ResolveSession();
        if (session == null)
            return;

        session.SkipRest();
    }

    private static void OnResetClicked(object? sender, EventArgs e)
    {
        WorkoutSessionService? session = ResolveSession();
        if (session == null)
            return;

        // Always restart the countdown with the exercise's default rest
        // interval, even when the current rest was started or changed by +Ns.
        var baseRest = GetExerciseRestSeconds(session);
        if (baseRest > 0)
            session.StartRest(baseRest);
    }

    private static int GetExerciseRestSeconds(WorkoutSessionService session)
    {
        WorkoutPlan? plan = session.CurrentPlan;
        if (plan == null) return 0;
        var idx = session.GetFirstUncompletedExerciseIndex();
        if (idx < 0 || idx >= plan.Exercises.Count) return 0;
        return plan.Exercises[idx].RestIntervalSeconds;
    }

    /// <summary>Opens the app when the bubble body is tapped (allowed: the app holds the overlay permission).</summary>
    private void OpenApp()
    {
        try
        {
            Intent? launch = PackageManager!.GetLaunchIntentForPackage(PackageName!);
            if (launch == null)
                return;

            launch.AddFlags(ActivityFlags.SingleTop);
            StartActivity(launch);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestOverlayService open app failed: {ex}");
        }
    }

    private void UpdateTicker()
    {
        if (_stopping)
            return;

        WorkoutSessionService? session = ResolveSession();
        if (session == null || session.CurrentPlan == null)
        {
            StopSelf();
            return;
        }

        if (_overlayView == null)
            return;

        WorkoutPlan plan = session.CurrentPlan;
        var exerciseIndex = session.GetFirstUncompletedExerciseIndex();
        string? nextExerciseName = null;
        int? nextSetIndex = null;
        int? nextSetTotal = null;
        ExerciseLogType logType = ExerciseLogType.WeightAndReps;

        if (exerciseIndex >= 0 && exerciseIndex < plan.Exercises.Count)
        {
            ExercisePlan exercise = plan.Exercises[exerciseIndex];
            nextExerciseName = exercise.Name;
            nextSetTotal = exercise.SetCount;
            logType = exercise.LogType;
            var defaultWeight = exercise.DefaultWeightKg ?? 0;
            var defaultReps = exercise.DefaultReps ?? 10;
            var setIndex = session.GetFirstUncompletedSetIndex(exerciseIndex);
            nextSetIndex = setIndex >= 0 ? setIndex + 1 : null;

            // Reset weight/reps when exercise changes
            if (exerciseIndex != _trackedExerciseIndex)
            {
                _trackedExerciseIndex = exerciseIndex;
                _currentWeightKg = defaultWeight;
                _currentReps = defaultReps;
                _currentLogType = logType;
                UpdateStepperDisplay();
            }
        }

        UpdateOverlayUi(session, nextExerciseName, nextSetIndex, nextSetTotal, logType);
    }

    /// <summary>Renders the rest timer or between-sets state into the overlay views.</summary>
    private void UpdateOverlayUi(
        WorkoutSessionService session,
        string? nextExerciseName,
        int? nextSetIndex,
        int? nextSetTotal,
        ExerciseLogType logType)
    {
        (AndroidColor Background, AndroidColor Surface, AndroidColor TextPrimary, AndroidColor TextSecondary, AndroidColor Primary, AndroidColor Warning, AndroidColor Error) colors = GetThemeColors();

        if (session.IsResting)
        {
            var remaining = session.RestSecondsRemaining;

            _headerText!.Text = nextExerciseName ?? string.Empty;
            _timerText!.Text = FormatTimer(remaining);
            SetVisible(_timerText, true);

            if (remaining <= 5)
                _timerText.SetTextColor(colors.Error);
            else if (remaining <= 10)
                _timerText.SetTextColor(colors.Warning);
            else
                _timerText.SetTextColor(colors.TextPrimary);

            _setInfoText!.Text = string.Empty;

            // During rest: no steppers/log set, show action buttons
            SetVisible(_stepperRow, false);
            SetVisible(_logSetButton, false);
            SetVisible(_addTimeButton, true);
            SetVisible(_resetButton, true);
            SetVisible(_skipButton, true);
        }
        else
        {
            // Hide timer when not resting
            SetVisible(_timerText, false);

            if (nextExerciseName != null)
            {
                _headerText!.Text = nextExerciseName;
                _setInfoText!.Text = nextSetIndex != null
                    ? $"{nextSetIndex}/{nextSetTotal}"
                    : string.Empty;

                // Between sets: show steppers, log set, and +Ns/Reset
                var showSteppers = logType != ExerciseLogType.Duration;
                SetVisible(_stepperRow, showSteppers);
                SetVisible(_logSetButton, true);
                SetVisible(_addTimeButton, true);
                SetVisible(_resetButton, true);
                SetVisible(_skipButton, false);
            }
            else
            {
                _headerText!.Text = "Workout complete";
                _setInfoText!.Text = string.Empty;
                SetVisible(_stepperRow, false);
                SetVisible(_logSetButton, false);
                SetVisible(_addTimeButton, false);
                SetVisible(_resetButton, false);
                SetVisible(_skipButton, false);
            }
        }
    }

    private static void SetVisible(AndroidView? button, bool visible)
    {
        if (button == null)
            return;

        button.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
    }

    /// <summary>Formats the timer like the in-app rest timer: MM:SS at or above 60 seconds, plain seconds below 60 seconds.</summary>
    private static string FormatTimer(int totalSeconds)
    {
        if (totalSeconds >= 60)
        {
            var m = totalSeconds / 60;
            var s = totalSeconds % 60;
            return $"{m}:{s:D2}";
        }
        return $"{totalSeconds}";
    }

    private static WorkoutSessionService? ResolveSession() =>
        IPlatformApplication.Current?.Services.GetService(typeof(WorkoutSessionService)) as WorkoutSessionService;

    private static WorkoutQuickActionService? ResolveQuickAction() =>
        IPlatformApplication.Current?.Services.GetService(typeof(WorkoutQuickActionService)) as WorkoutQuickActionService;

    private static RestAlertSettingsService? ResolveSettings() =>
        IPlatformApplication.Current?.Services.GetService(typeof(RestAlertSettingsService)) as RestAlertSettingsService;

    private static WorkoutTimerState ReadState(Intent intent)
    {
        var endTicks = intent.GetLongExtra(ExtraEndUtcTicks, 0);
        var remaining = intent.GetIntExtra(ExtraRemainingSeconds, 0);
        var title = intent.GetStringExtra(ExtraTitle) ?? "Physiquinator";
        var nextExercise = intent.GetStringExtra(ExtraNextExerciseName) ?? string.Empty;
        var nextExerciseIndex = intent.GetIntExtra(ExtraNextExerciseIndex, -1);
        var nextSetIndex = intent.GetIntExtra(ExtraNextSetIndex, -1);
        var nextSetTotal = intent.GetIntExtra(ExtraNextSetTotal, -1);

        return new WorkoutTimerState(
            title,
            endTicks > 0 ? new DateTime(endTicks, DateTimeKind.Utc) : null,
            remaining,
            string.IsNullOrEmpty(nextExercise) ? null : nextExercise,
            nextExerciseIndex >= 0 ? nextExerciseIndex : null,
            nextSetIndex >= 0 ? nextSetIndex : null,
            nextSetTotal >= 0 ? nextSetTotal : null);
    }

    private static WorkoutTimerState ReadSessionState()
    {
        WorkoutSessionService? session = ResolveSession();
        if (session == null || session.CurrentPlan == null)
            return new WorkoutTimerState(null, null, 0, null, null, null, null);

        WorkoutPlan plan = session.CurrentPlan;
        var exerciseIndex = session.GetFirstUncompletedExerciseIndex();
        string? nextExercise = null;
        int? nextSetIndex = null;
        int? nextSetTotal = null;
        if (exerciseIndex >= 0 && exerciseIndex < plan.Exercises.Count)
        {
            ExercisePlan exercise = plan.Exercises[exerciseIndex];
            nextExercise = exercise.Name;
            nextSetTotal = exercise.SetCount;
            var setIndex = session.GetFirstUncompletedSetIndex(exerciseIndex);
            nextSetIndex = setIndex >= 0 ? setIndex + 1 : null;
        }

        return new WorkoutTimerState(
            plan.Name,
            session.RestEndsAtUtc,
            session.RestSecondsRemaining,
            nextExercise,
            exerciseIndex >= 0 ? exerciseIndex : null,
            nextSetIndex,
            nextSetTotal);
    }

    internal int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density);

    private int StatusBarHeight
    {
        get
        {
            var id = Resources!.GetIdentifier("status_bar_height", "dimen", "android");
            return id > 0 ? Resources!.GetDimensionPixelSize(id) : 0;
        }
    }

    private int ScreenWidth
    {
        get
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R && _windowManager != null)
                return _windowManager.CurrentWindowMetrics.Bounds.Width();

            using DisplayMetrics metrics = Resources!.DisplayMetrics!;
            return metrics.WidthPixels;
        }
    }

    /// <summary>
    /// Resolves the effective theme by reading the user's preference from
    /// <see cref="IAppPreferences"/> and falling back to the OS theme when
    /// set to "system". Returns <c>true</c> if dark mode is active.
    /// </summary>
    private static bool IsDarkTheme()
    {
        try
        {
            if (IPlatformApplication.Current?.Services.GetService(typeof(IAppPreferences)) is not IAppPreferences preferences)
                return true; // dark is the safe default

            var profileSuffix = IPlatformApplication.Current?.Services.GetService(typeof(UserProfileService)) is UserProfileService profileService
                ? $"_{profileService.GetActiveProfile().Id}"
                : string.Empty;

            var key = $"{PreferenceKeys.ThemePreference}{profileSuffix}";
            var preference = preferences.Get(key, string.Empty);

            // Fall back to the unsuffixed key (written by JS localStorage fallback)
            if (string.IsNullOrEmpty(preference))
                preference = preferences.Get(PreferenceKeys.ThemePreference, string.Empty);

            if (preference == ThemePreference.Light)
                return false;
            if (preference == ThemePreference.Dark)
                return true;

            // "system" or unknown: check OS theme
            return (Microsoft.Maui.Controls.Application.Current?.RequestedTheme) != AppTheme.Light;
        }
        catch
        {
            return true;
        }
    }

    private (AndroidColor Background, AndroidColor Surface, AndroidColor TextPrimary, AndroidColor TextSecondary, AndroidColor Primary, AndroidColor Warning, AndroidColor Error) GetThemeColors()
    {
        if (IsDarkTheme())
            return (DarkBackground, DarkSurface, DarkTextPrimary, DarkTextSecondary, DarkPrimary, DarkWarning, DarkError);
        return (LightBackground, LightSurface, LightTextPrimary, LightTextSecondary, LightPrimary, LightWarning, LightError);
    }

    private sealed class OverlayDragListener(RestOverlayService service) : Java.Lang.Object, AndroidView.IOnTouchListener
    {
        private float _downRawX;
        private float _downRawY;
        private int _downLpX;
        private int _downLpY;
        private bool _moved;

        public bool OnTouch(AndroidView? v, MotionEvent? e)
        {
            if (v == null || e == null || service._layoutParams == null || service._windowManager == null)
                return false;

            switch (e.Action)
            {
                case MotionEventActions.Down:
                    _downRawX = e.RawX;
                    _downRawY = e.RawY;
                    _downLpX = service._layoutParams.X;
                    _downLpY = service._layoutParams.Y;
                    _moved = false;
                    return true;

                case MotionEventActions.Move:
                    var dx = e.RawX - _downRawX;
                    var dy = e.RawY - _downRawY;
                    if (!_moved && Math.Abs(dx) < 10 && Math.Abs(dy) < 10)
                        return true;

                    _moved = true;
                    WindowManagerLayoutParams lp = service._layoutParams;
                    lp.X = _downLpX + (int)dx;
                    lp.Y = _downLpY + (int)dy;
                    service._windowManager.UpdateViewLayout(v, lp);
                    return true;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    if (_moved && e.Action == MotionEventActions.Up)
                    {
                        // Snap floating bubble towards the nearest screen edge (left or right)
                        WindowManagerLayoutParams lpSnap = service._layoutParams;
                        var screenWidth = service.ScreenWidth;
                        var bubbleWidth = lpSnap.Width;
                        var currentCenterX = lpSnap.X + (bubbleWidth / 2);
                        var margin = service.Dp(12);

                        if (currentCenterX < screenWidth / 2)
                        {
                            lpSnap.X = margin;
                        }
                        else
                        {
                            lpSnap.X = Math.Max(margin, screenWidth - bubbleWidth - margin);
                        }
                        service._windowManager.UpdateViewLayout(v, lpSnap);
                    }
                    else if (!_moved && e.Action == MotionEventActions.Up)
                    {
                        // A tap (no drag) on the bubble body opens the app.
                        service.OpenApp();
                    }
                    return true;

                default:
                    return false;
            }
        }
    }
}
