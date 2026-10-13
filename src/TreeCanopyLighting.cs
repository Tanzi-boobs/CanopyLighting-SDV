using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>
/// Draws the standalone canopy shader technique at the same point in the world draw order
/// as each vanilla tree. The shader is loaded from the user's DynamicShader installation;
/// this mod deliberately does not redistribute the compiled shader asset.
/// </summary>
internal static class TreeCanopyLighting
{
    private const string HarmonyId = "Tanzi.CanopyLighting";
    private const int OakTreeType = 1;
    private const int PineTreeType = 3;
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static ModConfig? _config;
    private static IMonitor? _monitor;
    private static Harmony? _harmony;
    private static Type? _treeType;
    private static Type? _gameType;
    private static FieldInfo? _treeTopSourceRect;
    private static FieldInfo? _texture;
    private static FieldInfo? _growthStage;
    private static FieldInfo? _treeKind;
    private static FieldInfo? _stump;
    private static FieldInfo? _alpha;
    private static FieldInfo? _shakeRotation;
    private static FieldInfo? _viewport;
    private static FieldInfo? _currentLocation;
    private static FieldInfo? _timeOfDay;

    private static string? _shaderPath;
    private static object? _effect;
    private static bool _effectInitializationAttempted;
    private static bool _runtimeDisabled;
    private static bool _loggedRuntimeFailure;

    public static void Install(ModConfig config, IMonitor monitor, string modDirectory)
    {
        _config = config;
        _monitor = monitor;

        // The standalone release bundles this shader inside our own mod folder.
        // Dynamic Shader is not required at runtime.
        _shaderPath = Path.Combine(Path.GetFullPath(modDirectory), "assets", "shaders", "ShadowEffect.mgfxo");
        if (!File.Exists(_shaderPath))
        {
            throw new FileNotFoundException(
                "Canopy Lighting's bundled shader is missing: assets/shaders/ShadowEffect.mgfxo. Reinstall the complete CanopyLighting folder.");
        }

        _treeType = AccessTools.TypeByName("StardewValley.TerrainFeatures.Tree")
            ?? throw new InvalidOperationException("Couldn't find StardewValley.TerrainFeatures.Tree.");
        _gameType = AccessTools.TypeByName("StardewValley.Game1")
            ?? throw new InvalidOperationException("Couldn't find StardewValley.Game1.");

        _treeTopSourceRect = AccessTools.Field(_treeType, "treeTopSourceRect");
        _texture = AccessTools.Field(_treeType, "texture");
        _growthStage = AccessTools.Field(_treeType, "growthStage");
        _treeKind = AccessTools.Field(_treeType, "treeType");
        _stump = AccessTools.Field(_treeType, "stump");
        _alpha = AccessTools.Field(_treeType, "alpha");
        _shakeRotation = AccessTools.Field(_treeType, "shakeRotation");
        _viewport = AccessTools.Field(_gameType, "viewport");
        _currentLocation = AccessTools.Field(_gameType, "currentLocation");
        _timeOfDay = AccessTools.Field(_gameType, "timeOfDay");

        // Stardew Valley 1.6.15 uses TerrainFeature.draw(SpriteBatch), with the
        // tree tile exposed by the Tree.Tile property. The older two-argument
        // signature doesn't exist in this runtime (including Cinderbox Android).
        MethodInfo? drawMethod = _treeType
            .GetMethods(InstanceFlags)
            .FirstOrDefault(method =>
            {
                if (method.Name != "draw")
                    return false;

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 1
                    && parameters[0].ParameterType.FullName == "Microsoft.Xna.Framework.Graphics.SpriteBatch";
            });

        if (drawMethod is null)
            throw new InvalidOperationException("Couldn't find the two-argument Tree.draw method.");

        MethodInfo postfix = typeof(TreeCanopyLighting).GetMethod(
            nameof(OnTreeDrawPostfix),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Couldn't find the canopy draw hook.");

        _harmony = new Harmony(HarmonyId);
        _harmony.Patch(drawMethod, postfix: new HarmonyMethod(postfix));

        monitor.Log("Standalone canopy shader loaded from this mod's own assets.", LogLevel.Info);
    }

    private static void OnTreeDrawPostfix(object __instance, object[] __args)
    {
        if (_runtimeDisabled || _config is null || !_config.Enabled || _config.OverallStrength <= 0.001f)
            return;

        try
        {
            if (__args.Length < 1 || _gameType is null || _currentLocation is null || _viewport is null || _timeOfDay is null)
                return;

            object? location = _currentLocation.GetValue(null);
            if (location is null || !ReadBooleanMember(location, "IsOutdoors", false))
                return;

            if (Convert.ToInt32(ReadNetValue(__instance, _growthStage, 0)) < 5)
                return;
            if (Convert.ToBoolean(ReadNetValue(__instance, _stump, false)))
                return;

            int treeKind = Convert.ToInt32(ReadNetValue(__instance, _treeKind, 0));
            bool isOak = treeKind == OakTreeType;
            bool isPine = treeKind == PineTreeType;
            if ((!isOak || !_config.EnableOakCanopy) && (!isPine || !_config.EnablePineCanopy))
                return;

            int time = Convert.ToInt32(_timeOfDay.GetValue(null) ?? 1200);
            float hour = time / 100f;
            float solar = GetDaylightFactor(hour);
            if (solar <= 0.001f)
                return;

            object? topRect = _treeTopSourceRect?.GetValue(__instance);
            object? lazyTexture = _texture?.GetValue(__instance);
            object? texture = lazyTexture is null ? null : ReadMember(lazyTexture, "Value") ?? lazyTexture;
            if (topRect is null || texture is null)
                return;

            float treeAlpha = ReadFloatField(__instance, _alpha, 1f);
            // Avoid a full-opacity shader overlay while vanilla is fading a chopped or shaken tree.
            if (treeAlpha < 0.98f)
                return;

            float rotation = ReadFloatField(__instance, _shakeRotation, 0f);
            object? viewport = _viewport.GetValue(null);
            if (viewport is null)
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
            int textureWidth = ReadIntMember(texture, "Width");
            int textureHeight = ReadIntMember(texture, "Height");
            if (sourceWidth < 2 || sourceHeight < 2 || textureWidth < 1 || textureHeight < 1)
                return;

            bool flipped = Convert.ToBoolean(ReadNetValue(__instance, AccessTools.Field(_treeType!, "flipped"), false));
            int left = ToChannel(sourceX / (float)textureWidth);
            int right = ToChannel((sourceX + sourceWidth) / (float)textureWidth);
            int top = ToChannel(sourceY / (float)textureHeight);
            int bottom = ToChannel((sourceY + sourceHeight) / (float)textureHeight);

            // The canopy shader encodes the normalized source-rectangle bounds in Color.rgba.
            // Swapping R/B tells its shader that this sprite is horizontally flipped.
            object encodedColor = Activator.CreateInstance(
                colorType,
                new object[] { (byte)(flipped ? right : left), (byte)top, (byte)(flipped ? left : right), (byte)bottom })!;

            object sourceRect = Activator.CreateInstance(
                rectangleType,
                new object[] { sourceX, sourceY, sourceWidth, sourceHeight })!;

            object? tile = ReadMember(__instance, "Tile");
            if (tile is null)
                return;

            float tileX = ReadFloatMember(tile, "X");
            float tileY = ReadFloatMember(tile, "Y");
            float viewX = ReadFloatMember(viewport, "X");
            float viewY = ReadFloatMember(viewport, "Y");
            object position = Activator.CreateInstance(
                vectorType,
                new object[] { tileX * 64f + 32f - viewX, tileY * 64f + 64f - viewY })!;
            object origin = Activator.CreateInstance(vectorType, new object[] { 24f, 96f })!;
            object effects = Enum.ToObject(effectsType, flipped ? 1 : 0);

            object? batch = __args[0];
            if (batch is null)
                return;

            EnsureEffectLoaded();
            SetCanopyParameters(isOak, hour, solar);

            DrawCanopyWithEffect(
                batch,
                texture,
                position,
                sourceRect,
                encodedColor,
                rotation,
                origin,
                effects);
        }
        catch (Exception ex)
        {
            _runtimeDisabled = true;
            if (!_loggedRuntimeFailure)
            {
                _loggedRuntimeFailure = true;
                _monitor?.Log(
                    "Canopy shader disabled after a render-hook error: " + ex.GetType().Name + ": " + ex.Message,
                    LogLevel.Warn);
            }
        }
    }

    private static void EnsureEffectLoaded()
    {
        if (_effect is not null)
            return;
        if (_effectInitializationAttempted)
            throw new InvalidOperationException("The bundled canopy effect could not be initialized earlier.");
        _effectInitializationAttempted = true;

        if (_gameType is null || string.IsNullOrEmpty(_shaderPath) || !File.Exists(_shaderPath))
            throw new FileNotFoundException("Canopy Lighting's bundled ShadowEffect.mgfxo is missing.");

        object? graphicsManager = ReadStaticMember(_gameType, "graphics");
        object? graphicsDevice = graphicsManager is null ? null : ReadMember(graphicsManager, "GraphicsDevice");
        if (graphicsDevice is null)
            throw new InvalidOperationException("The game's GraphicsDevice is not available yet.");

        Type effectType = AccessTools.TypeByName("Microsoft.Xna.Framework.Graphics.Effect")
            ?? throw new InvalidOperationException("Couldn't find MonoGame's Effect type.");

        _effect = Activator.CreateInstance(effectType, new object[] { graphicsDevice, File.ReadAllBytes(_shaderPath) })
            ?? throw new InvalidOperationException("MonoGame failed to create the bundled canopy Effect.");
    }

    private static void SetCanopyParameters(bool isOak, float hour, float solar)
    {
        if (_effect is null || _config is null)
            throw new InvalidOperationException("The canopy shader has not been loaded.");

        float dayProgress = Math.Clamp((hour - 6f) / 12f, 0f, 1f);
        float sunX = MathF.Cos(dayProgress * MathF.PI);
        Type? vectorType = AccessTools.TypeByName("Microsoft.Xna.Framework.Vector2");
        if (vectorType is null)
            throw new InvalidOperationException("Couldn't find MonoGame's Vector2 type.");

        object sunDirection = Activator.CreateInstance(vectorType, new object[] { sunX, -0.55f })!;
        string technique = isOak ? "TreeCanopyLight" : "TextureGuidedPineCanopyLight";
        SetCurrentTechnique(technique);
        SetEffectParameter("TreeCanopyLightSunDirection", sunDirection);

        float strength = Math.Clamp(_config.OverallStrength, 0f, 2f) * solar;
        if (isOak)
        {
            SetVector4("TreeCanopyLightColor", 1.05f, 1.00f, 0.90f, 1f);
            SetVector4("TreeCanopyShadowColor", 0.78f, 0.84f, 0.92f, 1f);
            SetFloat("TreeCanopyLightStrength", Math.Clamp(_config.OakLightStrength, 0f, 5f) * strength);
            SetFloat("TreeCanopyShadowStrength", Math.Clamp(_config.OakShadowStrength, 0f, 5f) * strength);
        }
        else
        {
            SetVector4("TextureGuidedPineLightColor", 1.05f, 1.00f, 0.92f, 1f);
            SetVector4("TextureGuidedPineShadowColor", 0.78f, 0.84f, 0.92f, 1f);
            SetVector4("TextureGuidedPineRimColor", 1.12f, 1.06f, 0.90f, 1f);
            SetFloat("TextureGuidedPineLightStrength", Math.Clamp(_config.PineLightStrength, 0f, 5f) * strength);
            SetFloat("TextureGuidedPineShadowStrength", Math.Clamp(_config.PineShadowStrength, 0f, 5f) * strength);
            SetFloat("TextureGuidedPineRimStrength", Math.Clamp(_config.PineRimStrength, 0f, 5f) * strength);
        }
    }

    private static void DrawCanopyWithEffect(
        object batch,
        object texture,
        object position,
        object sourceRect,
        object encodedColor,
        float rotation,
        object origin,
        object effects)
    {
        BatchState state = CaptureBatchState(batch);
        dynamic spriteBatch = batch;
        bool batchOpen = true;

        try
        {
            spriteBatch.End();
            batchOpen = false;

            spriteBatch.Begin(
                GetEnum("Microsoft.Xna.Framework.Graphics.SpriteSortMode", "Deferred"),
                GetStaticMember("Microsoft.Xna.Framework.Graphics.BlendState", "AlphaBlend"),
                GetStaticMember("Microsoft.Xna.Framework.Graphics.SamplerState", "PointClamp"),
                GetStaticMember("Microsoft.Xna.Framework.Graphics.DepthStencilState", "None"),
                GetStaticMember("Microsoft.Xna.Framework.Graphics.RasterizerState", "CullCounterClockwise"),
                _effect,
                state.TransformMatrix);
            batchOpen = true;

            dynamic drawTexture = texture;
            dynamic drawPosition = position;
            dynamic drawSourceRect = sourceRect;
            dynamic drawColor = encodedColor;
            dynamic drawOrigin = origin;
            dynamic spriteEffects = effects;
            spriteBatch.Draw(drawTexture, drawPosition, drawSourceRect, drawColor, rotation, drawOrigin, 4f, spriteEffects, 0f);

            spriteBatch.End();
            batchOpen = false;
            BeginRestoredBatch(spriteBatch, state);
            batchOpen = true;
        }
        catch
        {
            // Never leave the game's SpriteBatch closed or leave our effect attached to it.
            if (batchOpen)
            {
                try
                {
                    spriteBatch.End();
                    batchOpen = false;
                }
                catch
                {
                    // A failed End means the batch may already be closed; try restoring it below.
                    batchOpen = false;
                }
            }

            if (!batchOpen)
            {
                try
                {
                    BeginRestoredBatch(spriteBatch, state);
                }
                catch (Exception restoreException)
                {
                    _monitor?.Log("Couldn't restore the game's SpriteBatch after canopy rendering: " + restoreException.Message, LogLevel.Error);
                }
            }
            throw;
        }
    }

    private sealed class BatchState
    {
        public object SortMode { get; init; } = GetEnum("Microsoft.Xna.Framework.Graphics.SpriteSortMode", "Deferred");
        public object BlendState { get; init; } = GetStaticMember("Microsoft.Xna.Framework.Graphics.BlendState", "AlphaBlend");
        public object SamplerState { get; init; } = GetStaticMember("Microsoft.Xna.Framework.Graphics.SamplerState", "PointClamp");
        public object DepthStencilState { get; init; } = GetStaticMember("Microsoft.Xna.Framework.Graphics.DepthStencilState", "None");
        public object RasterizerState { get; init; } = GetStaticMember("Microsoft.Xna.Framework.Graphics.RasterizerState", "CullCounterClockwise");
        public object? Effect { get; init; }
        public object TransformMatrix { get; init; } = GetStaticMember("Microsoft.Xna.Framework.Matrix", "Identity");
    }

    private static BatchState CaptureBatchState(object batch)
    {
        Type type = batch.GetType();
        object sortMode = ReadFirstField(batch, "_sortMode", "sortMode")
            ?? GetEnum("Microsoft.Xna.Framework.Graphics.SpriteSortMode", "Deferred");
        object blend = ReadFirstField(batch, "_blendState", "blendState")
            ?? GetStaticMember("Microsoft.Xna.Framework.Graphics.BlendState", "AlphaBlend");
        object sampler = ReadFirstField(batch, "_samplerState", "samplerState")
            ?? GetStaticMember("Microsoft.Xna.Framework.Graphics.SamplerState", "PointClamp");
        object depth = ReadFirstField(batch, "_depthStencilState", "depthStencilState")
            ?? GetStaticMember("Microsoft.Xna.Framework.Graphics.DepthStencilState", "None");
        object rasterizer = ReadFirstField(batch, "_rasterizerState", "rasterizerState")
            ?? GetStaticMember("Microsoft.Xna.Framework.Graphics.RasterizerState", "CullCounterClockwise");
        object? effect = ReadFirstField(batch, "_customEffect", "_effect", "customEffect");
        object transform = ReadFirstField(batch, "_transformMatrix", "transformMatrix")
            ?? GetStaticMember("Microsoft.Xna.Framework.Matrix", "Identity");

        object? begun = ReadFirstField(batch, "_beginCalled", "beginCalled");
        if (begun is not null && !Convert.ToBoolean(begun))
            throw new InvalidOperationException("Tree.draw ran without an active SpriteBatch.Begin.");

        return new BatchState
        {
            SortMode = sortMode,
            BlendState = blend,
            SamplerState = sampler,
            DepthStencilState = depth,
            RasterizerState = rasterizer,
            Effect = effect,
            TransformMatrix = transform
        };
    }

    private static void BeginRestoredBatch(dynamic spriteBatch, BatchState state)
    {
        spriteBatch.Begin(
            state.SortMode,
            state.BlendState,
            state.SamplerState,
            state.DepthStencilState,
            state.RasterizerState,
            state.Effect,
            state.TransformMatrix);
    }

    private static object? ReadFirstField(object instance, params string[] names)
    {
        for (Type? type = instance.GetType(); type is not null; type = type.BaseType)
        {
            foreach (string name in names)
            {
                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field is not null)
                    return field.GetValue(instance);
            }
        }
        return null;
    }

    private static void SetCurrentTechnique(string name)
    {
        object? techniques = ReadMember(_effect!, "Techniques");
        if (techniques is null)
            throw new InvalidOperationException("The bundled Effect has no technique collection.");

        PropertyInfo? indexer = techniques.GetType().GetProperties()
            .FirstOrDefault(property =>
            {
                ParameterInfo[] indexParameters = property.GetIndexParameters();
                return property.Name == "Item"
                    && indexParameters.Length == 1
                    && indexParameters[0].ParameterType == typeof(string);
            });

        object? technique = indexer?.GetValue(techniques, new object[] { name });
        if (technique is null)
            throw new InvalidOperationException("The bundled Effect is missing technique " + name + ".");

        PropertyInfo? currentTechnique = AccessTools.Property(_effect!.GetType(), "CurrentTechnique");
        if (currentTechnique?.CanWrite != true)
            throw new InvalidOperationException("Couldn't change the current canopy shader technique.");
        currentTechnique.SetValue(_effect, technique);
    }

    private static void SetVector4(string name, float x, float y, float z, float w)
    {
        Type vectorType = AccessTools.TypeByName("Microsoft.Xna.Framework.Vector4")
            ?? throw new InvalidOperationException("Couldn't find MonoGame's Vector4 type.");
        object value = Activator.CreateInstance(vectorType, new object[] { x, y, z, w })!;
        SetEffectParameter(name, value);
    }

    private static void SetFloat(string name, float value) => SetEffectParameter(name, value);

    private static void SetEffectParameter(string name, object value)
    {
        object? parameters = ReadMember(_effect!, "Parameters");
        if (parameters is null)
            throw new InvalidOperationException("The bundled Effect has no parameter collection.");

        PropertyInfo? indexer = parameters.GetType().GetProperties()
            .FirstOrDefault(property =>
            {
                ParameterInfo[] indexParameters = property.GetIndexParameters();
                return property.Name == "Item"
                    && indexParameters.Length == 1
                    && indexParameters[0].ParameterType == typeof(string);
            });

        object? parameter = indexer?.GetValue(parameters, new object[] { name });
        if (parameter is null)
            throw new InvalidOperationException("The bundled Effect is missing parameter " + name + ".");

        MethodInfo? setValue = parameter.GetType().GetMethods()
            .FirstOrDefault(method =>
            {
                if (method.Name != "SetValue")
                    return false;
                ParameterInfo[] parametersForMethod = method.GetParameters();
                return parametersForMethod.Length == 1 && parametersForMethod[0].ParameterType.IsInstanceOfType(value);
            });

        if (setValue is null)
            throw new InvalidOperationException("Couldn't set shader parameter " + name + ".");
        setValue.Invoke(parameter, new[] { value });
    }

    private static object GetEnum(string typeName, string value)
    {
        Type type = AccessTools.TypeByName(typeName)
            ?? throw new InvalidOperationException("Couldn't find runtime type " + typeName + ".");
        return Enum.Parse(type, value);
    }

    private static object GetStaticMember(string typeName, string memberName)
    {
        Type type = AccessTools.TypeByName(typeName)
            ?? throw new InvalidOperationException("Couldn't find runtime type " + typeName + ".");
        PropertyInfo? property = AccessTools.Property(type, memberName);
        if (property is not null)
            return property.GetValue(null)!;
        FieldInfo? field = AccessTools.Field(type, memberName);
        return field?.GetValue(null)
            ?? throw new InvalidOperationException("Couldn't find static member " + typeName + "." + memberName + ".");
    }

    private static object? ReadStaticMember(Type type, string name)
    {
        return AccessTools.Property(type, name)?.GetValue(null)
            ?? AccessTools.Field(type, name)?.GetValue(null);
    }

    private static object? ReadNetValue(object instance, FieldInfo? field, object fallback)
    {
        if (field?.GetValue(instance) is not object netField)
            return fallback;
        return ReadMember(netField, "Value") ?? fallback;
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

    private static int ToChannel(float normalized) =>
        Math.Clamp((int)Math.Round(Math.Clamp(normalized, 0f, 1f) * 255f), 0, 255);

    private static float GetDaylightFactor(float hour)
    {
        // Begin gradually at dawn so the effect is already visible at the game's 6:00 start.
        float sunrise = SmoothStep(5.5f, 7.0f, hour);
        float sunset = 1f - SmoothStep(16.5f, 18.2f, hour);
        return Math.Clamp(sunrise * sunset, 0f, 1f);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
