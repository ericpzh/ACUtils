# 安装
- 下载[BepInEx](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip)
  - 非win64下载请到: https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2
- 解压并将所有文件丢到游戏根目录 `AC2025_0.1.1_win64/`
- 运行一次游戏，游戏打开后退出（有概率触发windows defender）
- 将[Utils.dll](https://github.com/ericpzh/ACUtils/blob/main/bin/Debug/netstandard2.1/Utils.dll)丢进 `AC2025_0.1.1_win64/BepInEx/plugins`
  
# 功能
- 倍速调整
  - `空格`：暂停
  - `Z`: 1倍速
  - `X`：2倍速
  - `C`：4倍速
  - `V`: 8倍速
  - `B`：16倍速
  - 非一倍速下将不再播放无线电通信。
- 降落跑道选择
  - `左SHIFT + L`: 全部刷在36L
  - `左SHIFT + R`: 全部刷在36R
  - `左SHIFT + C`: 默认两边都刷
- 航班刷新时间调整
  - `左Control + 1`：默认离场刷新速度
  - `左Control + 2~9`：2~9倍离场刷新速度
  - `左Control + 0`：10倍离场刷新速度
  - `左Alt + 1`：默认进场刷新速度
  - `左Alt + 2~9`：2~9倍进场刷新速度
  - `左Alt + 0`：10倍进场刷新速度
- 静音
  - 按`M`切换是否播放无线电通信。非一倍速下锁定不播放无线电。