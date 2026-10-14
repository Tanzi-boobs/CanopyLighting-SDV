using System.Collections;
using System.Reflection;
using StardewModdingAPI;

namespace DoubleTapHUD;

public sealed class ModEntry : Mod
{
    private static ModEntry? _instance;
    private object? _harmony;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private ModConfig _config = new();
    private ClickTarget _pendingTarget;
    private long _pendingAt;
    private int _pendingX;
    private int _pendingY;
    private object? _pendingIdentity;
    private object? _resolvedIdentity;

    private enum ClickTarget { None, Clock, QuestButton, MapTab, CraftingTab, CraftingRecipe, MapBlank }

    public override void Entry(IModHelper helper)
    {
        _instance = this;
        _config = helper.ReadConfig<ModConfig>();
        _config.DoubleTapWindowMilliseconds = Math.Clamp(_config.DoubleTapWindowMilliseconds, 180, 900);
        _config.MaxTapDistance = Math.Clamp(_config.MaxTapDistance, 16, 160);
        helper.WriteConfig(_config);

        InstallClickPatch();
        Monitor.Log($"Double Tap HUD ready. Window={_config.DoubleTapWindowMilliseconds}ms, distance={_config.MaxTapDistance}px.", LogLevel.Info);
    }

    private void InstallClickPatch()
    {
        try
        {
            Type gameType = FindType("StardewValley.Game1")
                ?? throw new TypeLoadException("StardewValley.Game1 was not found.");
            Type harmonyType = FindType("HarmonyLib.Harmony")
                ?? throw new TypeLoadException("HarmonyLib.Harmony was not found in the loaded assemblies.");
            Type harmonyMethodType = harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false)
                ?? throw new TypeLoadException("HarmonyLib.HarmonyMethod was not found.");

            MethodInfo original = gameType.GetMethods(Flags).FirstOrDefault(m =>
            {
                ParameterInfo[] p = m.GetParameters();
                return m.IsStatic && m.Name == "pressLeftClick" && p.Length == 2
                    && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int);
            }) ?? throw new MissingMethodException("Game1.pressLeftClick(int, int) was not found.");

            MethodInfo prefixMethod = typeof(ModEntry).GetMethod(
                nameof(PressLeftClickPrefix), BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException("PressLeftClickPrefix was not found.");

            object harmony = Activator.CreateInstance(harmonyType, new object[] { "Tanzi.DoubleTapHUD" })
                ?? throw new InvalidOperationException("Couldn't create a Harmony instance.");
            object prefix = Activator.CreateInstance(harmonyMethodType, new object[] { prefixMethod })
                ?? throw new InvalidOperationException("Couldn't create a HarmonyMethod.");

            MethodInfo patch = harmonyType.GetMethods(Flags).FirstOrDefault(m =>
            {
                ParameterInfo[] p = m.GetParameters();
                return m.Name == "Patch" && p.Length == 5
                    && p[0].ParameterType == typeof(MethodBase)
                    && p[1].ParameterType == harmonyMethodType;
            }) ?? throw new MissingMethodException("The expected Harmony.Patch overload was not found.");

            patch.Invoke(harmony, new object?[] { original, prefix, null, null, null });
            _harmony = harmony;
            Monitor.Log("Installed click hook on Game1.pressLeftClick.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Exception cause = ex is TargetInvocationException tie && tie.InnerException is not null
                ? tie.InnerException : ex;
            Monitor.Log("Couldn't install the game-click hook: " + cause.GetType().Name + ": " + cause.Message, LogLevel.Error);
        }
    }

    private static bool PressLeftClickPrefix(int x, int y)
    {
        ModEntry? instance = _instance;
        if (instance is null)
            return true;

        try
        {
            return instance.ProcessLeftClick(x, y);
        }
        catch (Exception ex)
        {
            instance.ClearPendingTap();
            instance.Monitor.Log("Couldn't process tap: " + ex.GetType().Name + ": " + ex.Message, LogLevel.Warn);
            return true;
        }
    }

    private bool ProcessLeftClick(int x, int y)
    {
        ClickTarget target = ResolveTarget(x, y);
        object? identity = _resolvedIdentity;

        if (target == ClickTarget.None)
        {
            ClearPendingTap();
            return true;
        }

        bool secondTap = IsSecondTap(target, x, y, identity);

        // First recipe taps are blocked here. The second tap passes through to the
        // original Game1/vanilla/Better Crafting click pipeline.
        if (target == ClickTarget.CraftingRecipe)
        {
            if (!secondTap)
                return false;

            ClearPendingTap();
            return true;
        }

        // Delay these navigation actions until the second tap and prevent the
        // native handler from running on either tap.
        if (!secondTap)
            return false;

        ClearPendingTap();
        switch (target)
        {
            case ClickTarget.Clock: OpenGameMenu(); break;
            case ClickTarget.QuestButton: OpenQuestLog(); break;
            case ClickTarget.MapTab: OpenMenuTab("map"); break;
            case ClickTarget.CraftingTab: OpenMenuTab("crafting"); break;
            case ClickTarget.MapBlank: CloseMapTab(); break;
        }
        return false;
    }

    private ClickTarget ResolveTarget(int x, int y)
    {
        _resolvedIdentity = null;
        Type? game = FindType("StardewValley.Game1");
        if (game is null)
            return ClickTarget.None;

        object? activeMenu = GetStatic(game, "activeClickableMenu");
        if (activeMenu is not null)
        {
            if (_config.DoubleTapCraftingRecipes
                && TryGetCraftingPage(activeMenu, out object? craftingPage)
                && IsRecipeButtonAt(craftingPage!, x, y, out object? recipeButton))
            {
                _resolvedIdentity = recipeButton;
                return ClickTarget.CraftingRecipe;
            }

            if (activeMenu.GetType().Name == "GameMenu")
            {
                object? page = InvokeNoArg(activeMenu, "GetCurrentPage");
                if (page?.GetType().Name == "MapPage")
                {
                    // On the map page only blank space gets the double-tap behavior.
                    // Location markers, all menu tabs, and the close button remain native single taps.
                    if (Contains(GetMember(activeMenu, "upperRightCloseButton"), x, y)
                        || IsAnyTabAt(activeMenu, x, y)
                        || IsMapLocationAt(page, x, y))
                        return ClickTarget.None;

                    return _config.DoubleTapBlankMapToClose ? ClickTarget.MapBlank : ClickTarget.None;
                }

                if (_config.DoubleTapMapTab && TryGetTabAt(activeMenu, "map", x, y, out object? mapTab))
                {
                    _resolvedIdentity = mapTab;
                    return ClickTarget.MapTab;
                }

                if (_config.DoubleTapCraftingTab && TryGetTabAt(activeMenu, "crafting", x, y, out object? craftingTab))
                {
                    _resolvedIdentity = craftingTab;
                    return ClickTarget.CraftingTab;
                }
            }

            return ClickTarget.None;
        }

        object? player = GetStatic(game, "player");
        object? dayBox = GetStatic(game, "dayTimeMoneyBox");
        if (player is null || dayBox is null)
            return ClickTarget.None;

        if (_config.DoubleTapQuestButton && ReadInt(player, "visibleQuestCount", 0) > 0
            && Contains(GetMember(dayBox, "questButton"), x, y))
        {
            _resolvedIdentity = GetMember(dayBox, "questButton");
            return ClickTarget.QuestButton;
        }

        if (_config.ClockOpensGameMenu && IsClockPanelAt(game, dayBox, x, y))
            return ClickTarget.Clock;

        return ClickTarget.None;
    }

    private bool IsSecondTap(ClickTarget target, int x, int y, object? identity)
    {
        long now = Environment.TickCount64;
        bool sameIdentity = identity is null ? _pendingIdentity is null : ReferenceEquals(identity, _pendingIdentity);
        bool match = target == _pendingTarget && sameIdentity
            && now - _pendingAt <= _config.DoubleTapWindowMilliseconds
            && Math.Abs(x - _pendingX) <= _config.MaxTapDistance
            && Math.Abs(y - _pendingY) <= _config.MaxTapDistance;

        if (!match)
        {
            _pendingTarget = target;
            _pendingAt = now;
            _pendingX = x;
            _pendingY = y;
            _pendingIdentity = identity;
            return false;
        }

        return true;
    }

    private void ClearPendingTap()
    {
        _pendingTarget = ClickTarget.None;
        _pendingAt = 0;
        _pendingX = _pendingY = 0;
        _pendingIdentity = _resolvedIdentity = null;
    }

    private static bool IsClockPanelAt(Type game, object dayBox, int x, int y)
    {
        int left = ReadInt(dayBox, "xPositionOnScreen", -1);
        int top = ReadInt(dayBox, "yPositionOnScreen", -1);
        if (left < 0 || top < 0)
            return false;

        int viewportWidth = ReadInt(GetStatic(game, "uiViewport"), "Width", 0);
        int right = viewportWidth > 0 ? Math.Min(left + 286, viewportWidth) : left + 250;
        return x >= left + 8 && x <= right && y >= top + 8 && y <= top + 155;
    }

    private static bool TryGetTabAt(object menu, string tabName, int x, int y, out object? tabFound)
    {
        tabFound = null;
        if (GetMember(menu, "tabs") is not IEnumerable tabs)
            return false;

        foreach (object? tab in tabs)
        {
            if (tab is null)
                continue;
            if (string.Equals(Convert.ToString(GetMember(tab, "name")), tabName, StringComparison.OrdinalIgnoreCase)
                && Contains(tab, x, y))
            {
                tabFound = tab;
                return true;
            }
        }
        return false;
    }

    private static bool IsAnyTabAt(object menu, int x, int y)
    {
        if (GetMember(menu, "tabs") is not IEnumerable tabs)
            return false;
        foreach (object? tab in tabs)
            if (tab is not null && Contains(tab, x, y))
                return true;
        return false;
    }

    private static bool IsMapLocationAt(object page, int x, int y)
    {
        if (GetMember(page, "points") is not IEnumerable points)
            return false;
        foreach (object? point in points)
            if (point is not null && Contains(point, x, y))
                return true;
        return false;
    }

    private static bool TryGetCraftingPage(object activeMenu, out object? page)
    {
        page = null;
        if (IsCraftingPageType(activeMenu.GetType()))
        {
            page = activeMenu;
            return true;
        }

        if (activeMenu.GetType().Name == "GameMenu")
        {
            object? current = InvokeNoArg(activeMenu, "GetCurrentPage");
            if (current is not null && IsCraftingPageType(current.GetType()))
            {
                page = current;
                return true;
            }
        }
        return false;
    }

    private static bool IsCraftingPageType(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
            if (current.Name is "CraftingPage" or "BetterCraftingPage")
                return true;
        return false;
    }

    private static bool IsRecipeButtonAt(object page, int x, int y, out object? button)
    {
        button = null;

        // Vanilla active recipe dictionary.
        if (GetMember(page, "pagesOfCraftingRecipes") is IList pages && pages.Count > 0)
        {
            int index = ReadInt(page, "currentCraftingPage", 0);
            if (index >= 0 && index < pages.Count && pages[index] is IDictionary recipes)
            {
                foreach (DictionaryEntry entry in recipes)
                    if (entry.Key is not null && Contains(entry.Key, x, y))
                    {
                        button = entry.Key;
                        return true;
                    }
            }
        }

        // Better Crafting keeps explicit mappings between recipe models and their live buttons.
        // Reflection avoids a hard dependency and preserves its original click handlers.
        if (GetMember(page, "ComponentRecipes") is IDictionary components)
            foreach (DictionaryEntry entry in components)
                if (entry.Key is not null && Contains(entry.Key, x, y))
                {
                    button = entry.Key;
                    return true;
                }

        if (GetMember(page, "RecipeComponents") is IDictionary reverse)
            foreach (DictionaryEntry entry in reverse)
                if (entry.Value is not null && Contains(entry.Value, x, y))
                {
                    button = entry.Value;
                    return true;
                }

        // Vanilla's currentPageClickableComponents is recipe-focused. Better Crafting's
        // page-component collection also includes search, category, and filter controls.
        if (page.GetType().Name == "CraftingPage" && GetMember(page, "currentPageClickableComponents") is IEnumerable componentsOnPage)
            foreach (object? component in componentsOnPage)
                if (component is not null && Contains(component, x, y))
                {
                    button = component;
                    return true;
                }

        return false;
    }

    private void OpenGameMenu()
    {
        Type? game = FindType("StardewValley.Game1");
        Type? menuType = FindType("StardewValley.Menus.GameMenu");
        if (game is null || menuType is null)
            throw new InvalidOperationException("GameMenu types were not found.");
        if (GetStatic(game, "activeClickableMenu") is not null)
            return;
        SetStatic(game, "activeClickableMenu", CreateMenu(menuType));
        Monitor.Log("Double-tapped the clock panel; opened Inventory.", LogLevel.Trace);
    }

    private void OpenQuestLog()
    {
        Type? game = FindType("StardewValley.Game1");
        Type? questType = FindType("StardewValley.Menus.QuestLog");
        if (game is null || questType is null)
            throw new InvalidOperationException("QuestLog types were not found.");

        object? player = GetStatic(game, "player");
        if (player is null || !ReadBool(player, "CanMove", true)
            || ReadStaticBool(game, "dialogueUp", false) || ReadStaticBool(game, "eventUp", false)
            || GetStatic(game, "farmEvent") is not null)
            return;

        SetStatic(game, "activeClickableMenu", CreateMenu(questType));
        Monitor.Log("Double-tapped the quest icon; opened Quest Log.", LogLevel.Trace);
    }

    private void OpenMenuTab(string tabName)
    {
        Type? game = FindType("StardewValley.Game1");
        object? menu = game is null ? null : GetStatic(game, "activeClickableMenu");
        if (menu is null || menu.GetType().Name != "GameMenu")
            return;

        int targetIndex = -1;
        if (GetMember(menu, "tabs") is IEnumerable tabs)
        {
            int index = 0;
            foreach (object? tab in tabs)
            {
                if (tab is not null && string.Equals(Convert.ToString(GetMember(tab, "name")), tabName, StringComparison.OrdinalIgnoreCase))
                {
                    targetIndex = index;
                    break;
                }
                index++;
            }
        }
        if (targetIndex < 0)
            throw new MissingMemberException("Tab not found: " + tabName);

        object? current = InvokeNoArg(menu, "GetCurrentPage");
        if (tabName == "map" && current?.GetType().Name == "MapPage")
            return;
        if (tabName == "crafting" && current is not null && IsCraftingPageType(current.GetType()))
            return;

        MethodInfo? change = menu.GetType().GetMethods(Flags).FirstOrDefault(m =>
            m.Name == "changeTab" && m.GetParameters().Length == 2
            && m.GetParameters()[0].ParameterType == typeof(int)
            && m.GetParameters()[1].ParameterType == typeof(bool));
        if (change is null)
            throw new MissingMethodException("GameMenu.changeTab(int, bool) was not found.");

        change.Invoke(menu, new object[] { targetIndex, true });
        Monitor.Log("Double-tapped the " + tabName + " tab.", LogLevel.Trace);
    }

    private void CloseMapTab()
    {
        Type? game = FindType("StardewValley.Game1");
        object? menu = game is null ? null : GetStatic(game, "activeClickableMenu");
        if (menu is not null && menu.GetType().Name == "GameMenu")
        {
            int previousTab = ReadInt(menu, "lastOpenedNonMapTab", 0);
            MethodInfo? change = menu.GetType().GetMethods(Flags).FirstOrDefault(m =>
                m.Name == "changeTab" && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == typeof(int)
                && m.GetParameters()[1].ParameterType == typeof(bool));
            if (change is not null)
            {
                change.Invoke(menu, new object[] { previousTab, true });
                Monitor.Log("Double-tapped blank map space; returned to previous tab.", LogLevel.Trace);
                return;
            }
        }

        if (game is null)
            throw new InvalidOperationException("Game1 was not found.");
        MethodInfo? exit = game.GetMethods(Flags).FirstOrDefault(m => m.IsStatic && m.Name == "exitActiveMenu" && m.GetParameters().Length == 0);
        if (exit is not null)
            exit.Invoke(null, null);
        else
            SetStatic(game, "activeClickableMenu", null);
    }

    private static object CreateMenu(Type type)
    {
        ConstructorInfo? empty = type.GetConstructor(Flags, null, Type.EmptyTypes, null);
        if (empty is not null)
            return empty.Invoke(null);
        ConstructorInfo? boolCtor = type.GetConstructors(Flags).FirstOrDefault(c =>
        {
            ParameterInfo[] p = c.GetParameters();
            return p.Length == 1 && p[0].ParameterType == typeof(bool);
        });
        if (boolCtor is not null)
            return boolCtor.Invoke(new object[] { true });
        throw new MissingMethodException("No supported constructor for " + type.FullName);
    }

    private static bool Contains(object? component, int x, int y)
    {
        if (component is null)
            return false;
        object? bounds = GetMember(component, "bounds") ?? GetMember(component, "Bounds");
        if (bounds is null)
            return false;
        int left = ReadInt(bounds, "X", int.MinValue);
        int top = ReadInt(bounds, "Y", int.MinValue);
        int width = ReadInt(bounds, "Width", 0);
        int height = ReadInt(bounds, "Height", 0);
        return width > 0 && height > 0 && x >= left && x < left + width && y >= top && y < top + height;
    }

    private static (int X, int Y) GetMousePosition()
    {
        Type? game = FindType("StardewValley.Game1");
        if (game is null)
            throw new InvalidOperationException("Game1 was not found.");
        return (ReadStaticIntMethod(game, "getMouseX"), ReadStaticIntMethod(game, "getMouseY"));
    }

    private static int ReadStaticIntMethod(Type type, string name)
    {
        MethodInfo? method = type.GetMethods(Flags).FirstOrDefault(m => m.IsStatic && m.Name == name && m.GetParameters().Length == 0);
        if (method is null)
            throw new MissingMethodException(type.FullName, name);
        return Convert.ToInt32(method.Invoke(null, null));
    }

    private static Type? FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = assembly.GetType(fullName, false, false);
            if (type is not null)
                return type;
        }
        return null;
    }

    private static object? GetStatic(Type type, string name) => GetMember(type, null, name);
    private static object? GetMember(object instance, string name) => GetMember(instance.GetType(), instance, name);

    private static object? GetMember(Type type, object? instance, string name)
    {
        bool wantStatic = instance is null;
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            PropertyInfo? property = current.GetProperties(Flags | BindingFlags.DeclaredOnly).FirstOrDefault(p =>
                p.Name == name && p.GetIndexParameters().Length == 0 && p.GetMethod is not null && p.GetMethod.IsStatic == wantStatic);
            if (property is not null)
            {
                try { return property.GetValue(instance); } catch { return null; }
            }

            FieldInfo? field = current.GetFields(Flags | BindingFlags.DeclaredOnly).FirstOrDefault(f => f.Name == name && f.IsStatic == wantStatic);
            if (field is not null)
            {
                try { return field.GetValue(instance); } catch { return null; }
            }
        }
        return null;
    }

    private static void SetStatic(Type type, string name, object? value)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            PropertyInfo? property = current.GetProperties(Flags | BindingFlags.DeclaredOnly).FirstOrDefault(p =>
                p.Name == name && p.GetIndexParameters().Length == 0 && p.SetMethod?.IsStatic == true);
            if (property is not null) { property.SetValue(null, value); return; }

            FieldInfo? field = current.GetFields(Flags | BindingFlags.DeclaredOnly).FirstOrDefault(f => f.Name == name && f.IsStatic);
            if (field is not null) { field.SetValue(null, value); return; }
        }
        throw new MissingMemberException(type.FullName, name);
    }

    private static object? InvokeNoArg(object instance, string name)
    {
        MethodInfo? method = instance.GetType().GetMethods(Flags).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == 0);
        try { return method?.Invoke(instance, null); } catch { return null; }
    }

    private static int ReadInt(object? instance, string name, int fallback)
    {
        try { object? value = instance is null ? null : GetMember(instance, name); return value is null ? fallback : Convert.ToInt32(value); }
        catch { return fallback; }
    }

    private static bool ReadStaticBool(Type type, string name, bool fallback)
    {
        try { object? value = GetStatic(type, name); return value is null ? fallback : Convert.ToBoolean(value); }
        catch { return fallback; }
    }

    private static bool ReadBool(object? instance, string name, bool fallback)
    {
        try { object? value = instance is null ? null : GetMember(instance, name); return value is null ? fallback : Convert.ToBoolean(value); }
        catch { return fallback; }
    }
}