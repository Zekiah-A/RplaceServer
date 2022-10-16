using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.VisualBasic.FileIO;
using Nephrite.Exceptions;
using Nephrite.Lexer;

namespace Nephrite.Runtime;
internal class NephriteEnvironment
{
    private readonly NephriteEnvironment? enclosing;
    private readonly Dictionary<string, object> values;
    private const BindingFlags BindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;

    public NephriteEnvironment(NephriteEnvironment? enclosing = null)
    {
        this.enclosing = enclosing;
        values = new Dictionary<string, object>();
    }

    // Variables can not have empty names.
    public void Define(Token token, object? value)
        => values.Add(token.Value!.ToString()!, value);

    public void Delete(Token token)
        => values.Remove(token.Value?.ToString()!);

    public object? Get(Token name)
    {
        if (name.Value is not null)
        {
            var value = name.Value.ToString();

            if (value is not null)
            {
                if (values.ContainsKey(value))
                    return values[value];

                if (enclosing != null)
                    return enclosing.Get(name);

                foreach (var fieldInfos in Assembly.GetExecutingAssembly().GetTypes().Select(type => type.GetFields(BindingFlags)))
                {
                    foreach (var field in fieldInfos)
                    {
                        if (!field.Name.Equals(value)) continue;
                        return field.GetValue(field);
                    }
                }
                    
                throw new RuntimeErrorException($"Undefined variable '{name}'");
            }
        }

        throw new RuntimeErrorException($"Undefined variable '{name}'");
    }

    public void Assign(Token token, object value)
    {
        if (token.Value is not null)
        {
            var name = token.Value.ToString();

            if (name is not null)
            {
                if (values.ContainsKey(name))
                {
                    values[name] = value;
                    return;
                }

                if (enclosing != null)
                {
                    enclosing.Assign(token, value);
                    return;
                }
                
                foreach (var fieldInfos in Assembly.GetExecutingAssembly().GetTypes().Select(type => type.GetFields(BindingFlags)))
                {
                    foreach (var field in fieldInfos)
                    {
                        try
                        {
                            if (!field.Name.Equals(name)) continue;
                            var convertedValue = Convert.ChangeType(value, field.GetValue(field)?.GetType() ?? throw new InvalidOperationException());
                            field.SetValue(field, convertedValue);
                            return;
                        }
                        catch (Exception e)
                        {
                            throw new RuntimeErrorException($"Cast error, could could not cast value to type of '{name}'.\n{e}'");
                        }
                    }
                }
            }
            
            
        }

        throw new RuntimeErrorException($"Undefined variable '{token}'");
    }
}
