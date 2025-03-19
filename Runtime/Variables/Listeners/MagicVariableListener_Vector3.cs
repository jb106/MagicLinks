
        using UnityEngine;

        namespace MagicLinks
        {
            public class MagicVariableListener_Vector3 : MagicVariableListener<Vector3>
            {
                [SerializeField] private MagicVariable_Vector3 _variable;
                protected override MagicVariable<Vector3> Variable => _variable;
            }
        }
        