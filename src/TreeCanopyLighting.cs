using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>
/// Draws the standalone canopy shader technique at the same point in the world draw order
/// as each vanilla tree. The compiled effect is loaded from this mod's own assets;
/// Dynamic Shader is not a runtime dependency.
/// </summary>
internal static class TreeCanopyLighting
{
    private const string HarmonyId = "Tanzi.CanopyLighting";
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
    private static FieldInfo? _hasSeed;
    private static FieldInfo? _hasMoss;
    private static FieldInfo? _wasShakenToday;
    private static FieldInfo? _stump;
    private static MethodInfo? _tryGetTreeData;
    private static FieldInfo? _alpha;
    private static FieldInfo? _shakeRotation;
    private static FieldInfo? _viewport;
    private static FieldInfo? _timeOfDay;

    private static string? _shaderPath;
    private static object? _effect;
    private static bool _effectInitializationAttempted;
    private static bool _runtimeDisabled;
    private static bool _loggedRuntimeFailure;
    private static bool _loggedBindingFailure;
    private static bool _loggedHookExecution;
    private static bool _loggedMatureCandidate;
    private static bool _loggedSuccessfulRender;
    private static int _diagnosticTreeSamples;

    // Cache reflection metadata used inside the per-tree render hook. Looking up methods,
    // shader parameter indexers, and SetValue overloads for every tree every frame is costly
    // on mobile runtimes.
    private static MethodInfo? _spriteBatchEndMethod;
    private static MethodInfo? _spriteBatchBeginMethod;
    private static MethodInfo? _spriteBatchDrawMethod;
    private static object? _effectParametersCollection;
    private static object? _effectTechniquesCollection;
    private static PropertyInfo? _effectParameterIndexer;
    private static PropertyInfo? _effectTechniqueIndexer;
    private static PropertyInfo? _effectCurrentTechniqueProperty;
    private static readonly System.Collections.Generic.Dictionary<string, object> EffectParameters = new(StringComparer.Ordinal);
    private static readonly System.Collections.Generic.Dictionary<string, MethodInfo> EffectParameterSetters = new(StringComparer.Ordinal);
    private static readonly System.Collections.Generic.Dictionary<string, object> EffectTechniques = new(StringComparer.Ordinal);
    private static readonly System.Collections.Generic.List<CanopyDrawCommand> QueuedCanopies = new();
    private static object? _queuedSpriteBatch;
    private sealed record CanopyDrawCommand(
        object Texture, object Position, object SourceRect, object EncodedColor,
        float Rotation, object Origin, object Effects, bool IsPine);

    internal static void UpdateConfig(ModConfig config)
    {
        _config = config;
    }

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
        _hasSeed = AccessTools.Field(_treeType, "hasSeed");
        _hasMoss = AccessTools.Field(_treeType, "hasMoss");
        _wasShakenToday = AccessTools.Field(_treeType, "wasShakenToday");
        _tryGetTreeData = _treeType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
            {
                if (method.Name != "TryGetData")
                    return false;

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2
                    && parameters[0].ParameterType == typeof(string)
                    && parameters[1].ParameterType.IsByRef;
            });
        _stump = AccessTools.Field(_treeType, "stump");
        _alpha = AccessTools.Field(_treeType, "alpha");
        _shakeRotation = AccessTools.Field(_treeType, "shakeRotation");
        _viewport = AccessTools.Field(_gameType, "viewport");
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
            throw new InvalidOperationException("Couldn't find the one-argument Tree.draw(SpriteBatch) method.");

        MethodInfo postfix = typeof(TreeCanopyLighting).GetMethod(
            nameof(OnTreeDrawPostfix),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Couldn't find the canopy draw hook.");

        _harmony = new Harmony(HarmonyId);
        _harmony.Patch(drawMethod, postfix: new HarmonyMethod(postfix));

        monitor.Log("Found bundled canopy shader asset and installed the Tree.draw hook.", LogLevel.Info);
    }

    private static void OnTreeDrawPostfix(object __instance, object __0)
    {
        if (_runtimeDisabled || _config is null || !_config.Enabled || _config.OverallStrength <= 0.001f)
            return;

        if (!_loggedHookExecution)
        {
            _loggedHookExecution = true;
            _monitor?.Log("Tree.draw hook is executing.", LogLevel.Trace);
        }

        try
        {
            if (__0 is null)
            {
                _monitor?.Log("Canopy hook received a null SpriteBatch argument.", LogLevel.Warn);
                return;
            }

            if (_gameType is null || _viewport is null || _timeOfDay is null)
            {
                if (!_loggedBindingFailure)
                {
                    _loggedBindingFailure = true;
                    _monitor?.Log(
                        $"Canopy hook binding check failed: Game1={_gameType is not null}, viewport={_viewport is not null}, timeOfDay={_timeOfDay is not null}.",
                        LogLevel.Warn);
                }
                return;
            }

            // Do not gate canopy rendering on Game1.currentLocation: some Android/Cinderbox
            // builds expose that member differently, and Tree.draw itself already proves
            // that this tree is currently being rendered in the active scene.
            int growthStage = Convert.ToInt32(ReadNetValue(__instance, _growthStage, 0));
            bool isStump = Convert.ToBoolean(ReadNetValue(__instance, _stump, false));
            string treeKind = Convert.ToString(ReadNetValue(__instance, _treeKind, "1")) ?? "1";
            bool isPine = treeKind == "3";

            // The latest device log showed the hook running, but no mature-tree path.
            // Log a few samples so the next run identifies which filter is blocking all trees.
            if (_diagnosticTreeSamples < 12)
            {
                _diagnosticTreeSamples++;
                _monitor?.Log(
                    $"Tree draw sample #{_diagnosticTreeSamples}: growthStage={growthStage}, treeType={treeKind}, stump={isStump}.",
                    LogLevel.Trace);
            }

            if (growthStage < 5)
                return;
            if (isStump)
                return;

            if (!_loggedMatureCandidate)
            {
                _loggedMatureCandidate = true;
                _monitor?.Log($"Mature tree reached canopy pass (treeType={treeKind}, pine={isPine}).", LogLevel.Trace);
            }

            // Use the pine silhouette for pine trees; use the broadleaf canopy profile
            // for every other mature vanilla/custom Wild Tree type. This covers maple,
            // mahogany, palm, mushroom, mystic and seasonal variants too.
            if (isPine ? !_config.EnablePineCanopy : !_config.EnableOakCanopy)
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

            // Match Tree.draw's selected canopy tile, otherwise seed-ready/untouched
            // variants and mossy canopies would receive the lighting overlay from
            // the wrong atlas cell and appear unchanged or misaligned.
            if (TryGetTreeData(treeKind, out object? treeData))
            {
                bool hasSeed = Convert.ToBoolean(ReadNetValue(__instance, _hasSeed, false));
                bool hasMoss = Convert.ToBoolean(ReadNetValue(__instance, _hasMoss, false));
                bool wasShakenToday = Convert.ToBoolean(ReadNetValue(__instance, _wasShakenToday, false));
                bool useAlternateWhenSeedReady = ReadBooleanMember(treeData!, "UseAlternateSpriteWhenSeedReady", false);
                bool useAlternateWhenNotShaken = ReadBooleanMember(treeData!, "UseAlternateSpriteWhenNotShaken", false);

                sourceX = (useAlternateWhenSeedReady && hasSeed)
                    || (useAlternateWhenNotShaken && !wasShakenToday) ? 48 : 0;
                if (hasMoss)
                    sourceX = 96;
            }

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

            object? batch = __0;
            if (batch is null)
                return;

            // Queue the canopy data only. Ending/restarting SpriteBatch for every tree caused
            // severe CPU overhead on Android; the queued overlays are rendered together in
            // one batch per canopy technique at Display.RenderedWorld.
            _queuedSpriteBatch = batch;
            QueuedCanopies.Add(new CanopyDrawCommand(
                texture, position, sourceRect, encodedColor, rotation, origin, effects, isPine));

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

    private static bool TryGetTreeData(string treeId, out object? data)
    {
        data = null;
        if (_tryGetTreeData is null)
            return false;

        object?[] arguments = { treeId, null };
        bool found = Convert.ToBoolean(_tryGetTreeData.Invoke(null, arguments) ?? false);
        data = arguments[1];
        return found && data is not null;
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

        CacheEffectMetadata();

        // These color values never change during play. Set them once rather than invoking
        // reflection and allocating Vector4 values for every visible mature tree.
        SetVector4("TreeCanopyLightColor", 1.05f, 1.00f, 0.90f, 1f);
        SetVector4("TreeCanopyShadowColor", 0.78f, 0.84f, 0.92f, 1f);
        SetVector4("TextureGuidedPineLightColor", 1.05f, 1.00f, 0.92f, 1f);
        SetVector4("TextureGuidedPineShadowColor", 0.78f, 0.84f, 0.92f, 1f);
        SetVector4("TextureGuidedPineRimColor", 1.12f, 1.06f, 0.90f, 1f);
    }

    private static void SetCanopyParameters(bool isPine, float hour, float solar)
    {
        if (_effect is null || _config is null)
            throw new InvalidOperationException("The canopy shader has not been loaded.");

        float dayProgress = Math.Clamp((hour - 6f) / 12f, 0f, 1f);
        // Match the reference canopy look: morning light enters from screen-left,
        // then crosses overhead and moves to screen-right in the afternoon.
        float sunX = -MathF.Cos(dayProgress * MathF.PI);
        Type? vectorType = AccessTools.TypeByName("Microsoft.Xna.Framework.Vector2");
        if (vectorType is null)
            throw new InvalidOperationException("Couldn't find MonoGame's Vector2 type.");

        object sunDirection = Activator.CreateInstance(vectorType, new object[] { sunX, -0.55f })!;
        string technique = isPine ? "TextureGuidedPineCanopyLight" : "TreeCanopyLight";
        SetCurrentTechnique(technique);
        SetEffectParameter("TreeCanopyLightSunDirection", sunDirection);

        // Keep runtime multipliers inside a conservative range even when an older config.json
        // contains values from pre-tuning releases. In particular, broadleaf exposure is
        // exponential in the shader and can saturate whole canopies at high values.
        float strength = Math.Clamp(_config.OverallStrength, 0f, 1.25f) * solar;
        if (!isPine)
        {
            SetFloat("TreeCanopyLightStrength", Math.Clamp(_config.OakLightStrength, 0f, 1.75f) * strength);
            SetFloat("TreeCanopyShadowStrength", Math.Clamp(_config.OakShadowStrength, 0f, 3f) * strength);
        }
        else
        {
            SetFloat("TextureGuidedPineLightStrength", Math.Clamp(_config.PineLightStrength, 0f, 3f) * strength);
            SetFloat("TextureGuidedPineShadowStrength", Math.Clamp(_config.PineShadowStrength, 0f, 3f) * strength);
            SetFloat("TextureGuidedPineRimStrength", Math.Clamp(_config.PineRimStrength, 0f, 3f) * strength);
        }
    }

    internal static void RenderQueuedOverlays()
    {
        if (QueuedCanopies.Count == 0)
        {
            _queuedSpriteBatch = null;
            return;
        }

        CanopyDrawCommand[] pending = QueuedCanopies.ToArray();
        QueuedCanopies.Clear();
        object? batch = _queuedSpriteBatch;
        _queuedSpriteBatch = null;
        if (batch is null)
        {
            _monitor?.Log("Skipping queued canopy overlays because the SpriteBatch reference was unavailable.", LogLevel.Warn);
            return;
        }

        if (_runtimeDisabled || _config is null || !_config.Enabled || _config.OverallStrength <= 0.001f)
            return;

        int time = Convert.ToInt32(_timeOfDay?.GetValue(null) ?? 1200);
        float hour = time / 100f;
        float solar = GetDaylightFactor(hour);
        if (solar <= 0.001f)
            return;

        BatchState? state = null;
        bool batchOpen = false;
        try
        {
            EnsureEffectLoaded();
            state = CaptureBatchState(batch);
            batchOpen = true;
            object effectTransform = CreateEffectTransform(batch, state);

            // Flush the world's existing sprites once, then draw each shader technique
            // as a group. This avoids four SpriteBatch state transitions per mature tree.
            InvokeSpriteBatchEnd(batch);
            batchOpen = false;

            for (int group = 0; group < 2; group++)
            {
                bool pine = group == 1;
                bool hasCanopies = pending.Any(item => item.IsPine == pine);
                if (!hasCanopies)
                    continue;

                SetCanopyParameters(pine, hour, solar);
                SetEffectParameter("MatrixTransform", effectTransform);

                InvokeSpriteBatchBegin(
                    batch,
                    state.SortMode,
                    state.BlendState,
                    state.SamplerState,
                    state.DepthStencilState,
                    state.RasterizerState,
                    _effect,
                    state.TransformMatrix);
                batchOpen = true;

                foreach (CanopyDrawCommand item in pending)
                {
                    if (item.IsPine != pine)
                        continue;

                    InvokeSpriteBatchDraw(
                        batch, item.Texture, item.Position, item.SourceRect, item.EncodedColor,
                        item.Rotation, item.Origin, 4f, item.Effects, 0f);
                }

                InvokeSpriteBatchEnd(batch);
                batchOpen = false;
            }

            BeginRestoredBatch(batch, state);
            batchOpen = true;

            if (!_loggedSuccessfulRender)
            {
                _loggedSuccessfulRender = true;
                _monitor?.Log($"Canopy shader rendered its first batched overlay pass at {time:0000}; queued={pending.Length}.", LogLevel.Info);
            }
        }
        catch (Exception ex)
        {
            if (batchOpen)
            {
                try
                {
                    InvokeSpriteBatchEnd(batch);
                    batchOpen = false;
                }
                catch
                {
                    // The batch may already have been closed by a failed End invocation.
                }
            }

            if (!batchOpen && state is not null)
            {
                try
                {
                    BeginRestoredBatch(batch, state);
                    batchOpen = true;
                }
                catch (Exception restoreException)
                {
                    _monitor?.Log(
                        "Couldn't restore SpriteBatch after batched canopy rendering: " +
                        restoreException.GetType().Name + ": " + restoreException.Message,
                        LogLevel.Error);
                }
            }

            _runtimeDisabled = true;
            if (!_loggedRuntimeFailure)
            {
                _loggedRuntimeFailure = true;
                _monitor?.Log(
                    "Batched canopy rendering disabled after an error: " + ex.GetType().Name + ": " + ex.Message,
                    LogLevel.Warn);
            }
        }
    }

    private static void InvokeSpriteBatchEnd(object batch)
    {
        MethodInfo? method = _spriteBatchEndMethod;
        if (method is null)
        {
            method = batch.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate => candidate.Name == "End" && candidate.GetParameters().Length == 0);

            _spriteBatchEndMethod = method;
        }

        if (method is null)
            throw new MissingMethodException("Couldn't find SpriteBatch.End().");

        InvokeRenderMethod(method, batch, Array.Empty<object?>(), "SpriteBatch.End");
    }

    private static void InvokeSpriteBatchBegin(
        object batch,
        object sortMode,
        object blendState,
        object samplerState,
        object depthStencilState,
        object rasterizerState,
        object? effect,
        object? transformMatrix)
    {
        Type matrixType = AccessTools.TypeByName("Microsoft.Xna.Framework.Matrix")
            ?? throw new InvalidOperationException("Couldn't find MonoGame's Matrix type.");

        MethodInfo? method = _spriteBatchBeginMethod;
        if (method is null)
        {
            method = batch.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.Name == "Begin" && candidate.GetParameters().Length == 7)
                .FirstOrDefault(candidate =>
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters[0].ParameterType.IsInstanceOfType(sortMode)
                        && parameters[1].ParameterType.IsInstanceOfType(blendState)
                        && parameters[2].ParameterType.IsInstanceOfType(samplerState)
                        && parameters[3].ParameterType.IsInstanceOfType(depthStencilState)
                        && parameters[4].ParameterType.IsInstanceOfType(rasterizerState)
                        && (effect is null || parameters[5].ParameterType.IsInstanceOfType(effect))
                        && (parameters[6].ParameterType == matrixType
                            || Nullable.GetUnderlyingType(parameters[6].ParameterType) == matrixType);
                });
            _spriteBatchBeginMethod = method;
        }

        if (method is null)
            throw new MissingMethodException("Couldn't find compatible seven-argument SpriteBatch.Begin(..., Effect, Matrix?).");

        InvokeRenderMethod(
            method,
            batch,
            new[] { sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect, transformMatrix },
            "SpriteBatch.Begin");
    }

    private static void InvokeSpriteBatchDraw(
        object batch,
        object texture,
        object position,
        object sourceRect,
        object color,
        float rotation,
        object origin,
        float scale,
        object effects,
        float layerDepth)
    {
        Type? rectangleType = AccessTools.TypeByName("Microsoft.Xna.Framework.Rectangle");
        Type? vectorType = AccessTools.TypeByName("Microsoft.Xna.Framework.Vector2");
        Type? colorType = AccessTools.TypeByName("Microsoft.Xna.Framework.Color");
        Type? effectsType = AccessTools.TypeByName("Microsoft.Xna.Framework.Graphics.SpriteEffects");
        if (rectangleType is null || vectorType is null || colorType is null || effectsType is null)
            throw new InvalidOperationException("Couldn't resolve MonoGame drawing types.");

        MethodInfo? method = _spriteBatchDrawMethod;
        if (method is null)
        {
            method = batch.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.Name == "Draw" && candidate.GetParameters().Length == 9)
                .FirstOrDefault(candidate =>
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    Type? nullableSource = Nullable.GetUnderlyingType(parameters[2].ParameterType);
                    return parameters[0].ParameterType.IsInstanceOfType(texture)
                        && parameters[1].ParameterType == vectorType
                        && (parameters[2].ParameterType == rectangleType || nullableSource == rectangleType)
                        && parameters[3].ParameterType == colorType
                        && parameters[4].ParameterType == typeof(float)
                        && parameters[5].ParameterType == vectorType
                        && parameters[6].ParameterType == typeof(float)
                        && parameters[7].ParameterType == effectsType
                        && parameters[8].ParameterType == typeof(float);
                });
            _spriteBatchDrawMethod = method;
        }

        if (method is null)
            throw new MissingMethodException("Couldn't find the SpriteBatch.Draw(Texture2D, Vector2, Rectangle?, Color, float, Vector2, float, SpriteEffects, float) overload.");

        InvokeRenderMethod(
            method,
            batch,
            new object?[] { texture, position, sourceRect, color, rotation, origin, scale, effects, layerDepth },
            "SpriteBatch.Draw canopy");
    }

    private static object? InvokeRenderMethod(MethodInfo method, object instance, object?[] arguments, string label)
    {
        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(label + " failed: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message, ex.InnerException);
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
        object? spriteEffect = ReadFirstField(batch, "_spriteEffect", "spriteEffect");
        object? transform = spriteEffect is null ? null : ReadMember(spriteEffect, "TransformMatrix");
        transform ??= ReadFirstField(batch, "_transformMatrix", "transformMatrix");
        transform ??= GetStaticMember("Microsoft.Xna.Framework.Matrix", "Identity");

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

    private static object CreateEffectTransform(object batch, BatchState state)
    {
        object? graphicsManager = _gameType is null ? null : ReadStaticMember(_gameType, "graphics");
        object? graphicsDevice = graphicsManager is null ? null : ReadMember(graphicsManager, "GraphicsDevice");
        object? viewport = graphicsDevice is null ? null : ReadMember(graphicsDevice, "Viewport");
        if (viewport is null)
            throw new InvalidOperationException("Couldn't read the GraphicsDevice viewport for the canopy shader.");

        float width = ReadFloatMember(viewport, "Width");
        float height = ReadFloatMember(viewport, "Height");
        if (width <= 0f || height <= 0f)
            throw new InvalidOperationException("The GraphicsDevice viewport has invalid dimensions.");

        Type matrixType = AccessTools.TypeByName("Microsoft.Xna.Framework.Matrix")
            ?? throw new InvalidOperationException("Couldn't find MonoGame's Matrix type.");
        MethodInfo createProjection = matrixType
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(method =>
            {
                if (method.Name != "CreateOrthographicOffCenter")
                    return false;

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 7
                    && parameters.Take(6).All(parameter => parameter.ParameterType == typeof(float))
                    && parameters[6].ParameterType == matrixType.MakeByRefType();
            })
            ?? throw new InvalidOperationException("Couldn't find Matrix.CreateOrthographicOffCenter.");

        object?[] projectionArguments = { 0f, width, height, 0f, 0f, -1f, null };
        createProjection.Invoke(null, projectionArguments);
        object projection = projectionArguments[6]
            ?? throw new InvalidOperationException("MonoGame failed to create the SpriteBatch projection matrix.");

        // Match MonoGame SpriteEffect.OnApply for backends which require a half-pixel offset.
        if (graphicsDevice is not null && ReadBooleanMember(graphicsDevice, "UseHalfPixelOffset", false))
        {
            FieldInfo? m41 = AccessTools.Field(matrixType, "M41");
            FieldInfo? m42 = AccessTools.Field(matrixType, "M42");
            if (m41 is not null)
                m41.SetValue(projection, ReadFloatMember(projection, "M41") - 0.5f * ReadFloatMember(projection, "M11"));
            if (m42 is not null)
                m42.SetValue(projection, ReadFloatMember(projection, "M42") - 0.5f * ReadFloatMember(projection, "M22"));
        }

        MethodInfo multiply = matrixType.GetMethod(
            "op_Multiply",
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            types: new[] { matrixType, matrixType },
            modifiers: null)
            ?? throw new InvalidOperationException("Couldn't find Matrix multiplication for the SpriteBatch transform.");

        return multiply.Invoke(null, new[] { state.TransformMatrix, projection })
            ?? throw new InvalidOperationException("Couldn't combine the SpriteBatch transform and projection.");
    }

    private static void BeginRestoredBatch(object spriteBatch, BatchState state)
    {
        InvokeSpriteBatchBegin(
            spriteBatch,
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
        if (!EffectTechniques.TryGetValue(name, out object? technique))
        {
            if (_effectTechniquesCollection is null || _effectTechniqueIndexer is null)
                throw new InvalidOperationException("The bundled Effect has no cached technique collection.");

            technique = _effectTechniqueIndexer.GetValue(_effectTechniquesCollection, new object[] { name });
            if (technique is null)
                throw new InvalidOperationException("The bundled Effect is missing technique " + name + ".");
            EffectTechniques[name] = technique;
        }

        if (_effectCurrentTechniqueProperty?.CanWrite != true)
            throw new InvalidOperationException("Couldn't change the current canopy shader technique.");
        _effectCurrentTechniqueProperty.SetValue(_effect, technique);
    }

    private static void SetVector4(string name, float x, float y, float z, float w)
    {
        Type vectorType = AccessTools.TypeByName("Microsoft.Xna.Framework.Vector4")
            ?? throw new InvalidOperationException("Couldn't find MonoGame's Vector4 type.");
        object value = Activator.CreateInstance(vectorType, new object[] { x, y, z, w })!;
        SetEffectParameter(name, value);
    }

    private static void SetFloat(string name, float value) => SetEffectParameter(name, value);

    private static void CacheEffectMetadata()
    {
        _effectParametersCollection = ReadMember(_effect!, "Parameters");
        _effectTechniquesCollection = ReadMember(_effect!, "Techniques");
        if (_effectParametersCollection is null || _effectTechniquesCollection is null)
            throw new InvalidOperationException("The bundled Effect has no parameter/technique collection.");

        _effectParameterIndexer = FindStringIndexer(_effectParametersCollection.GetType());
        _effectTechniqueIndexer = FindStringIndexer(_effectTechniquesCollection.GetType());
        _effectCurrentTechniqueProperty = AccessTools.Property(_effect!.GetType(), "CurrentTechnique");
        if (_effectParameterIndexer is null || _effectTechniqueIndexer is null || _effectCurrentTechniqueProperty is null)
            throw new InvalidOperationException("Couldn't cache the bundled Effect's reflection metadata.");
    }

    private static PropertyInfo? FindStringIndexer(Type type) =>
        type.GetProperties()
            .FirstOrDefault(property =>
            {
                ParameterInfo[] indexParameters = property.GetIndexParameters();
                return property.Name == "Item"
                    && indexParameters.Length == 1
                    && indexParameters[0].ParameterType == typeof(string);
            });

    private static void SetEffectParameter(string name, object value)
    {
        if (_effectParametersCollection is null || _effectParameterIndexer is null)
            throw new InvalidOperationException("The bundled Effect parameter collection wasn't initialized.");

        if (!EffectParameters.TryGetValue(name, out object? parameter))
        {
            parameter = _effectParameterIndexer.GetValue(_effectParametersCollection, new object[] { name });
            if (parameter is null)
                throw new InvalidOperationException("The bundled Effect is missing parameter " + name + ".");
            EffectParameters[name] = parameter;
        }

        if (!EffectParameterSetters.TryGetValue(name, out MethodInfo? setValue))
        {
            setValue = parameter.GetType().GetMethods()
                .FirstOrDefault(method =>
                {
                    if (method.Name != "SetValue")
                        return false;
                    ParameterInfo[] parametersForMethod = method.GetParameters();
                    return parametersForMethod.Length == 1 && parametersForMethod[0].ParameterType.IsInstanceOfType(value);
                });
            if (setValue is null)
                throw new InvalidOperationException("Couldn't set shader parameter " + name + ".");
            EffectParameterSetters[name] = setValue;
        }

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
