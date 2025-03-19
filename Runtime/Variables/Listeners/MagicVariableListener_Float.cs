
        using UnityEngine;

        namespace MagicLinks
        {
            public class MagicVariableListener_Float : MagicVariableListener<float>
            {
                [SerializeField] private MagicVariable_Float _variable;
                protected override MagicVariable<float> Variable => _variable;
            }
        }
        