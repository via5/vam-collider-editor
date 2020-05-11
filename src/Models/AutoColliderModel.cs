using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

using Object = UnityEngine.Object;

public class AutoColliderModel : ColliderContainerModelBase<AutoCollider>, IModel
{
    private readonly float _initialAutoLengthBuffer;
    private readonly float _initialAutoRadiusBuffer;
    private readonly float _initialAutoRadiusMultiplier;
    private readonly List<ColliderModel> _ownedColliders = new List<ColliderModel>();

    public List<UIDynamic> Controls { get; private set; }

    public AutoColliderModel(MVRScript script, AutoCollider autoCollider)
        : base(script, autoCollider, $"[au] {Simplify(autoCollider.name)}")
    {
        _initialAutoLengthBuffer = autoCollider.autoLengthBuffer;
        _initialAutoRadiusBuffer = autoCollider.autoRadiusBuffer;
        _initialAutoRadiusMultiplier = autoCollider.autoRadiusMultiplier;
        if (Component.hardCollider != null) _ownedColliders.Add(ColliderModel.CreateTyped(script, autoCollider.hardCollider));
        if (Component.jointCollider != null) _ownedColliders.Add(ColliderModel.CreateTyped(script, Component.jointCollider));
    }

    protected override void CreateControls()
    {
        DestroyControls();

        var controls = new List<UIDynamic>();

        var resetUi = Script.CreateButton("Reset AutoCollider", true);
        resetUi.button.onClick.AddListener(ResetToInitial);

        controls.Add(resetUi);
        controls.AddRange(DoCreateControls());

        Controls = controls;
    }

    public IEnumerable<UIDynamic> DoCreateControls()
    {
        yield return Script.CreateFloatSlider(new JSONStorableFloat("autoLengthBuffer", Component.autoLengthBuffer, value =>
        {
            Component.autoLengthBuffer = value;
        }, 0f, _initialAutoLengthBuffer * 4f, false).WithDefault(_initialAutoLengthBuffer), "Auto Length Buffer");

        yield return Script.CreateFloatSlider(new JSONStorableFloat("autoRadiusBuffer", Component.autoRadiusBuffer, value =>
        {
            Component.autoRadiusBuffer = value;
        }, 0f, _initialAutoRadiusBuffer * 4f, false).WithDefault(_initialAutoRadiusBuffer), "Auto Radius Buffer");

        yield return Script.CreateFloatSlider(new JSONStorableFloat("autoRadiusMultiplier", Component.autoRadiusMultiplier, value =>
        {
            Component.autoRadiusMultiplier = value;
        }, 0f, _initialAutoRadiusMultiplier * 4f, false).WithDefault(_initialAutoRadiusMultiplier), "Auto Radius Multiplier");
    }

    protected override void DestroyControls()
    {
        if (Controls == null)
            return;

        foreach (var adjustmentJson in Controls)
            Object.Destroy(adjustmentJson.gameObject);

        Controls.Clear();
    }

    protected override void SetSelected(bool value)
    {
        // TODO: Track colliders to highlight them
        base.SetSelected(value);
    }

    protected override void DoLoadJson(JSONClass jsonClass)
    {
        Component.autoLengthBuffer = jsonClass["autoLengthBuffer"].AsFloat;
        Component.autoRadiusBuffer = jsonClass["autoRadiusBuffer"].AsFloat;
        Component.autoRadiusMultiplier = jsonClass["autoRadiusMultiplier"].AsFloat;
    }

    protected override JSONClass DoGetJson()
    {
        var jsonClass = new JSONClass();
        jsonClass["autoLengthBuffer"].AsFloat = Component.autoLengthBuffer;
        jsonClass["autoRadiusBuffer"].AsFloat = Component.autoRadiusBuffer;
        jsonClass["autoRadiusMultiplier"].AsFloat = Component.autoRadiusMultiplier;
        return jsonClass;
    }

    public void ResetToInitial()
    {
        DoResetToInitial();

        if (Selected)
        {
            DestroyControls();
            CreateControls();
        }
    }

    protected void DoResetToInitial()
    {
        Component.autoRadiusBuffer = _initialAutoRadiusBuffer;
    }

    public override IEnumerable<ColliderModel> GetColliders() => _ownedColliders;

    public IEnumerable<Rigidbody> GetRigidbodies()
    {
        if (Component.jointRB != null) yield return Component.jointRB;
        if (Component.kinematicRB != null) yield return Component.kinematicRB;
    }
}