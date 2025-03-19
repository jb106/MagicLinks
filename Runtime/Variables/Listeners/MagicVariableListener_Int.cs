
        using UnityEngine;

        namespace MagicLinks
        {
            public class MagicVariableListener_Int : MagicVariableListener<int>
            {
                [SerializeField] private MagicVariable_Int _variable;
                protected override MagicVariable<int> Variable => _variable;
            }
        }
        