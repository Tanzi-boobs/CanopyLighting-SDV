using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>
/// Adds a restrained, time-dependent light gradient directly to mature tree crowns.
/// The original tree texture is reused as the mask, so no rectangles are painted over
/// the grass, trunk, buildings, or nearby sprites.
/// </summary>
internal static class TreeCanopyLighting
{
    private const string HarmonyId = "Tanzi.CanopyLighting";
    private const int Strips = 6;
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static ModConfig? _config;
    private static IMonitor? _monitor;
    private static Harmony? _harmony;
    private static Type? _treeType;
    private static Type? _gameType;
    private static FieldInfo? _treeTopSourceRect;
    private static FieldInfo? _texture;
    private static FieldInfo? _growthStage;
    private static FieldInfo? _treeKind;
    private static FieldInfo? _hasSeed;
    private static FieldInfo? _flipped;
    private static FieldInfo? _stump;
    private static FieldInfo? _alpha;
    private static FieldInfo? _shakeRotation;
    private static FieldInfo? _viewport;
    private static FieldInfo? _currentLocation;
    private static FieldInfo? _timeOfDay;
    private static bool _runtimeDisabled;
    private static bool _loggedRuntimeFailure;

    public static void Install(ModConfig config, IMonitor monitor)
    {
        _config = config;
        _monitor = monitor;

        _treeType = AccessTools.TypeByName("StardewValley.TerrainFeatures.Tree")
            ?? throw new InvalidOperationException("Couldn't find StardewValley.TerrainFeatures.Tree.");
        _gameType = AccessTools.TypeByName("StardewValley.Game1")
            ?? throw new InvalidOperationException("Couldn't find StardewValley.Game1.");

        _treeTopSourceRect = AccessTools.Field(_treeType, "treeTopSourceRect");
        _texture = AccessTools.Field(_treeType, "texture");
        _growthStage = AccessTools.Field(_treeType, "growthStage");
        _treeKind = AccessTools.Field(_treeType, "treeType");
        _hasSeed = AccessTools.Field(_treeType, "hasSeed");
        _flipped = AccessTools.Field(_treeType, "flipped");
        _stump = AccessTools.Field(_treeType, "stump");
        _alpha = AccessTools.Field(_treeType, "alpha");
        _shakeRotation = AccessTools.Field(_treeType, "shakeRotation");
        _viewport = AccessTools.Field(_gameType, "viewport");
        _currentLocation = AccessTools.Field(_gameType, "currentLocation");
        _timeOfDay = AccessTools.Field(_gameType, "timeOfDay");

        MethodInfo? drawMethod = _treeType
            .GetMethods(InstanceFlags)
            .FirstOrDefault(method =>
            {
                if (method.Name != "draw")
                    return false;

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2
                    && parameters[1].ParameterType.FullName == "Microsoft.Xna.Framework.Vector2";
            });

        if (drawMethod is null)
            throw new InvalidOperationException("Couldn't find the two-argument Tree.draw method.");

        MethodInfo postfix = typeof(TreeCanopyLighting).GetMethod(
            nameof(OnTreeDrawPostfix),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Couldn't find the canopy draw hook.");

        _harmony = new Harmony(HarmonyId);
        _harmony.Patch(drawMethod, postfix: new HarmonyMethod(postfix));
        monitor.Log("Canopy lighting hook installed for mature tree crowns.", LogLevel.Info);
    }

    private static void OnTreeDrawPostfix(object __instance, object[] __args)
    {
        if (_runtimeDisabled || _config is null || !_config.Enabled || _config.Strength <= 0.001f)
            return;

        try
        {
            if (__args.Length < 2 || _gameType is null || _currentLocation is null || _viewport is null || _timeOfDay is null)
                return;

            object? location = _currentLocation.GetValue(null);
            if (location is null || !ReadBooleanMember(location, "IsOutdoors", false))
                return;

            if (Convert.ToInt32(ReadNetValue(__instance, _growthStage, 0)) < 5)
                return;
            if (Convert.ToBoolean(ReadNetValue(__instance, _stump, false)))
                return;

            int time = Convert.ToInt32(_timeOfDay.GetValue(null) ?? 1200);
            float solar = GetDaylightFactor(time / 100f);
            if (solar <= 0.01f)
                return;

            object? topRect = _treeTopSourceRect?.GetValue(__instance);
            object? lazyTexture = _texture?.GetValue(__instance);
            object? texture = lazyTexture is null ? null : ReadMember(lazyTexture, "Value");
            if (topRect is null || texture is null)
                return;

            Type rectangleType = topRect.GetType();
            Type? vectorType = rectangleType.Assembly.GetType("Microsoft.Xna.Framework.Vector2");
            Type? colorType = rectangleType.Assembly.GetType("Microsoft.Xna.Framework.Color");
            Type? effectsType = rectangleType.Assembly.GetType("Microsoft.Xna.Framework.Graphics.SpriteEffects");
            if (vectorType is null || colorType is null || effectsType is null)
                return;

            int sourceX = ReadIntMember(topRect, "X");
            int sourceY = ReadIntMember(topRect, "Y");
            int sourceWidth = ReadIntMember(topRect, "Width");
            int sourceHeight = ReadIntMember(topRect, "Height");
            if (sourceWidth < 2 || sourceHeight < 2)
                return;

            int treeKind = Convert.ToInt32(ReadNetValue(__instance, _treeKind, 0));
            bool hasSeed = Convert.ToBoolean(ReadNetValue(__instance, _hasSeed, false));
            // Vanilla draws the second palm tree from one of two adjacent canopy frames.
            if (treeKind == 9)
                sourceX = hasSeed ? 48 : 0;

            bool flipped = Convert.ToBoolean(ReadNetValue(__instance, _flipped, false));
            float treeAlpha = Math.Clamp(ReadFloatField(__instance, _alpha, 1f), 0f, 1f);
            if (treeAlpha <= 0.01f)
                return;
            float rotation = ReadFloatField(__instance, _shakeRotation, 0f);

            object? viewport = _viewport.GetValue(null);
            if (viewport is null)
                return;

            object tile = __args[1];
            float tileX = ReadFloatMember(tile, "X");
            float tileY = ReadFloatMember(tile, "Y");
            float viewX = ReadFloatMember(viewport, "X");
            float viewY = ReadFloatMember(viewport, "Y");
            object position = Activator.CreateInstance(
                vectorType,
                new object[] { tileX * 64f + 32f - viewX, tileY * 64f + 64f - viewY })!;

            object? batch = __args[0];
            if (batch is null)
                return;

            float hour = time / 100f;
            float progress = Math.Clamp((hour - 6f) / 12f, 0f, 1f);
            float noon = Math.Clamp(1f - Math.Abs(progress - 0.5f) * 2f, 0f, 1f);
            float diffuse = 0.30f + 0.30f * noon;
            float directional = 0.48f * (1f - noon);
            float sunDirection = progress < 0.5f ? 1f : -1f;
            float warmth = Math.Clamp(_config.Warmth, 0f, 1f);
            float strength = Math.Clamp(_config.Strength, 0f, 1f);
            float tintAlpha = 112f * strength * solar * treeAlpha;
            if (tintAlpha < 1f)
                return;

            // Use short strips rather than one left/right split. This creates a soft pixel-art
            // gradient across the canopy without a shader or a full-screen render target.
            int stripWidth = Math.Max(1, (int)Math.Ceiling(sourceWidth / (float)Strips));
            for (int start = 0; start < sourceWidth; start += stripWidth)
            {
                int width = Math.Min(stripWidth, sourceWidth - start);
                float sourceXNorm = ((start + width * 0.5f) / sourceWidth) * 2f - 1f;
                float screenXNorm = flipped ? -sourceXNorm : sourceXNorm;
                float lightness = Math.Clamp(diffuse - screenXNorm * sunDirection * directional, 0.12f, 0.94f);

                // The shaded tone stays green-brown, while the lit tone warms towards sunlight.
                int red = (int)Math.Round(Lerp(24f, 255f, lightness));
                int green = (int)Math.Round(Lerp(39f, 242f - 34f * warmth, lightness));
                int blue = (int)Math.Round(Lerp(30f, 214f - 78f * warmth, lightness));
                object color = Activator.CreateInstance(
                    colorType,
                    new object[] { (byte)red, (byte)green, (byte)blue, (byte)Math.Clamp((int)tintAlpha, 0, 255) })!;

                object sourceRect = Activator.CreateInstance(
                    rectangleType,
                    new object[] { sourceX + start, sourceY, width, sourceHeight })!;

                // Keep each strip anchored to the same 48x96 canopy origin used by Tree.draw.
                // When flipped, the sub-rectangle's origin is mirrored as well.
                float originX = flipped ? start + width - 24f : 24f - start;
                object origin = Activator.CreateInstance(
                    vectorType,
                    new object[] { originX, 96f })!;

                object effects = Enum.ToObject(effectsType, flipped ? 1 : 0);
                // Slightly nearer than the vanilla crown, but still behind its trunk.
                float layerDepth = ((tileY * 64f + 64f + 2f) / 10000f) - tileX / 1000000f - 0.0000001f;

                dynamic spriteBatch = batch;
                dynamic canopyTexture = texture;
                dynamic screenPosition = position;
                dynamic source = sourceRect;
                dynamic tint = color;
                dynamic drawOrigin = origin;
                dynamic spriteEffects = effects;

                spriteBatch.Draw(
                    canopyTexture,
                    screenPosition,
                    source,
                    tint,
                    rotation,
                    drawOrigin,
                    4f,
                    spriteEffects,
                    layerDepth);
            }
        }
        catch (Exception ex)
        {
            _runtimeDisabled = true;
            if (!_loggedRuntimeFailure)
            {
                _loggedRuntimeFailure = true;
                _monitor?.Log(
                    "Canopy lighting disabled after a render-hook error: " + ex.GetType().Name + ": " + ex.Message,
                    LogLevel.Warn);
            }
        }
    }

    private static object? ReadNetValue(object instance, FieldInfo? field, object fallback)
    {
        if (field?.GetValue(instance) is not object netField)
            return fallback;

        object? value = ReadMember(netField, "Value");
        return value ?? fallback;
    }

    private static object? ReadMember(object instance, string name)
    {
        Type type = instance.GetType();
        return AccessTools.Property(type, name)?.GetValue(instance)
            ?? AccessTools.Field(type, name)?.GetValue(instance);
    }

    private static bool ReadBooleanMember(object instance, string name, bool fallback)
    {
        object? value = ReadMember(instance, name);
        return value is null ? fallback : Convert.ToBoolean(value);
    }

    private static int ReadIntMember(object instance, string name)
    {
        object? value = ReadMember(instance, name);
        return value is null ? 0 : Convert.ToInt32(value);
    }

    private static float ReadFloatMember(object instance, string name)
    {
        object? value = ReadMember(instance, name);
        return value is null ? 0f : Convert.ToSingle(value);
    }

    private static float ReadFloatField(object instance, FieldInfo? field, float fallback)
    {
        object? value = field?.GetValue(instance);
        return value is null ? fallback : Convert.ToSingle(value);
    }

    private static float GetDaylightFactor(float hour)
    {
        // Fade the light in after sunrise and out around sunset; no crown tint at night.
        float sunrise = SmoothStep(6f, 7.3f, hour);
        float sunset = 1f - SmoothStep(16.7f, 18.6f, hour);
        return Math.Clamp(sunrise * sunset, 0f, 1f);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;
}
