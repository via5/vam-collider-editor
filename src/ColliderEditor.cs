using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;

/// <summary>
/// Collider Editor
/// By Acidbubbles and ProjectCanyon
/// Configures and customizes collisions (rigidbodies and colliders)
/// Source: https://github.com/acidbubbles/vam-collider-editor
/// </summary>
public class ColliderEditor : MVRScript
{
    private const string _saveExt = "colliders";
    private const string _noSelectionLabel = "Select...";
    private const string _allLabel = "All";
    private const string _searchDefault = "Search...";
    private string _lastBrowseDir = SuperController.singleton.savesDir;

    private JSONStorableStringChooser _groupsJson;
    private JSONStorableStringChooser _typesJson;
    private JSONStorableBool _modifiedOnlyJson;
    private JSONStorableString _textFilterJson;
    private JSONStorableStringChooser _editablesJson;
    private readonly List<UIDynamicPopup> _popups = new List<UIDynamicPopup>();

    private IModel _selected;
    private EditablesList _editables;

    public override void Init()
    {
        try
        {
            _editables = EditablesList.Build(this);
            BuildUI();
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(ColliderEditor)}.{nameof(Init)}: {e}");
        }
    }

    private void BuildUI()
    {
        var showPreviews = new JSONStorableBool("showPreviews", false, value =>
        {
            foreach (var editable in _editables.All)
                editable.SetShowPreview(value);
        });
        RegisterBool(showPreviews);
        var showPreviewsToggle = CreateToggle(showPreviews);
        showPreviewsToggle.label = "Show Previews";

        var xRayPreviews = new JSONStorableBool("xRayPreviews", true, value =>
        {
            foreach (var editable in _editables.All)
                editable.SetXRayPreview(value);
        });
        RegisterBool(xRayPreviews);
        var xRayPreviewsToggle = CreateToggle(xRayPreviews);
        xRayPreviewsToggle.label = "Use XRay Previews";

        JSONStorableFloat previewOpacity = new JSONStorableFloat("previewOpacity", 0.001f, value =>
        {
            var alpha = value.ExponentialScale(0.1f, 1f);
            foreach (var editable in _editables.All)
                editable.SetPreviewOpacity(alpha);
        }, 0f, 1f);
        RegisterFloat(previewOpacity);
        CreateSlider(previewOpacity).label = "Preview Opacity";

        JSONStorableFloat selectedPreviewOpacity = new JSONStorableFloat("selectedPreviewOpacity", 0.3f, value =>
        {
            var alpha = value.ExponentialScale(0.1f, 1f);
            foreach (var editable in _editables.All)
                editable.SetSelectedPreviewOpacity(alpha);
        }, 0f, 1f);
        RegisterFloat(selectedPreviewOpacity);
        CreateSlider(selectedPreviewOpacity).label = "Selected Preview Opacity";

        var loadPresetUI = CreateButton("Load Preset");
        loadPresetUI.button.onClick.AddListener(() =>
        {
            if (_lastBrowseDir != null) SuperController.singleton.NormalizeMediaPath(_lastBrowseDir);
            SuperController.singleton.GetMediaPathDialog(HandleLoadPreset, _saveExt);
        });

        var savePresetUI = CreateButton("Save Preset");
        savePresetUI.button.onClick.AddListener(() =>
        {
            SuperController.singleton.NormalizeMediaPath(_lastBrowseDir);
            SuperController.singleton.GetMediaPathDialog(HandleSavePreset, _saveExt);

            var browser = SuperController.singleton.mediaFileBrowserUI;
            browser.SetTextEntry(true);
            browser.fileEntryField.text = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds + "." + _saveExt;
            browser.ActivateFileNameField();
        });

        var resetAllUI = CreateButton("Reset All");
        resetAllUI.button.onClick.AddListener(() =>
        {
            foreach (var colliderPair in _editables.Colliders)
                colliderPair.Value.ResetToInitial();
        });

        var groups = new List<string> { _noSelectionLabel };
        groups.AddRange(_editables.Groups.Select(e => e.Name).Distinct());
        groups.Add(_allLabel);
        _groupsJson = new JSONStorableStringChooser("Group", groups, groups[0], "Group");
        _groupsJson.setCallbackFunction = _ => UpdateFilter();
        var groupsList = CreateScrollablePopup(_groupsJson, false);
        groupsList.popupPanelHeight = 400f;
        _popups.Add(groupsList);

        var types = new List<string> { _noSelectionLabel };
        types.AddRange(_editables.All.Select(e => e.Type).Distinct());
        types.Add(_allLabel);
        _typesJson = new JSONStorableStringChooser("Type", types, types[0], "Type");
        _typesJson.setCallbackFunction = _ => UpdateFilter();
        var typesList = CreateScrollablePopup(_typesJson, false);
        typesList.popupPanelHeight = 400f;
        _popups.Add(typesList);

        _modifiedOnlyJson = new JSONStorableBool("Modified Only", false);
        _modifiedOnlyJson.setCallbackFunction = _ => UpdateFilter();
        CreateToggle(_modifiedOnlyJson, false);

        _textFilterJson = new JSONStorableString("Search", _searchDefault);
        _textFilterJson.setCallbackFunction = _ => UpdateFilter();
        CreateTextInput(_textFilterJson, false);

        _editablesJson = new JSONStorableStringChooser(
            "Edit",
            new List<string>(),
            new List<string>(),
            "",
            "Edit");
        var editablesList = CreateScrollablePopup(_editablesJson, true);
        editablesList.popupPanelHeight = 1000f;
        _popups.Add(editablesList);
        _editablesJson.setCallbackFunction = id =>
        {
            if (_selected != null) _selected.Selected = false;
            _editables.ByUuid.TryGetValue(id, out _selected);
            if (_selected != null) _selected.Selected = true;
            SyncPopups();
        };

        UpdateFilter();
    }

    private void SyncPopups()
    {
        foreach (var popup in _popups)
        {
            popup.popup.Toggle();
            popup.popup.Toggle();
        }
    }

    private void UpdateFilter()
    {
        try
        {
            IEnumerable<IModel> filtered = _editables.All;
            var hasSearchQuery = !string.IsNullOrEmpty(_textFilterJson.val) && _textFilterJson.val != _searchDefault;

            if (_groupsJson.val != _allLabel && !(_groupsJson.val == _noSelectionLabel && hasSearchQuery))
                filtered = filtered.Where(e => e.Group?.Name == _groupsJson.val);

            if (_typesJson.val != _allLabel && !(_typesJson.val == _noSelectionLabel && hasSearchQuery))
                filtered = filtered.Where(e => e.Type == _typesJson.val);

            if (_modifiedOnlyJson.val)
                filtered = filtered.Where(e => e.Modified);

            if (hasSearchQuery)
            {
                var tokens = _textFilterJson.val.Split(' ').Select(t => t.Trim());
                foreach (var token in tokens)
                {
                    filtered = filtered.Where(e =>
                        e.Type.IndexOf(token, StringComparison.InvariantCultureIgnoreCase) > -1 ||
                        e.Label.IndexOf(token, StringComparison.InvariantCultureIgnoreCase) > -1
                    );
                }
            }

            var result = filtered.ToList();

            _editablesJson.choices = filtered.Select(x => x.Id).ToList();
            _editablesJson.displayChoices = filtered.Select(x => x.Label).ToList();
            if (!_editablesJson.choices.Contains(_editablesJson.val) || string.IsNullOrEmpty(_editablesJson.val))
                _editablesJson.val = _editablesJson.choices.FirstOrDefault() ?? "";

            SyncPopups();
        }
        catch (Exception e)
        {
            LogError(nameof(UpdateFilter), e.ToString());
        }
    }

    #region Presets

    private void HandleLoadPreset(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        _lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new[] { '/', '\\' })) + @"\";

        LoadFromJson((JSONClass)LoadJSON(path));
    }

    private void HandleSavePreset(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        _lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new[] { '/', '\\' })) + @"\";

        if (!path.ToLower().EndsWith($".{_saveExt}"))
            path += $".{_saveExt}";

        var presetJsonClass = new JSONClass();
        AppendJson(presetJsonClass);
        SaveJSON(presetJsonClass, path);
    }

    #endregion

    #region Load / Save JSON

    public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
    {
        base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);

        try
        {
            LoadFromJson(jc);
        }
        catch (Exception exc)
        {
            LogError(nameof(RestoreFromJSON), exc.ToString());
        }
    }

    private void LoadFromJson(JSONClass jsonClass)
    {
        var editablesJsonClass = jsonClass["editables"].AsObject;
        foreach (string editableId in editablesJsonClass.Keys)
        {
            IModel editableModel;
            if (_editables.ByUuid.TryGetValue(editableId, out editableModel))
                editableModel.LoadJson(editablesJsonClass[editableId].AsObject);
        }
    }

    public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
    {
        var jsonClass = base.GetJSON(includePhysical, includeAppearance, forceStore);

        needsStore = true;

        AppendJson(jsonClass);

        return jsonClass;
    }

    private void AppendJson(JSONClass jsonClass)
    {
        var editablesJsonClass = new JSONClass();
        foreach (var editable in _editables.All)
        {
            editable.AppendJson(editablesJsonClass);
        }
        jsonClass.Add("editables", editablesJsonClass);
    }

    #endregion

    #region Unity events

    public void OnDestroy()
    {
        if (_editables?.All == null) return;
        try
        {
            foreach (var editable in _editables.All)
                editable.DestroyPreview();
        }
        catch (Exception e)
        {
            LogError(nameof(OnDestroy), e.ToString());
        }
    }


    private void FixedUpdate()
    {
        // TODO: Validate whether this is really necessary. Running code multiple times per frame should be avoided.
        foreach (var colliderPair in _editables.Colliders)
        {
            colliderPair.Value.UpdateControls();
            colliderPair.Value.UpdatePreview();
        }
    }

    #endregion

    private void LogError(string method, string message) => SuperController.LogError($"{nameof(ColliderEditor)}.{method}: {message}");

    public UIDynamicTextField CreateTextInput(JSONStorableString jss, bool rightSide = false)
    {
        var textfield = CreateTextField(jss, rightSide);
        textfield.height = 20f;
        textfield.backgroundColor = Color.white;
        var input = textfield.gameObject.AddComponent<InputField>();
        var rect = input.GetComponent<RectTransform>().sizeDelta = new Vector2(1f, 0.4f);
        input.textComponent = textfield.UItext;
        jss.inputField = input;
        return textfield;
    }
}
