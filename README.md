# OtpBridge

OtpBridge 是一个 Windows 本地验证码桥接工具。iPhone 收到验证码短信后，通过 iOS「快捷指令」把短信正文发送到 Windows 电脑；OtpBridge 会提取验证码、复制到剪贴板，并在右下角弹出提示。

它不依赖微软「连接手机」、Phone Link 或任何微软官方手机同步软件。

## 功能

- Windows 客户端：C#、.NET 8、WPF。
- 内置轻量 HTTP 服务：默认监听 `0.0.0.0:18080`。
- 接口：`POST /api/sms`，使用 Bearer Token 校验。
- 自动提取 4 到 8 位数字或字母验证码。
- 可自动复制验证码到剪贴板。
- 可弹出 Windows 右下角通知，并带托盘提示兜底。
- 托盘常驻，右键可打开设置、复制最近验证码、查看记录、勾选开机自启动、退出。
- 默认开机自启动，登录 Windows 后最小化到托盘。
- 单实例运行：如果已经在托盘运行，再次双击不会再启动第二个进程。
- 如果默认端口被占用，会自动改用下一个可用端口，并在主窗口显示新的监听地址。
- 最近记录只保存在内存中，默认不把短信全文写入磁盘。
- 配置文件保存到：`%AppData%\OtpBridge\config.json`。

## 推荐使用方式

### 普通用户：使用便携版

发布后给用户一个压缩包：

```text
OtpBridge-Portable-win-x64.zip
```

用户这样使用：

1. 解压到一个固定位置，例如 `D:\Apps\OtpBridge`。
2. 双击 `OtpBridge.exe`。
3. 第一次运行时，如果 Windows 防火墙弹窗，允许「专用网络」访问。
4. 按主窗口里的教程配置 iPhone 快捷指令。

便携版不需要安装。建议不要频繁移动 `OtpBridge.exe` 的位置；如果移动了，请重新打开程序并确认「开机自启动」仍然开启。

### 开发者：从源码生成 exe

在项目根目录运行：

```powershell
.\publish-win-x64.cmd
```

生成结果在：

```text
dist\OtpBridge\OtpBridge.exe
dist\OtpBridge-Portable-win-x64.zip
```

`dist\OtpBridge\OtpBridge.exe` 是自包含单文件 exe。正常情况下，用户双击这个 exe 就能运行，不需要额外安装 .NET。

开发调试时也可以运行：

```powershell
dotnet run --project .\OtpBridge\OtpBridge.csproj
```

### 清理历史构建文件

如果想清理旧的 `bin`、`obj`、`dist`、临时发布目录，运行：

```powershell
.\clean-generated.cmd
```

这个脚本只删除生成物，不会删除源码，也不会删除 `%AppData%\OtpBridge` 里的用户配置。

## 第一次打开 Windows 客户端

主窗口会显示：

- 监听地址，例如 `http://192.168.1.20:18080/api/sms`
- API Token
- iPhone 快捷指令配置教程
- 最近收到的验证码记录

在 iPhone 里要填写主窗口显示的局域网地址。不要填写：

```text
http://0.0.0.0:18080/api/sms
```

如果窗口里出现多个地址，优先使用与你手机同一个 Wi-Fi 网段的地址，常见格式是：

```text
http://192.168.x.x:18080/api/sms
http://10.x.x.x:18080/api/sms
http://172.16.x.x:18080/api/sms
```

其中 `172.16.x.x` 到 `172.31.x.x` 都属于常见局域网地址范围。

如果主窗口提示「端口 18080 被占用，已自动改用 0.0.0.0:18081」之类的信息，请直接使用主窗口显示的新地址，例如：

```text
http://192.168.1.20:18081/api/sms
```

不要手动猜端口，以窗口里显示的地址为准。

配置前可以先在 iPhone Safari 打开：

```text
http://<Windows局域网IP>:18080/health
```

如果看到：

```json
{"ok":true}
```

说明 iPhone 已经能访问 Windows 上的 OtpBridge。

## iPhone 快捷指令配置步骤

下面是给第一次使用快捷指令的用户看的详细步骤。不同 iOS 版本的文字可能略有差异，但要填的内容相同。

### 1. 创建短信自动化

1. 打开 iPhone「快捷指令」App。
2. 点底部「自动化」。
3. 新建自动化，选择「收到信息」。
4. 条件建议设置为：我收到包含「验证码」的信息。
5. 也可以再创建几条条件，例如包含 `code`、`OTP`。
6. 进入动作编辑页后，顶部应能看到类似「作为输入接收信息」的提示。

### 2. 添加「获取 URL 内容」

在动作搜索框搜索并添加：

```text
获取 URL 内容
```

填写：

```text
URL: http://<Windows局域网IP>:18080/api/sms
方法: POST
```

这里的 URL 直接复制 OtpBridge 主窗口里的监听地址即可。

### 3. 填写头部 Header

展开「头部」，添加两行：

```text
Authorization    Bearer <主窗口里的 API Token>
Content-Type     application/json
```

注意：`Bearer` 后面必须有一个空格。

例如：

```text
Authorization    Bearer abcdefg123456
```

### 4. 填写请求体 JSON

在「请求体」里选择：

```text
JSON
```

添加一个必填字段：

```text
键: message
值: 输入快捷指令的信息
```

这里的「输入快捷指令的信息」就是自动化收到的短信正文。它通常会以蓝色变量块的形式出现。你也可能看到类似「收到的信息正文」「快捷指令输入」的变量，选择能代表短信全文的那个。

可选字段可以不填：

```text
sender
receivedAt
```

MVP 只需要 `message` 字段。

### 5. 先用固定文本测试

如果不确定短信变量是否选对，可以先把 `message` 的值临时改成固定文本：

```text
【测试】您的验证码是 123456，5分钟内有效
```

手动运行快捷指令后，Windows 应该会：

- 右下角弹出提示。
- 剪贴板变成 `123456`。
- 主窗口「最近记录」新增一行。

测试成功后，再把 `message` 的值改回「输入快捷指令的信息」。

## 以后重启还需要重新配置 iPhone 吗？

通常不需要。

iOS 快捷指令长期有效的前提是：

- Windows 局域网 IP 不变。
- 监听端口不变，默认 `18080`。
- API Token 不变。

OtpBridge 会把设置保存在：

```text
%AppData%\OtpBridge\config.json
```

开机自启动默认开启。Windows 登录后，OtpBridge 会自动启动并最小化到托盘，iPhone 可以继续使用原来的 URL 和 Token。

需要重新修改 iPhone 快捷指令的情况：

- Windows 局域网 IP 变了。
- 你修改了监听端口。
- 你重新生成了 API Token。
- 你把 exe 移动到别的位置，并且旧的开机自启动路径失效。

想尽量「配置一次长期有效」，建议在路由器里给 Windows 电脑设置 DHCP 地址保留，或者给电脑设置固定局域网 IP。

## 设置说明

| 设置项 | 默认值 | 说明 |
| --- | --- | --- |
| 监听端口 | `18080` | 修改后会自动重启本地 HTTP 服务；如果端口被占用，会自动改用后续可用端口。 |
| API Token | 自动生成 | iPhone 请求必须带 `Authorization: Bearer <token>`。 |
| 自动复制 | 开启 | 收到验证码后写入剪贴板。 |
| 右下角通知 | 开启 | 收到验证码后弹出 Windows 通知；Toast 不可用时用托盘提示兜底。 |
| 开机自启动 | 开启 | 当前用户登录 Windows 后自动启动并最小化到托盘。 |
| 最近记录数量 | `20` | 只保存在内存里，不写短信全文。 |
| 自定义正则 | 空 | 特殊短信格式可填写。若有捕获组，优先使用最后一个非空捕获组。 |

## 托盘菜单

右键系统托盘里的 OtpBridge 图标，可以：

- 打开设置
- 复制最近验证码
- 查看最近记录
- 勾选或取消「开机自启动」
- 退出

关闭主窗口不会退出程序，只会隐藏到托盘。要真正退出，请右键托盘图标选择「退出」。

## API

### `POST /api/sms`

Headers:

```text
Authorization: Bearer <token>
Content-Type: application/json
```

Request:

```json
{
  "message": "【某服务】您的验证码是 123456，5分钟内有效",
  "sender": "optional",
  "receivedAt": "optional"
}
```

成功：

```json
{
  "ok": true,
  "code": "123456"
}
```

失败：

```json
{
  "ok": false,
  "error": "reason"
}
```

健康检查：

```text
GET /health
```

## 验证码提取规则

优先匹配关键词附近的验证码：

```text
验证码、校验码、动态码、code、otp
```

如果没有命中，再兜底查找 4 到 8 位、至少包含一个数字的字母数字串。程序会尽量避开手机号、年份、金额、订单号、账号尾号等明显不是验证码的数字。

特殊短信格式可以在设置里填写自定义正则，例如：

```regex
安全码[:：]\s*([A-Z0-9]{6})
```

## 常见问题

### iPhone 访问失败

优先检查：

- iPhone 和 Windows 是否在同一个 Wi-Fi 或同一个局域网。
- iPhone Safari 是否能打开 `http://<Windows局域网IP>:18080/health`。
- Windows 防火墙是否允许 OtpBridge 访问「专用网络」。
- iOS 快捷指令里是否填了正确 URL，而不是 `0.0.0.0`。
- 如果程序自动换了端口，iOS 快捷指令里的 URL 是否也换成主窗口显示的新地址。

### 显示端口被占用

这通常有两种情况：

- OtpBridge 已经在右下角托盘运行，你又双击打开了第二次。新版会拦截第二个实例，并提示去托盘查看。
- 电脑上其他程序正在使用 `18080`。新版会自动改用后续可用端口，例如 `18081`，并保存到配置文件。iOS 快捷指令需要使用主窗口显示的新地址。

### 返回 `unauthorized`

Token 不匹配。重新复制主窗口当前 Token，确认 Header 是：

```text
Authorization: Bearer <token>
```

`Bearer` 后面要有一个空格。

### 返回 `code not found`

Windows 收到了短信正文，但没有提取到验证码。请确认 `message` 字段是短信全文，或者在设置里填写自定义正则。

### 没有右下角通知

确认设置里的「右下角通知」已开启。程序会先弹托盘提示，并尝试 Windows Toast。还需要检查 Windows 系统通知是否被关闭、勿扰模式是否开启。

### 构建时报错或旧文件残留

先从托盘退出 OtpBridge，再运行：

```powershell
.\clean-generated.cmd
.\publish-win-x64.cmd
```
