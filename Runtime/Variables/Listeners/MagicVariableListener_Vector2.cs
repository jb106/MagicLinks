
        using UnityEngine;

        namespace MagicLinks
        {
            public class MagicVariableListener_Vector2 : MagicVariableListener<Vector2>
            {
                [SerializeField] private MagicVariable_Vector2 _variable;
                protected override MagicVariable<Vector2> Variable => _variable;
            }
        }
        