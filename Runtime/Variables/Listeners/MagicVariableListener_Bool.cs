using UnityEngine;

namespace MagicLinks
{
    public class MagicVariableListener_Bool : MagicVariableListener<bool>
    {
        [SerializeField] private MagicVariable_Bool _variable;
        protected override MagicVariable<bool> Variable => _variable;
    }
}
