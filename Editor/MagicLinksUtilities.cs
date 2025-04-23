using System;
using System.Linq;
using UnityEngine;

public static class MagicLinksUtilities
{
    public const string TypesConfigurationPath = "Assets/Resources/MagicVariables/";
    public const string TypesConfigurationName = "MLTypesConfiguration.asset";
    
    public const string VariablesPath = "Assets/Resources/MagicVariables/Variables";
    public const string ResourcesVariablesPath = "MagicVariables/Variables";
    
    public const string EventDict = "_EVENT";

    public const string String = "string";
    public const string Bool = "bool";
    public const string Int = "int";
    public const string Float = "float";
    public const string Vector2 = "Vector2";
    public const string Vector3 = "Vector3";
    public const string GameObject = "GameObject";
    public const string Transform = "Transform";
    public const string Collider = "Collider";
    public const string Color = "Color";
    
    public static bool DoesTypeExist(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Any(t => t.Name == typeName);
    }
}
