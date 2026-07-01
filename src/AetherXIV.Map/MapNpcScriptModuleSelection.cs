using AetherXIV.Scripting;

namespace AetherXIV.Map;

public sealed record MapNpcScriptModuleSelection(
    ScriptModuleId? ModuleId,
    IReadOnlyList<ScriptModuleId> Candidates)
{
    public bool Found => ModuleId is not null;
}

public interface IMapNpcScriptModuleSelector
{
    MapNpcScriptModuleSelection SelectModule(MapNpcScriptDescriptor descriptor, string functionName);
}

public sealed class DefaultMapNpcScriptModuleSelector : IMapNpcScriptModuleSelector
{
    public static DefaultMapNpcScriptModuleSelector Instance { get; } = new();

    private DefaultMapNpcScriptModuleSelector()
    {
    }

    public MapNpcScriptModuleSelection SelectModule(MapNpcScriptDescriptor descriptor, string functionName)
    {
        ScriptModuleId module = descriptor.HasPrivateArea
            ? MapScriptModuleIds.PrivateAreaNpc(descriptor)
            : MapScriptModuleIds.Npc(descriptor);
        return new MapNpcScriptModuleSelection(module, [module]);
    }
}

public sealed class MeteorFileSystemMapNpcScriptModuleSelector : IMapNpcScriptModuleSelector
{
    private readonly ILegacyScriptFileResolver files;
    private readonly IScriptFunctionProbe? functions;

    public MeteorFileSystemMapNpcScriptModuleSelector(
        ILegacyScriptFileResolver files,
        IScriptFunctionProbe? functions = null)
    {
        this.files = files;
        this.functions = functions;
    }

    public MapNpcScriptModuleSelection SelectModule(MapNpcScriptDescriptor descriptor, string functionName)
    {
        List<ScriptModuleId> candidates = new();

        ScriptModuleId unique = descriptor.HasPrivateArea
            ? MapScriptModuleIds.PrivateAreaNpc(descriptor)
            : MapScriptModuleIds.Npc(descriptor);
        candidates.Add(unique);

        if (CanRun(unique, functionName))
            return new MapNpcScriptModuleSelection(unique, candidates);

        if (!string.IsNullOrWhiteSpace(descriptor.ActorClassPath))
        {
            ScriptModuleId parent = MapScriptModuleIds.BaseNpc(descriptor);
            candidates.Add(parent);
            if (CanRun(parent, functionName))
                return new MapNpcScriptModuleSelection(parent, candidates);
        }

        return new MapNpcScriptModuleSelection(null, candidates);
    }

    private bool CanRun(ScriptModuleId moduleId, string functionName)
    {
        if (!Exists(moduleId))
            return false;

        return functions?.DefinesFunction(moduleId, functionName) ?? true;
    }

    private bool Exists(ScriptModuleId moduleId)
    {
        try
        {
            files.ResolveScriptPath(moduleId.Path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
