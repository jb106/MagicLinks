
        using UnityEngine;

        namespace MagicLinks
        {
            public class MagicVariableListener_String : MagicVariableListener<string>
            {
                [SerializeField] private MagicVariable_String _variable;
                protected override MagicVariable<string> Variable => _variable;
            }
        }
        