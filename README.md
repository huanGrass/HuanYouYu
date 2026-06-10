# 幻游域

幻游域 是一个基于 Unity / 团结引擎开发的小游戏合集项目，面向微信小游戏平台。

项目以统一的大厅入口组织多个轻量级休闲小游戏，包含关卡、资源、界面与运行时逻辑等内容，目标是在一个工程中提供可扩展、易维护的小游戏体验。

除了作为可游玩的小游戏合集，本项目中各类常见玩法的实现，也可以为开发者提供现成的结构与实现参考。

## 项目说明

本项目希望打造一个无广告、纯净体验的微信小游戏合集，让玩家可以直接游玩，不被广告、付费点或复杂账号系统打断。

项目没有收益，因此没有额外付费接入服务器，整体设计会尽量避免依赖服务端能力。

本项目中的代码全部由 AI 编写，作者负责需求设计、效果验收、测试验证、版本整合与上线发布。

## 在线游玩

本项目已经上线为微信小游戏，欢迎使用微信扫描下方二维码随时游玩。

![幻游域微信小游戏二维码](./wechat-minigame-qrcode.jpg)

作者B站视频，游戏上线时刻：[https://www.bilibili.com/video/BV1F3QjBrEYY](https://www.bilibili.com/video/BV1F3QjBrEYY)

版本更新公告见：[CHANGELOG.md](./CHANGELOG.md)

## 开发环境

- 编辑器：团结引擎 1.8.3
- 目标平台：微信小游戏

## 已有游戏

当前项目已包含以下小游戏：

- 经典连连看（ClassicLink）
- 蔬果消消乐（Match3）
- 贪吃蛇（Snake）
- 俄罗斯方块（Tetris）
- 2048（2048）
- 合成大西瓜（WatermelonMerge）
- 水排序（WaterSort）
- 记忆翻牌（MemoryFlip）
- 扫雷（Minesweeper）
- 数独（Sudoku）
- 数织（Nonogram）
- 五子棋（Gomoku）
- 黑白棋（reversi）
- 经典打砖块（Breakout）
- 钩爪拾宝（GoldMiner）
- 见缝插针（NeedleHit）
- AI星露谷（StardewAI）
- 占点攻防（ControlPoint）
- 数学电灯（LightsOut）
- 点灯谜题（Akari）
- 叠牌消消（StackMatch）
- 方块消除（BlockPuzzle）
- 猜数字（BullsCows）
- 箭头迷阵（ArrowEscape）
- 数字华容道（SlidingPuzzle）
- 跳一跳（JumpJump）
- 过河问题（RiverCrossing）
- 汉诺塔（TowerOfHanoi）
- 倒水量杯（WaterPouring）
- 打地鼠（WhacAMole）

## 参与贡献

如果你也喜欢这个项目，欢迎以 Issue、建议、修复问题或补充小游戏内容等方式参与。也可以直接通过 Issue 提供希望新增的玩法想法。提交的改动会由作者先在本地和测试环境中进行验证；确认稳定且适合当前版本后，会合入项目，并在后续版本中提交上线。

被采用的贡献者名字会添加到游戏说明中。

## 新增玩法规范

新增子游戏时，请尽量保持改动边界清晰，避免影响已有玩法和公共框架：

- 新增子游戏的代码、资源、测试等内容应放在 `Assets/Games/xxx` 目录下，其中 `xxx` 为该玩法的唯一英文目录名。
- 新增子游戏原则上只能修改自己的 `Assets/Games/xxx` 目录。
- 如确实需要修改大厅入口、公共组件、公共资源、项目设置或构建配置，请先通过 Issue 或 Pull Request 说明原因，由作者确认后统一调整。
- 微信小游戏包体需要控制在 30MB 以内，请尽量复用现有资源，避免新增美术资源导致包体膨胀。

## 许可证

本项目采用 MIT License 开源，详见 [LICENSE](./LICENSE)。
