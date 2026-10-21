using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN.World
{
    public enum WorldMode { Exploring, Dialogue, Paused }

    /// <summary>
    /// 3D 探索世界的总控：搭关卡、生成玩家与相机、处理互动按键、
    /// 在世界里以叠加层形式启动 AVG 对话（复用 VNView），以及暂停/存档。
    /// 由 GameManager.EnterWorld 创建；返回主菜单时整体销毁。
    /// </summary>
    public class WorldController : MonoBehaviour
    {
        public static WorldController Instance { get; private set; }

        public WorldMode Mode { get; private set; } = WorldMode.Exploring;
        public bool IsExploring => Mode == WorldMode.Exploring;

        public PlayerController Player { get; private set; }
        public ThirdPersonCamera CameraRig { get; private set; }
        public WorldHUD Hud { get; private set; }
        public WorldLevel Level { get; private set; }

        public KeyCode interactKey = KeyCode.E;

        private Camera _camera;
        private InteractionDetector _detector;
        private readonly Dictionary<string, StoryData> _storyCache = new Dictionary<string, StoryData>();
        private Action _onDialogueDone;

        private CameraClearFlags _savedClearFlags;
        private Color _savedBackground;
        private bool _levelBuiltAtRuntime;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>由 GameManager 调用。loadSave=true 时尝试读取世界存档。</summary>
        public void Init(Camera camera, bool loadSave)
        {
            _camera = camera;
            if (_camera == null) _camera = Camera.main;

            // 关卡：场景里有 WorldLevel 就用它，没有就现搭一个原型
            Level = FindObjectOfType<WorldLevel>();
            if (Level == null)
            {
                var levelGO = new GameObject("WorldLevel");
                Level = levelGO.AddComponent<WorldLevel>();
                LevelBuilder.BuildPrototype(Level);
                _levelBuiltAtRuntime = true;
            }
            else if (Level.autoBuildPrototype && Level.transform.childCount == 0)
            {
                LevelBuilder.BuildPrototype(Level);
            }

            // 场景里的角色仅用于 Edit Mode 摆放出生点，运行时换成真正可操控玩家。
            foreach (var child in Level.GetComponentsInChildren<Transform>(true))
                if (child.name == "PlayerPreview_EDIT_ONLY")
                    child.gameObject.SetActive(false);

            // 玩家位置：读档 or 出生点
            Vector3 pos = Level.SpawnPosition;
            float yaw = Level.SpawnYaw;
            if (loadSave)
            {
                if (!WorldState.TryLoad(out pos, out yaw))
                {
                    pos = Level.SpawnPosition;
                    yaw = Level.SpawnYaw;
                }
            }
            else
            {
                WorldState.Reset();
            }

            // 让关卡里的物体按变量恢复状态（已拾取 / 已开门 / 新游戏复位）
            foreach (var it in Level.GetComponentsInChildren<Interactable>(true))
                it.OnWorldStart();

            var playerGO = LevelBuilder.BuildPlayer(pos, yaw);
            Player = playerGO.GetComponent<PlayerController>();
            _detector = playerGO.GetComponent<InteractionDetector>();

            // 相机
            if (_camera != null)
            {
                _savedClearFlags = _camera.clearFlags;
                _savedBackground = _camera.backgroundColor;
                _camera.clearFlags = CameraClearFlags.Skybox;
                _camera.backgroundColor = new Color(0.55f, 0.7f, 0.85f);
                _camera.nearClipPlane = 0.1f;

                CameraRig = _camera.GetComponent<ThirdPersonCamera>();
                if (CameraRig == null) CameraRig = _camera.gameObject.AddComponent<ThirdPersonCamera>();
                CameraRig.Target = Player.transform;
                CameraRig.SnapBehind(yaw);
                Player.CameraReference = _camera.transform;
            }

            Hud = WorldHUD.Create(GameManager.Instance.Root);

            SetMode(WorldMode.Exploring);
            if (loadSave) Hud.Toast(GameLanguage.Loaded);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Mode == WorldMode.Exploring) Pause();
                else if (Mode == WorldMode.Paused) Resume();
                return;
            }

            if (Mode != WorldMode.Exploring) return;

            var target = _detector != null ? _detector.Current : null;
            Hud.SetPrompt(target != null ? target.Prompt : "");

            if (target != null && Input.GetKeyDown(interactKey))
                target.Interact(this);
        }

        // ---------- 模式切换 ----------

        private void SetMode(WorldMode mode)
        {
            Mode = mode;
            bool exploring = mode == WorldMode.Exploring;

            if (Player != null)
            {
                Player.InputEnabled = exploring;
                if (!exploring) Player.Halt();
            }
            if (_detector != null)
            {
                _detector.enabled = exploring;
                if (!exploring) _detector.Clear();
            }
            if (CameraRig != null) CameraRig.InputEnabled = exploring;

            if (Hud != null)
            {
                Hud.SetVisible(mode != WorldMode.Dialogue);
                if (!exploring) Hud.SetPrompt("");
            }

            Cursor.lockState = exploring ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !exploring;
            Time.timeScale = mode == WorldMode.Paused ? 0f : 1f;
        }

        // ---------- 对话 ----------

        /// <summary>
        /// 在世界里叠加播放一段 AVG 对话。对话结束（节点没有 next、或 ending 为空）后回到探索。
        /// 若节点带 ending（如 "menu" / "pet" / "lock"），交给 GameManager.OnEnding 处理（会离开世界）。
        /// </summary>
        public void StartDialogue(string storyFile, string startNode, Action onDone = null)
        {
            if (Mode == WorldMode.Dialogue) return;

            var story = LoadStory(storyFile);
            if (story == null)
            {
                Hud.Toast(GameLanguage.MissingStoryFile(string.IsNullOrEmpty(storyFile) ? GameConfig.WorldStoryFile : storyFile));
                return;
            }

            string node = string.IsNullOrEmpty(startNode) ? story.startNode : startNode;
            if (story.Get(node) == null)
            {
                Hud.Toast("Missing node: " + node);
                return;
            }

            _onDialogueDone = onDone;
            SetMode(WorldMode.Dialogue);
            GameManager.Instance.StartDialogueOverlay(story, node, OnDialogueEnded, WorldState.Variables);
        }

        private void OnDialogueEnded(string ending)
        {
            // 把对话里改动的变量同步回世界
            var vn = GameManager.Instance.CurrentDialogue;
            if (vn != null) WorldState.Merge(vn.Variables);
            GameManager.Instance.CloseDialogue();

            var done = _onDialogueDone;
            _onDialogueDone = null;

            if (!string.IsNullOrEmpty(ending))
            {
                // 带结局的节点：交回 GameManager（会 ClearScreen，销毁本世界）
                GameManager.Instance.OnEnding(ending);
                return;
            }

            SetMode(WorldMode.Exploring);
            done?.Invoke();
        }

        private StoryData LoadStory(string storyFile)
        {
            if (string.IsNullOrEmpty(storyFile)) storyFile = GameConfig.WorldStoryFile;
            if (_storyCache.TryGetValue(storyFile, out var cached)) return cached;

            // 先找本地化版本（如 world_en.json），没有再退回原文件
            var data = StoryLoader.Load(GameLanguage.LocalizedJson(storyFile));
            if (data == null && GameLanguage.IsEnglish) data = StoryLoader.Load(storyFile);
            if (data != null) _storyCache[storyFile] = data;
            return data;
        }

        // ---------- 暂停 / 存档 ----------

        public void Pause()
        {
            if (Mode != WorldMode.Exploring) return;
            SetMode(WorldMode.Paused);
            Hud.ShowPause(Resume, SaveGame, BackToMainMenu);
        }

        public void Resume()
        {
            if (Mode != WorldMode.Paused) return;
            Hud.HidePause();
            SetMode(WorldMode.Exploring);
        }

        public void SaveGame()
        {
            if (Player == null) return;
            WorldState.Save(Player.transform.position, Player.transform.eulerAngles.y);
            FairytaleVFX.Spawn("SaveTwinkle", Player.transform.position + Vector3.up * 1.5f, 2.4f, 1.15f);
            Hud.Toast(GameLanguage.Saved);
        }

        private void BackToMainMenu()
        {
            Time.timeScale = 1f;
            GameManager.Instance.ShowMainMenu();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (CameraRig != null) Destroy(CameraRig);
            if (_camera != null)
            {
                _camera.clearFlags = _savedClearFlags;
                _camera.backgroundColor = _savedBackground;
            }
            if (Player != null) Destroy(Player.gameObject);
            if (Hud != null) Destroy(Hud.gameObject);
            // 运行时现搭的原型关卡随世界一起销毁，下次进入重新搭（场景里手工摆的关卡则保留）
            if (_levelBuiltAtRuntime && Level != null) Destroy(Level.gameObject);
        }
    }
}
