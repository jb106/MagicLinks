
        using UnityEngine;

        namespace MagicLinks
        {
            public class MagicVariableListener_Collider : MagicVariableListener<Collider>
            {
                [SerializeField] private MagicVariable_Collider _variable;
                protected override MagicVariable<Collider> Variable => _variable;
            }
        }
        