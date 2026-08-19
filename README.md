# OpenClaw 控制中心（悬浮球）🖥️

Windows 桌面小工具：**OpenClaw 网关状态 + DeepSeek 用量监控**，单 exe 免安装。

- 桌面 ¥ 悬浮球：实时显示 **余额 / 今日 Token / 今日消费** 三项数据
- 控制中心面板：网关运行状态（TCP 端口探测）、地址、当前模型识别、三项用量、开机自启开关
- 特效按钮：hover 变亮 + 按下变暗（自定义 RoundedBtn 控件）
- 圆角深色 UI，半透明悬浮球可拖动

## 截图

（待补充）

## 编译

环境：Windows + .NET Framework（自带 csc，无需额外安装）

```bat
csc /nologo /target:winexe /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:ds_control.exe ds_float.cs
```

产出 `ds_control.exe`，复制到桌面即用。

## 配置

API Key 自动获取，优先级从高到低：

1. `~/.openclaw/models.json` 里的 provider key
2. `~/.openclaw/openclaw.json` 里的 provider key
3. 环境变量（`DEEPSEEK_API_KEY` 等）
4. 同目录 `ds_key.conf`（纯文本存 key，兜底用）

> 🔒 `ds_key.conf` 含真实密钥，已被 `.gitignore` 排除，**切勿提交到 GitHub**。

## 使用

- 双击悬浮球 → 弹出用量卡片（余额黄绿高亮，含官网链接）
- 右键悬浮球 / 控制中心按钮 → 各项操作
- 控制中心「切换悬浮球」开关 → 控制悬浮球显隐

## License

MIT
