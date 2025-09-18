using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;

using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class PuzzleMenuSceneManager : MonoBehaviour
{

    enum State
    {
        None,
        Menu,
        Setting,
        Solutions,
        Loading,
    }

    /*
     * UI
     */
    public Toggle HardcoreToggle;
    public TextMeshProUGUI TextProgress;
    public TextMeshProUGUI TextCongratulations;
    public Image Preview;
    string _l10nLevel;
    public TextMeshProUGUI TextLevel;

    /*
     * Menu
     */
    public GameObject MenuPanel;
    public Button MenuOpenButton;
    public Button MenuCloseButton;

    public GameObject SettingPanel;
    public Button SettingOpenButton;
    public Button SettingCloseButton;

    public Button ReturnToMenuButton;

    public Button QuitButton;

    public GameObject SolutionsPanel;
    public Button SolutionsCloseButton;

    Camera _camera;
    Vector2 _mousePos;

    /*
     * _state
     */
    State _state;
    AssetManager _assetManager;
    AudioManager _audioManager;
    LoadingManager _loadingManager;
    PersistentManager _persistentManager;
    SettingManager _settingManager;
    SteamManager _steamManager;
    SolutionManager _solutionManager;

    GameObject[] _parentsH;
    GameObject _activePuzzle;
    Vector2 _expansionScale = new Vector2(1.2f, 1.2f);

    void ChangeState(State to)
    {
        switch (to)
        {
            case State.None:
                SolutionsPanel.SetActive(false);
                MenuPanel.SetActive(false);
                SolutionsPanel.SetActive(false);
                break;
            case State.Solutions:
                SolutionsPanel.SetActive(true);
                break;
            case State.Menu:
                MenuPanel.SetActive(true);
                if (_state == State.Setting)
                {
                    SettingPanel.SetActive(false);
                    _persistentManager.SetBGMVolume(_settingManager.BGMSlider.value);
                    _persistentManager.SetSEVolume(_settingManager.SESlider.value);
                    _persistentManager.SetMouseWheelSensitivity((int)_settingManager.MouseWheelSensitivitySlider.value);
                }
                break;
            case State.Setting:
                SettingPanel.SetActive(true);
                break;
            case State.Loading:
                break;
            default:
                Debug.LogError("Unexpected _state" + to);
                break;
        }
        _state = to;
    }

    /*
     * GameObject#name represents the level required to be unlocked.
     */
    int LevelsRequiredUnlock(GameObject metaTileParent)
    {
        return Int32.Parse(metaTileParent.name.Substring(1));
    }

    void Awake()
    {
        _assetManager = this.gameObject.GetComponent<AssetManager>();
        _audioManager = this.gameObject.GetComponent<AudioManager>();
        _loadingManager = this.gameObject.GetComponent<LoadingManager>();
        _persistentManager = this.gameObject.GetComponent<PersistentManager>();
        _settingManager = this.gameObject.GetComponent<SettingManager>();
        _solutionManager = this.gameObject.GetComponent<SolutionManager>();
    }

    void Start()
    {
        _camera = Camera.main;
        _audioManager.SetPlaylist(_assetManager.GetPlaylist(LoadingManager.Scene.PuzzleMenu)).StartBGM();
        _steamManager = GameObject.Find("/SteamManager").GetComponent<SteamManager>();
        _l10nLevel = LocalizationSettings.StringDatabase.GetTableEntry("default", "level").Entry.Value;
        int currentLevel = _persistentManager.LoadProgress(GlobalData.Slot).CurrentLevel;
        HardcoreToggle.isOn = GlobalData.IsHardcoreMode = _persistentManager.IsHardcoreMode(GlobalData.Slot);
        HardcoreToggle.onValueChanged.AddListener((isOn) => GlobalData.IsHardcoreMode = _persistentManager.SetHardcoreMode(GlobalData.Slot, isOn));
        TextProgress.text = $"{currentLevel * 100 / GlobalData.TotalLevel}%";
        MenuOpenButton.onClick.AddListener(() => ChangeState(State.Menu));
        MenuCloseButton.onClick.AddListener(() => ChangeState(State.None));
        SettingOpenButton.onClick.AddListener(() => ChangeState(State.Setting));
        SettingCloseButton.onClick.AddListener(() => ChangeState(State.Menu));
        ReturnToMenuButton.onClick.AddListener(OnReturnToMenuButtonClick);
        SolutionsCloseButton.onClick.AddListener(() => ChangeState(State.None));
        QuitButton.onClick.AddListener(OnPowerOff);
        _parentsH = GameObject.Find("/PlacedTiles/H").Children();
        var parentsT = GameObject.Find("/PlacedTiles/T").Children();
        var parentsF = GameObject.Find("/PlacedTiles/F").Children();
        var parentsP = GameObject.Find("/PlacedTiles/P").Children();
        var metaTileParents = _parentsH.Concat(parentsT).Concat(parentsF).Concat(parentsP).ToArray();
        for (int level = 1; level <= currentLevel; level++)
            StartCoroutine(_assetManager.LoadPuzzleFrameAsync(level, Color.white, (sprite) => {}));
        TextCongratulations.gameObject.SetActive(currentLevel == GlobalData.TotalLevel);
        foreach (var metaTileParent in metaTileParents)
        {
            var requiredLevel = LevelsRequiredUnlock(metaTileParent);
            // Solved puzzle.
            if (currentLevel > requiredLevel)
            {
                _steamManager.UnlockAchievement(requiredLevel + 1);
            }
            // Unresolved puzzle but shown.
            else if (currentLevel == requiredLevel)
            {
                var dissolveMaterial = new Material(_assetManager.DissolveMaterial);
                foreach (var tile in metaTileParent.Children())
                {
                    foreach (var checkmark in tile.Children())
                        checkmark.SetActive(false);
                    foreach (var renderer in metaTileParent.GetComponentsInChildren<SpriteRenderer>())
                        renderer.material = dissolveMaterial;
                }
                StartCoroutine(DissolveAsync(dissolveMaterial));
            }
            // Hidden puzzle.
            else
            {
                metaTileParent.SetActive(false);
            }
            // If all are not resolved, change to white.
            if (currentLevel != GlobalData.TotalLevel)
            {
                foreach (var tileComponent in metaTileParent.GetComponentsInChildren<Tile>())
                {
                    if (!Tags.match(tileComponent.gameObject, Tags.LevelTile))
                        tileComponent.ChangeColor(Color.white);
                }
            }
        }
        ChangeState(State.None);
    }

    IEnumerator DissolveAsync(Material material)
    {
        var se = _assetManager.SETileDissolve;
        _audioManager.PlaySE(se);
        float t = 0f;
        while (t < se.length)
        {
            t += Time.deltaTime;
            var ratio = Mathf.Lerp(0f, 1f, t / se.length);
            material.SetFloat("_DissolveRatio", ratio);
            yield return null;
        }
        yield return null;
    }

    void FixedUpdate()
    {
        _mousePos = _camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        switch (_state)
        {
            case State.None:
                {
                    var o = XGameObject.AtWorldPoint(_mousePos);
                    if (o == null || !_parentsH.Contains(o = o.Parent()))
                    {
                        Preview.gameObject.SetActive(false);
                        TextLevel.text = null;
                        if (_activePuzzle != null)
                        {
                            _activePuzzle.transform.GetChild(0).localScale /= _expansionScale;
                            _activePuzzle = null;
                        }
                    }
                    else if (o != _activePuzzle)
                    {
                        var level = LevelsRequiredUnlock(o) + 1;
                        _audioManager.PlaySE(_assetManager.SEOnHoverUI);
                        StartCoroutine(_assetManager.LoadPuzzleFrameAsync(level, Color.white, (sprite) => {
                            Preview.gameObject.SetActive(true);
                            Preview.sprite = sprite;
                            TextLevel.text = $"{_l10nLevel} {level}";
                        }));
                        if (_activePuzzle != null)
                        {
                            _activePuzzle.transform.GetChild(0).localScale /= _expansionScale;
                        }
                        _activePuzzle = o;
                        _activePuzzle.transform.GetChild(0).localScale *= _expansionScale;
                    }
                }
                break;
            default:
                break;
        }
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            switch (_state)
            {
                case State.None:
                    var o = XGameObject.AtWorldPoint(_mousePos);
                    if (Tags.match(o, Tags.LevelTile))
                    {
                        var se = _assetManager.SEOK;
                        GlobalData.Level = LevelsRequiredUnlock(o.Parent()) + 1;
                        if (_solutionManager.Init().HasSolution() && !GlobalData.IsHardcoreMode)
                        {
                            ChangeState(State.Solutions);
                        }
                        else {
                            _solutionManager.OpenNewSolution();
                            ChangeState(State.Loading);
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }

    public void OnReturnToMenuButtonClick()
    {
        StartCoroutine(_loadingManager.LoadAsync(LoadingManager.Scene.Menu));
    }

    public void OnCancel(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        _audioManager.PlaySE(_assetManager.SECancel);
        switch (_state)
        {
            case State.None:
                ChangeState(State.Menu);
                break;
            case State.Solutions:
                _solutionManager.OnCancel();
                if (!SolutionsPanel.activeSelf) ChangeState(State.None);
                break;
            case State.Menu:
                ChangeState(State.None);
                break;
            case State.Setting:
                ChangeState(State.Menu);
                break;
            default:
                break;
        }
    }

    void OnPowerOff()
    {
        _steamManager.Close();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnDebug(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
#if UNITY_EDITOR
        // _steamManager.ResetAllAchievements();
        StartCoroutine(_loadingManager.LoadAsync(LoadingManager.Scene.PuzzleMenu, 0.5f, () => {
            int currentLevel = _persistentManager.LoadProgress(GlobalData.Slot).CurrentLevel;
            _persistentManager.SaveProgress(GlobalData.Slot, new Progress(Math.Min(GlobalData.TotalLevel, currentLevel + 1)));
            GlobalData.GameMode = GameMode.Puzzle;
        }));
#endif
    }

}
