# 轻译（QingYi）

[![Windows build](https://github.com/kodaihaoshen-chn/qingyi/actions/workflows/build.yml/badge.svg)](https://github.com/kodaihaoshen-chn/qingyi/actions/workflows/build.yml)

一个小巧、启动迅速的 Windows 中英文互译与本地朗读工具。

## 功能

- 中文、英文自动判断并互译
- 按 Enter 或点击“翻译”发起请求
- 原文、译文均可调用 Windows 本地语音朗读一次
- 手动复制译文
- 可切换窗口置顶
- 最多输入 200 个字符，定位于单词、短语和短句
- 不保存翻译历史
- 百度翻译凭据通过 Windows DPAPI 加密保存
- 关闭窗口即完全退出，不驻留后台

## 下载

从 [Releases](https://github.com/kodaihaoshen-chn/qingyi/releases/latest) 下载最新版 `轻译.exe`。

系统要求：Windows 10/11 64 位。

## 首次配置

轻译使用百度“通用文本翻译 API”。首次运行时需要填写自己的 `APP ID` 和密钥：

1. 登录[百度翻译开放平台](https://fanyi-api.baidu.com/)。
2. 开通“通用文本翻译 API”。
3. 在“管理控制台 → 开发者信息”查看 `APP ID` 和密钥。
4. 将两项凭据填入轻译的设置窗口并保存。

请勿填写“大模型文本翻译 API”的 API Key。轻译当前使用的是通用文本翻译接口。

## 隐私边界

- 只有按 Enter 或点击“翻译”时，输入内容才会发送至百度翻译 API。
- 翻译历史、原文和译文不会写入本地文件。
- 本地朗读不上传语音或文字。
- 凭据保存在 `%LOCALAPPDATA%\QingYi\credentials.dat`，由当前 Windows 用户账户加密。
- 仓库及发布包不包含任何用户凭据。

## 从源码构建

项目使用 Windows 自带的 .NET Framework、WinForms、DPAPI 和 `System.Speech`，不依赖 Node.js、Python 或浏览器插件。

```powershell
.\build.ps1
```

运行自动检查：

```powershell
.\build.ps1 -IncludeTests
.\轻译-测试.exe
```

当前版本包含 24 项自动检查，覆盖语言方向、200 字符边界、百度签名、响应解析、错误处理、DPAPI 加密往返和关键界面行为。

## 已知边界

- 仅支持中英文。
- 只展示翻译 API 返回的直接译文，不提供音标、词性、例句或词典模式。
- 需要互联网连接和用户自己的百度翻译凭据。
- 个人构建未使用商业代码签名证书，Windows 可能显示“未知发布者”。

## License

[MIT](LICENSE)
