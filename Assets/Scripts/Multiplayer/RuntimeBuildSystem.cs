using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VN.World;

namespace VN.Multiplayer
{
    /// <summary>
    /// 游戏内共享建造模式。B 开关，1-5 选择物件，左键放置，右键删除，
    /// Shift+左键选择，R 旋转。所有变更经 Host 验证/广播。
    /// </summary>
    public class RuntimeBuildSystem : MonoBehaviour
    {
        public bool BuildMode { get; private set; }
        public string SelectedKind => Kinds[_kindIndex];

        private static readonly string[] Kinds = { "crate", "bench", "lantern", "flowers", "xylophone" };
        private readonly Dictionary<int, NetworkEditableObject> _objects = new Dictionary<int, NetworkEditableObject>();
        private MultiplayerSession _session;
        private LocalMultiplayerManager _players;
        private WorldController _world;
        private Camera _camera;
        private Transform _root;
        private int _kindIndex;
        private int _nextHostObjectId = 1000;
        private NetworkEditableObject _selected;

        public void Init(WorldController world, MultiplayerSession session,
            LocalMultiplayerManager players, Camera camera)
        {
            _world = world;
            _session = session;
            _players = players;
            _camera = camera;
            var rootGO = new GameObject("NetworkEditableObjects");
            rootGO.transform.SetParent(world.Level.transform, false);
            _root = rootGO.transform;
            _session.MessageReceived += OnNetworkMessage;
        }

        private void Update()
        {
            if (_world == null || !_world.IsExploring) return;
            if (_players.LocalPlayers.Any(p => p != null && p.InputSlot != null && p.InputSlot.BuildToggleDown))
            {
                BuildMode = !BuildMode;
                if (_world.CameraRig != null) _world.CameraRig.InputEnabled = !BuildMode;
                Cursor.lockState = BuildMode ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = BuildMode;
                _world.Hud.Toast(BuildMode
                    ? "建造模式：1-5 选择 · 左键放置 · 右键删除 · Shift+左键选择 · R 旋转"
                    : "已退出建造模式", 3f);
            }
            if (!BuildMode) return;

            for (int i = 0; i < Kinds.Length; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) _kindIndex = i;

            if (Input.GetMouseButtonDown(0))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    SelectUnderCursor();
                else
                    RequestSpawnAtCursor();
            }
            if (Input.GetMouseButtonDown(1)) RequestDeleteUnderCursor();
            if (_selected != null && Input.GetKeyDown(KeyCode.R))
            {
                _selected.transform.Rotate(0f, 15f, 0f, Space.World);
                RequestTransform(_selected);
            }
            if (_selected != null && Input.GetKeyDown(KeyCode.PageUp))
            {
                _selected.transform.position += Vector3.up * 0.25f;
                RequestTransform(_selected);
            }
            if (_selected != null && Input.GetKeyDown(KeyCode.PageDown))
            {
                _selected.transform.position -= Vector3.up * 0.25f;
                RequestTransform(_selected);
            }
        }

        private void RequestSpawnAtCursor()
        {
            if (!RaycastCursor(out var hit)) return;
            int id = _session.Mode == SessionMode.Client ? 0 : _nextHostObjectId++;
            _session.EmitEdit(new MultiplayerMessage
            {
                type = MultiplayerProtocol.EditSpawn,
                objectId = id,
                payload = SelectedKind,
                Position = Snap(hit.point),
                yaw = 0f
            });
        }

        private void RequestDeleteUnderCursor()
        {
            if (!RaycastCursor(out var hit)) return;
            var editable = hit.collider.GetComponentInParent<NetworkEditableObject>();
            if (editable == null || editable.hostProtected) return;
            _session.EmitEdit(new MultiplayerMessage
            {
                type = MultiplayerProtocol.EditDelete,
                objectId = editable.objectId
            });
        }

        private void SelectUnderCursor()
        {
            if (!RaycastCursor(out var hit)) return;
            _selected = hit.collider.GetComponentInParent<NetworkEditableObject>();
            if (_selected != null)
                _world.Hud.Toast("已选择 " + _selected.prefabKind + " #" + _selected.objectId);
        }

        private void RequestTransform(NetworkEditableObject editable)
        {
            _session.EmitEdit(new MultiplayerMessage
            {
                type = MultiplayerProtocol.EditTransform,
                objectId = editable.objectId,
                Position = editable.transform.position,
                yaw = editable.transform.eulerAngles.y
            });
        }

        private bool RaycastCursor(out RaycastHit hit)
        {
            hit = default(RaycastHit);
            if (_camera == null) return false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out hit, 120f, ~0, QueryTriggerInteraction.Ignore);
        }

        private static Vector3 Snap(Vector3 value)
        {
            value.x = Mathf.Round(value.x * 2f) * 0.5f;
            value.y = Mathf.Round(value.y * 4f) * 0.25f;
            value.z = Mathf.Round(value.z * 2f) * 0.5f;
            return value;
        }

        private void OnNetworkMessage(MultiplayerMessage message)
        {
            switch (message.type)
            {
                case MultiplayerProtocol.EditSpawn:
                    Spawn(message.objectId, message.payload, message.Position, message.yaw);
                    break;
                case MultiplayerProtocol.EditDelete:
                    if (_objects.TryGetValue(message.objectId, out var editable) && !editable.hostProtected)
                    {
                        if (_selected == editable) _selected = null;
                        Destroy(editable.gameObject);
                        _objects.Remove(message.objectId);
                    }
                    break;
                case MultiplayerProtocol.EditTransform:
                    if (_objects.TryGetValue(message.objectId, out var moved))
                    {
                        moved.transform.position = message.Position;
                        moved.transform.rotation = Quaternion.Euler(0f, message.yaw, 0f);
                    }
                    break;
            }
        }

        private void Spawn(int id, string kind, Vector3 position, float yaw)
        {
            if (id == 0 || _objects.ContainsKey(id)) return;
            GameObject go;
            switch (kind)
            {
                case "bench":
                    go = FairytaleArt.BuildBench(_root, position, yaw);
                    break;
                case "lantern":
                    go = FairytaleArt.BuildLantern(_root, position);
                    break;
                case "flowers":
                    go = FairytaleArt.BuildFlowers(_root, position, id);
                    break;
                case "xylophone":
                    go = FairytaleArt.BuildXylophone(_root, position, yaw);
                    var instrumentCollider = go.AddComponent<BoxCollider>();
                    instrumentCollider.center = new Vector3(0f, 0.65f, 0f);
                    instrumentCollider.size = new Vector3(3.6f, 1.4f, 1.7f);
                    var instrument = go.AddComponent<PlayableInstrument>();
                    instrument.instrumentId = id;
                    instrument.displayName = GameLanguage.IsEnglish ? "xylophone" : "木琴";
                    break;
                default:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "CrayonCrate";
                    go.transform.SetParent(_root, false);
                    go.transform.position = position + Vector3.up * 0.5f;
                    go.transform.localScale = Vector3.one;
                    go.GetComponent<Renderer>().sharedMaterial =
                        FairytaleArt.Material("EditableCrate", Color.white, "WoodPlanks");
                    break;
            }
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var editable = go.AddComponent<NetworkEditableObject>();
            editable.objectId = id;
            editable.prefabKind = kind;
            _objects[id] = editable;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.MessageReceived -= OnNetworkMessage;
        }
    }
}
