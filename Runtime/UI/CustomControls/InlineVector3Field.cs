using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class InlineVector3Field : VisualElement
{
    [UxmlAttribute] public float x { get; set; } = 0f;
    [UxmlAttribute] public float y { get; set; } = 0f;
    [UxmlAttribute] public float z { get; set; } = 0f;

    private FloatField xField;
    private FloatField yField;
    private FloatField zField;
    private Vector3 _lastValue;

    public Vector3 value
    {
        get => new Vector3(xField.value, yField.value, zField.value);
        set => SetValueWithoutNotify(value);
    }

    public InlineVector3Field()
    {
        style.flexDirection = FlexDirection.Row;
        style.alignItems = Align.Center;

        xField = CreateField(out var xInput);
        yField = CreateField(out var yInput);
        zField = CreateField(out var zInput);

        Add(xField);
        Add(yField);
        Add(zField);

        RegisterCallback<AttachToPanelEvent>(_ =>
        {
            SetValueWithoutNotify(new Vector3(x, y, z));
        });

        xField.RegisterValueChangedCallback(e =>
        {
            x = e.newValue;
            NotifyVector3Changed();
        });

        yField.RegisterValueChangedCallback(e =>
        {
            y = e.newValue;
            NotifyVector3Changed();
        });

        zField.RegisterValueChangedCallback(e =>
        {
            z = e.newValue;
            NotifyVector3Changed();
        });
    }

    private FloatField CreateField(out VisualElement input)
    {
        var field = new FloatField { label = "" };
        field.style.flexGrow = 1;
        field.style.minWidth = 73;
        field.style.maxWidth = 73;
        field.style.flexShrink = 1;
        field.style.overflow = Overflow.Hidden;

        input = field.Q("unity-text-input");
        input.style.flexGrow = 1;
        input.style.flexShrink = 1;
        input.style.overflow = Overflow.Hidden;
        input.style.unityTextAlign = TextAnchor.MiddleLeft;

        return field;
    }

    public void SetValueWithoutNotify(Vector3 v)
    {
        xField.SetValueWithoutNotify(v.x);
        yField.SetValueWithoutNotify(v.y);
        zField.SetValueWithoutNotify(v.z);
        x = v.x;
        y = v.y;
        z = v.z;
        _lastValue = v;
    }

    private void NotifyVector3Changed()
    {
        var newValue = value;
        var evt = ChangeEvent<Vector3>.GetPooled(_lastValue, newValue);
        evt.target = this;
        SendEvent(evt);
        _lastValue = newValue;
    }
}
