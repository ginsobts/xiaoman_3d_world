using UnityEngine;

namespace VN.Multiplayer
{
    /// <summary>运行时建造物标记；objectId 由 Host 分配，所有客户端一致。</summary>
    public class NetworkEditableObject : MonoBehaviour
    {
        public int objectId;
        public string prefabKind;
        public bool hostProtected;
    }
}
