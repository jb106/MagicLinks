using System;
using UnityEngine;
using System.Collections.Generic;

namespace MagicLinks
{
    public class MagicLinksConfiguration : ScriptableObject
    {
        public List<string> customTypes = new List<string>();
        public List<string> categories = new List<string>();
    }
}