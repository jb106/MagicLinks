
        using UnityEngine;

        namespace MagicLinks
        {
            public class MagicVariableListener_Collision : MagicVariableListener<Collision>
            {
                [SerializeField] private MagicVariable_Collision _variable;
                protected override MagicVariable<Collision> Variable => _variable;
            }
        }
        