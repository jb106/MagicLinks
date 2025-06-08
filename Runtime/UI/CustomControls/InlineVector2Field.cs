using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class InlineVector2Field : VisualElement
{
    [UxmlAttribute] public float x { get; set; } = 0f;
    [UxmlAttribute] public float y { get; set; } = 0f;

    private FloatField xField;
    private FloatField yField;
    private Vector2 _lastValue;

    public Vector2 value
    {
        get => new Vector2(xField.value, yField.value);
        set => SetValueWithoutNotify(value);
    }

    public InlineVector2Field()
    {
        style.flexDirection = FlexDirection.Row;
        style.alignItems = Align.Center;

        xField = new FloatField();
        xField.label = "";
        xField.style.flexGrow = 1;
        xField.style.minWidth = 60;
        xField.style.maxWidth = 120;
        xField.style.flexShrink = 1;
        xField.style.overflow = Overflow.Hidden;

        yField = new FloatField();
        yField.label = "";
        yField.style.flexGrow = 1;
        yField.style.minWidth = 60;
        yField.style.maxWidth = 120;
        yField.style.flexShrink = 1;
        yField.style.overflow = Overflow.Hidden;

        Add(xField);
        Add(yField);

        // Empêche le champ texte interne de déborder
        var xInput = xField.Q("unity-text-input");
        xInput.style.flexGrow = 1;
        xInput.style.flexShrink = 1;
        xInput.style.overflow = Overflow.Hidden;
        xInput.style.unityTextAlign = TextAnchor.MiddleLeft;

        var yInput = yField.Q("unity-text-input");
        yInput.style.flexGrow = 1;
        yInput.style.flexShrink = 1;
        yInput.style.overflow = Overflow.Hidden;
        yInput.style.unityTextAlign = TextAnchor.MiddleLeft;

        RegisterCallback<AttachToPanelEvent>(_ =>
        {
            SetValueWithoutNotify(new Vector2(x, y));
        });

        xField.RegisterValueChangedCallback(e =>
        {
            x = e.newValue;
            NotifyVector2Changed();
        });

        yField.RegisterValueChangedCallback(e =>
        {
            y = e.newValue;
            NotifyVector2Changed();
        });
    }

    public void SetValueWithoutNotify(Vector2 v)
    {
        xField.SetValueWithoutNotify(v.x);
        yField.SetValueWithoutNotify(v.y);
        x = v.x;
        y = v.y;
        _lastValue = v;
    }

    private void NotifyVector2Changed()
    {
        var newValue = value;
        var evt = ChangeEvent<Vector2>.GetPooled(_lastValue, newValue);
        evt.target = this;
        SendEvent(evt);
        _lastValue = newValue;
    }
}
