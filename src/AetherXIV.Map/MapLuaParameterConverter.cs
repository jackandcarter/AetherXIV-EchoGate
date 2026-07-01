using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map;

public static class MapLuaParameterConverter
{
    public static IReadOnlyList<LuaParameter> FromObjects(params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        List<LuaParameter> parameters = new();
        foreach (object? value in values)
            AddValue(value, parameters);

        return parameters;
    }

    private static void AddValue(object? value, List<LuaParameter> parameters)
    {
        if (value is null)
        {
            parameters.Add(new LuaParameter(LuaParameterType.Null, null));
            return;
        }

        if (value is object?[] array)
        {
            foreach (object? item in array)
                AddValue(item, parameters);
            return;
        }

        if (value is Array clrArray and not byte[])
        {
            foreach (object? item in clrArray)
                AddValue(item, parameters);
            return;
        }

        switch (value)
        {
            case int intValue:
                parameters.Add(new LuaParameter(LuaParameterType.Int32, intValue));
                break;
            case uint uintValue:
                parameters.Add(new LuaParameter(LuaParameterType.UInt32, uintValue));
                break;
            case byte byteValue:
                parameters.Add(new LuaParameter(LuaParameterType.UInt8, byteValue));
                break;
            case bool boolValue:
                parameters.Add(new LuaParameter(boolValue ? LuaParameterType.BooleanTrue : LuaParameterType.BooleanFalse, null));
                break;
            case string stringValue:
                parameters.Add(new LuaParameter(LuaParameterType.String, stringValue));
                break;
            case double doubleValue when doubleValue % 1 == 0:
                parameters.Add(new LuaParameter(LuaParameterType.Int32, (int)doubleValue));
                break;
            case float floatValue when floatValue % 1 == 0:
                parameters.Add(new LuaParameter(LuaParameterType.Int32, (int)floatValue));
                break;
            case ActorId actorId:
                parameters.Add(new LuaParameter(LuaParameterType.ActorId, actorId.Value));
                break;
            case IActorScriptApi actor:
                parameters.Add(new LuaParameter(LuaParameterType.ActorId, actor.ActorId.Value));
                break;
            case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                parameters.Add(new LuaParameter(LuaParameterType.Int32, (int)longValue));
                break;
            case ulong ulongValue when ulongValue <= uint.MaxValue:
                parameters.Add(new LuaParameter(LuaParameterType.UInt32, (uint)ulongValue));
                break;
            case short shortValue:
                parameters.Add(new LuaParameter(LuaParameterType.Int32, (int)shortValue));
                break;
            case ushort ushortValue:
                parameters.Add(new LuaParameter(LuaParameterType.Int32, (int)ushortValue));
                break;
        }
    }
}
