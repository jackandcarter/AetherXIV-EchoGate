using AetherXIV.Scripting;

namespace AetherXIV.Scripting.Tests;

public sealed class ScriptApiCatalogTests
{
    [Fact]
    public void BuiltInShapesCoverCurrentScriptRoles()
    {
        ScriptRole[] expected =
        [
            ScriptRole.Player,
            ScriptRole.Npc,
            ScriptRole.Director,
            ScriptRole.Command,
            ScriptRole.Quest,
            ScriptRole.Content,
            ScriptRole.Zone
        ];

        foreach (ScriptRole role in expected)
            Assert.Contains(ScriptApiCatalog.BuiltInShapes, shape => shape.Role == role);
    }

    [Fact]
    public void PlayerLifecycleShapeRequiresPlayerApi()
    {
        ScriptCallShape shape = Assert.Single(ScriptApiCatalog.BuiltInShapes, item => item.Role == ScriptRole.Player);

        Assert.Contains(shape.Bindings, binding =>
            binding.Kind == ScriptApiBindingKind.Argument
            && binding.Name == "player"
            && binding.ApiContract == nameof(IPlayerScriptApi)
            && binding.Required);
    }

    [Fact]
    public void NpcEventShapeRequiresPlayerAndNpcApis()
    {
        ScriptCallShape shape = Assert.Single(ScriptApiCatalog.BuiltInShapes, item => item.Role == ScriptRole.Npc);

        Assert.Contains(shape.Bindings, binding => binding.Name == "player" && binding.ApiContract == nameof(IPlayerScriptApi) && binding.Required);
        Assert.Contains(shape.Bindings, binding => binding.Name == "npc" && binding.ApiContract == nameof(INpcScriptApi) && binding.Required);
    }
}
