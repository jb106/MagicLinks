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
}