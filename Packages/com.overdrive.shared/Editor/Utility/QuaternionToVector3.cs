using UnityEngine; 
using UnityEngine.UIElements;

namespace Overdrive
{
    public class QuaternionEulerField : BaseField<Quaternion>
    {
        Vector3Field eulerField;

        public QuaternionEulerField(string label) : base(label, new Vector3Field())
        {
            eulerField = this.Q<Vector3Field>();
            eulerField.RegisterValueChangedCallback(evt =>
            {
                var euler = evt.newValue;
                value = Quaternion.Euler(euler);
            });
            Add(eulerField);
        }

        public override void SetValueWithoutNotify(Quaternion newValue)
        {
            eulerField.SetValueWithoutNotify(newValue.eulerAngles);
            base.SetValueWithoutNotify(newValue);
        }
    }
}