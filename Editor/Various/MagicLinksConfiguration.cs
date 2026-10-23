using System;
using UnityEngine;
using System.Collections.Generic;

namespace MagicLinks
{
    public class MagicLinksConfiguration : ScriptableObject
    {
        public List<string> customTypes = new List<string>();
        public List<string> categories = new List<string>();

        public List<MagicLinkTypeNamePair> typesNamesPairs = new List<MagicLinkTypeNamePair>();

        public bool enableRuntimeUI;

        // ----- Visual customization -----
        public Color variableHeaderColor = new Color(70f / 255f, 95f / 255f, 130f / 255f, 1f);
        public Color variableHeaderAccent = new Color(120f / 255f, 170f / 255f, 230f / 255f, 1f);

        public List<MagicCategoryStyle> categoryStyles = new List<MagicCategoryStyle>();

        // ----- Network -----
        public List<MagicNetTypeSettings> networkTypes = new List<MagicNetTypeSettings>();

        public MagicNetFieldMode GetFieldMode(string typeName, string fieldName)
        {
            foreach (MagicNetTypeSettings typeSettings in networkTypes)
            {
                if (typeSettings.typeName != typeName) continue;

                foreach (MagicNetFieldSettings field in typeSettings.fields)
                {
                    if (field.fieldName == fieldName) return field.mode;
                }
            }

            return MagicNetFieldMode.Unset;
        }

        public void SetFieldMode(string typeName, string fieldName, MagicNetFieldMode mode)
        {
            MagicNetTypeSettings typeSettings = networkTypes.Find(t => t.typeName == typeName);

            if (typeSettings == null)
            {
                typeSettings = new MagicNetTypeSettings { typeName = typeName };
                networkTypes.Add(typeSettings);
            }

            typeSettings.fields.RemoveAll(f => f.fieldName == fieldName);

            if (mode != MagicNetFieldMode.Unset)
                typeSettings.fields.Add(new MagicNetFieldSettings { fieldName = fieldName, mode = mode });

            if (typeSettings.fields.Count == 0)
                networkTypes.Remove(typeSettings);
        }
    }

    [System.Serializable]
    public class MagicLinkTypeNamePair
    {
        public string mlType;
        public string mlName;

        public MagicLinkTypeNamePair(string mlType, string mlName)
        {
            this.mlType = mlType;
            this.mlName = mlName;
        }
    }

    [System.Serializable]
    public class MagicCategoryStyle
    {
        public string category;
        public bool useCustomHeader;
        public Color headerColor = new Color(70f / 255f, 95f / 255f, 130f / 255f, 1f);
        public bool useCustomRow;
        public Color rowColor = new Color(0.4f, 0.4f, 0.4f, 0.25f);
    }

    /// <summary>How a field of a custom type travels on the network.</summary>
    public enum MagicNetFieldMode
    {
        /// <summary>No choice made: the editor picks Value when the field is transportable, otherwise reports an error.</summary>
        Unset = 0,
        /// <summary>Sent by value.</summary>
        Value = 1,
        /// <summary>Never sent: each peer keeps its own local value.</summary>
        Excluded = 2,
        /// <summary>Sent as a reference to a spawned NetworkObject and resolved to the local instance on reception.</summary>
        Reference = 3
    }

    [System.Serializable]
    public class MagicNetTypeSettings
    {
        public string typeName;
        public List<MagicNetFieldSettings> fields = new List<MagicNetFieldSettings>();
    }

    [System.Serializable]
    public class MagicNetFieldSettings
    {
        public string fieldName;
        public MagicNetFieldMode mode;
    }
}
