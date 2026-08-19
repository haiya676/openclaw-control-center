# Changelog

## v1.0.2 (2026-08-19) — 实时模型识别补丁

### 新增
- **实时模型检测**：每 20 秒调用 `openclaw status --json` 查询网关，显示「最近活跃会话」实际在用的模型
- **会话级切换识别**：在聊天里用 `/model` 切换到 Agnes 等模型后，控制中心自动跟随显示（此前只读配置文件默认模型，永远显示 DeepSeek）
- **模型显示名映射**：优先显示配置里的别名（如 `Agnes 2.0 Flash`），读不到别名时回退为原始模型 ID

### 修复
- Windows 上 `openclaw` 实际是 `.ps1` shim，无法直接拉起 → 改为从 `~/.openclaw/gateway.cmd` 解析真实 `node.exe` + `dist/index.js` 路径再调用
- 子进程输出超过 4KB 匿名管道缓冲导致读取死锁（6 秒超时被误杀）→ 改为异步读取 stdout/stderr

## v1.0.1 (2026-08-19) — i18n + 首次配置

- i18n 自动语言识别：中文系统 → 中文界面，其他 → 英文界面
- 首次运行 API Key 输入对话框（自动保存为 `ds_key.conf`，便携免配置）
- 修复控制中心余额 / 今日 Token / 今日花费三项显示（`RefreshData` 漏调 `mainForm.UpdateUsage`）
- README 增加免责声明与已知问题说明

## v1.0.0 (2026-08-19) — 首发

- OpenClaw 控制中心合并版：网关状态（TCP 探测）/ 地址 / 模型识别 + 余额 / 今日 Token / 今日消费
- ¥ 悬浮球：hover 放大 + 外发光、可拖动、点击弹出三项数据卡片
- 特效按钮：hover 变亮 + 按下变暗（自定义 `RoundedBtn` 控件）
- 开机自启开关、网关启停、模型 → 官网快捷跳转
