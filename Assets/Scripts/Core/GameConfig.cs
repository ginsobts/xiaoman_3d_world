namespace VN
{
    /// <summary>
    /// 全局游戏配置（编译期常量，改完重新编译即可生效）。
    /// </summary>
    public static class GameConfig
    {
        /// <summary>
        /// true：主菜单「开始游戏」进入 3D 探索世界，AVG 对话作为叠加层在世界里触发。
        /// false：沿用旧版纯 AVG 流程（开场提示 -> story.json 主线）。
        /// </summary>
        public static readonly bool Use3DWorld = true;

        /// <summary>
        /// 3D 项目默认直接进入可移动的世界，不再让旧 AVG / 桌宠存档决定启动界面。
        /// 改为 false 才会先显示新项目主菜单。
        /// </summary>
        public static readonly bool EnterWorldImmediately = true;

        /// <summary>3D 世界默认使用的剧本文件（StreamingAssets 下；英文自动找 *_en.json）。</summary>
        public const string WorldStoryFile = "world.json";
    }
}
