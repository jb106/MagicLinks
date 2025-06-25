namespace MagicLinks
{
    [System.Serializable]
    public class DynamicVariable
    {
        public string vName;
        public string vLabelType;
        public string vTruelType;
        public string vPath;
        public string initialValue;
        public int magicType;
        public bool isList;
        public string category;

        public bool IsEvent()
        {
            return magicType != 0;
        }

        public bool IsVoid()
        {
            return magicType == 2;
        }

        public string GetDictName()
        {
            if (magicType != 0) return vName + MagicLinksConst.EventDict;

            return vName;
        }
    }
}