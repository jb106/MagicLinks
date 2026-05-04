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
}
