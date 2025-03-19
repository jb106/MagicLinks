
        using UnityEngine;

        namespace MagicLinks
        {
            public class MagicVariableListener_GameObject : MagicVariableListener<GameObject>
            {
                [SerializeField] private MagicVariable_GameObject _variable;
                protected override MagicVariable<GameObject> Variable => _variable;
            }
        }
        