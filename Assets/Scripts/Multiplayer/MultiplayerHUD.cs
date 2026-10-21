using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VN.World;

namespace VN.Multiplayer
{
    /// <summary>F2 打开的联机面板。支持本机 Host、直连 IP 与局域网发现。</summary>
    public class MultiplayerHUD : MonoBehaviour
    {
        private MultiplayerSession _session;
        private LocalMultiplayerManager _players;
        private WorldController _world;
        private RectTransform _panel;
        private RectTransform _offerList;
        private InputField _address;
        private InputField _port;
        private Text _status;
        private Text _compact;
        private readonly HashSet<string> _offers = new HashSet<string>();

        public static MultiplayerHUD Create(RectTransform parent, WorldController world,
            MultiplayerSession session, LocalMultiplayerManager players)
        {
            var go = UITheme.NewUIObject("MultiplayerHUD", parent);
            UITheme.FullStretch(go.GetComponent<RectTransform>());
            var hud = go.AddComponent<MultiplayerHUD>();
            hud._world = world;
            hud._session = session;
            hud._players = players;
            hud.Build();
            session.HostDiscovered += hud.AddOffer;
            return hud;
        }

        private void Build()
        {
            _compact = UITheme.AddText("NetworkStatus", transform,
                "离线空间 · F2 联机", 22, Color.white, TextAnchor.UpperRight);
            UITheme.AddOutline(_compact, new Color(0f, 0f, 0f, 0.8f), new Vector2(1.5f, -1.5f));
            UITheme.SetRect(_compact.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-520, -64), new Vector2(-24, -24));

            var image = UITheme.AddPanel("NetworkLobby", transform, new Color(0.96f, 0.91f, 0.79f, 0.98f));
            _panel = image.rectTransform;
            UITheme.SetRect(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-420, -330), new Vector2(420, 330));

            var title = UITheme.AddText("Title", _panel, "联机童话空间", 42,
                new Color(0.25f, 0.22f, 0.31f), TextAnchor.MiddleCenter);
            UITheme.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(30, -85), new Vector2(-30, -20));

            _status = UITheme.AddText("Status", _panel, "", 24,
                new Color(0.25f, 0.22f, 0.31f), TextAnchor.UpperLeft);
            UITheme.SetRect(_status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(50, -190), new Vector2(-50, -100));

            AddButton("Host", "创建空间（主机）", 50, 350, () =>
            {
                _session.StartHost(ParsePort(), "Xiaoman Space");
                _world.Hud.Toast("空间已创建，其他玩家可通过局域网搜索加入");
            });
            AddButton("Search", "搜索局域网", 440, 350, () =>
            {
                _offers.Clear();
                foreach (Transform child in _offerList) Destroy(child.gameObject);
                _session.DiscoverLan(ParsePort());
            });
            AddButton("Join", "直连加入", 50, 235, () =>
            {
                _session.StartClient(string.IsNullOrWhiteSpace(_address.text) ? "127.0.0.1" : _address.text.Trim(),
                    ParsePort(), "Player");
            });
            AddButton("Stop", "断开 / 离线", 440, 235, _session.StopSession);

            _address = AddInput("Address", "IP / Tailscale 地址", 50, 285, 350);
            _port = AddInput("Port", MultiplayerProtocol.DefaultPort.ToString(), 615, 285, 150);

            var local = UITheme.AddText("LocalHelp", _panel,
                "同屏加入：Enter = 键盘2号玩家，手柄 Start = 手柄玩家\n" +
                "远程无服务器：推荐 Tailscale/ZeroTier，或给主机转发 UDP 7777 端口\n" +
                "进入游戏后：Q/手柄Y 演奏，B 开关共享建造模式", 21,
                new Color(0.31f, 0.27f, 0.35f), TextAnchor.UpperLeft);
            UITheme.SetRect(local.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(50, 38), new Vector2(-50, 150));

            var offersTitle = UITheme.AddText("OffersTitle", _panel, "发现的空间：", 22,
                new Color(0.25f, 0.22f, 0.31f), TextAnchor.MiddleLeft);
            UITheme.SetRect(offersTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(50, 170), new Vector2(220, 215));
            _offerList = UITheme.NewUIObject("Offers", _panel).GetComponent<RectTransform>();
            UITheme.SetRect(_offerList, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(220, 168), new Vector2(-50, 215));

            _panel.gameObject.SetActive(false);
        }

        private void AddButton(string name, string label, float x, float y, UnityEngine.Events.UnityAction action)
        {
            var btn = UITheme.AddButton(name, _panel, label, 22, action);
            UITheme.SetRect(btn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(x, y), new Vector2(x + 325, y + 65));
        }

        private InputField AddInput(string name, string placeholder, float x, float y, float width)
        {
            var bg = UITheme.AddImage(name, _panel, new Color(1f, 1f, 1f, 0.78f));
            UITheme.SetRect(bg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(x, y), new Vector2(x + width, y + 50));
            var text = UITheme.AddText("Text", bg.transform, "", 22, Color.black, TextAnchor.MiddleLeft);
            UITheme.FullStretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(12, 3);
            text.rectTransform.offsetMax = new Vector2(-12, -3);
            var input = bg.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.text = name == "Port" ? placeholder : "";
            var hint = UITheme.AddText("Placeholder", bg.transform, placeholder, 20,
                new Color(0.35f, 0.35f, 0.35f, 0.7f), TextAnchor.MiddleLeft);
            UITheme.FullStretch(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(12, 3);
            hint.rectTransform.offsetMax = new Vector2(-12, -3);
            input.placeholder = hint;
            return input;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                bool show = !_panel.gameObject.activeSelf;
                _panel.gameObject.SetActive(show);
                _players.SetGameplayEnabled(!show && _world.IsExploring);
                Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = show;
            }

            string mode = _session.Mode == SessionMode.Offline ? "离线"
                : _session.Mode == SessionMode.Host ? "主机"
                : _session.Mode == SessionMode.Client ? "已加入"
                : "搜索中";
            _compact.text = $"{mode}空间 · 本地玩家 {_players.LocalPlayers.Count} · F2 联机";
            if (_status != null)
                _status.text = $"状态：{mode}\n{_session.StatusText}\n本机玩家：{_players.LocalPlayers.Count}";
        }

        private int ParsePort()
        {
            return _port != null && int.TryParse(_port.text, out int value)
                ? Mathf.Clamp(value, 1024, 65535)
                : MultiplayerProtocol.DefaultPort;
        }

        private void AddOffer(string address, string roomName)
        {
            string key = address + ":" + ParsePort();
            if (!_offers.Add(key)) return;
            float x = (_offers.Count - 1) * 180f;
            var btn = UITheme.AddButton("Offer", _offerList,
                string.IsNullOrEmpty(roomName) ? key : roomName, 18, () =>
            {
                _address.text = address;
                _session.StartClient(address, ParsePort(), "Player");
            });
            UITheme.SetRect(btn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(x, 0), new Vector2(x + 165, 45));
        }

        private void OnDestroy()
        {
            if (_session != null) _session.HostDiscovered -= AddOffer;
        }
    }
}
