using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 关卡根节点标记。场景里有这个组件时，WorldController 会把它当作当前关卡：
    ///  - spawnPoint：新游戏出生点（空 = 自身位置）
    ///  - autoBuildPrototype：当根节点下没有任何子物体时，运行时自动用 LevelBuilder 搭一个原型关卡
    /// 场景里没有 WorldLevel 时，WorldController 会自动创建一个并搭原型关卡。
    /// </summary>
    public class WorldLevel : MonoBehaviour
    {
        public string levelId = "prototype";
        public Transform spawnPoint;
        public bool autoBuildPrototype = true;

        public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
        public float SpawnYaw => spawnPoint != null ? spawnPoint.eulerAngles.y : transform.eulerAngles.y;
    }
}
