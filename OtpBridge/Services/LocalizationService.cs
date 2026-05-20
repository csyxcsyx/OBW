namespace OtpBridge.Services;

public sealed record LanguageOption(string Code, string DisplayName);

public static class LocalizationService
{
    public const string Chinese = "zh-CN";
    public const string English = "en-US";

    private static readonly IReadOnlyDictionary<string, string> ChineseTexts = new Dictionary<string, string>
    {
        ["App.StartFailed"] = "OtpBridge 启动失败，详细日志：{0}{1}{1}{2}",
        ["App.AlreadyRunning"] = "OtpBridge 已经在运行。请查看右下角系统托盘里的 OtpBridge 图标；如需关闭，请右键托盘图标选择“退出”。",
        ["App.UnhandledDispatcher"] = "OtpBridge 发生未处理异常，详细日志：{0}{1}{1}{2}",

        ["Main.Status.Starting"] = "正在启动...",
        ["Main.Status.Listening"] = "已监听 0.0.0.0:{0}",
        ["Main.Status.StartingListen"] = "正在监听 0.0.0.0:{0}...",
        ["Main.Status.PortChanged"] = "端口 {0} 被占用，已自动改用 0.0.0.0:{1}",
        ["Main.Status.ListenFailed"] = "监听失败：{0}",
        ["Main.Tray.Starting"] = "OtpBridge 正在启动",
        ["Main.Tray.Listening"] = "OtpBridge 已监听 {0}",
        ["Main.Tray.ListenFailed"] = "OtpBridge 监听失败",
        ["Main.PortChanged.Notification"] = "端口 {0} 被占用，已改用 {1}。iPhone 快捷指令请使用新的监听地址。",
        ["Main.Recent.Empty"] = "尚未收到验证码。",
        ["Main.Recent.Summary"] = "内存中保留最近 {0} 条，设置上限 {1} 条。",
        ["Main.Toast.Disabled"] = "未启用",
        ["Main.Toast.Shown"] = "已弹出",
        ["Main.Toast.ReceivedCopied"] = "收到验证码 {0}，已复制。",
        ["Main.Toast.Received"] = "收到验证码 {0}。",
        ["Main.Copy.NoRecent"] = "暂无最近验证码。",
        ["Main.Copy.RecentCopied"] = "已复制最近验证码 {0}。",
        ["Main.Copy.Failed"] = "复制失败，请稍后重试。",
        ["Main.Copy.AddressCopied"] = "已复制监听地址。",

        ["Main.Tray.OpenSettings"] = "打开设置",
        ["Main.Tray.CopyRecent"] = "复制最近验证码",
        ["Main.Tray.ViewRecent"] = "查看最近记录",
        ["Main.Tray.StartWithWindows"] = "开机自启动",
        ["Main.Tray.Exit"] = "退出",

        ["Main.StatusSuffix"] = "  ·  托盘常驻，收到验证码后自动复制",
        ["Main.Button.OpenSettings"] = "打开设置",
        ["Main.Button.CopyAddress"] = "复制地址",
        ["Main.Button.CopyRecent"] = "复制最近验证码",
        ["Main.Connection.Title"] = "连接信息",
        ["Main.Connection.LanReminder"] = "重要提醒：iPhone 和 Windows 电脑必须在同一个局域网内，否则快捷指令无法访问本程序。",
        ["Main.Connection.Description"] = "iPhone 快捷指令会把短信内容发送到这里。请优先复制 10.x、172.x、192.168.x 这类局域网地址；如果默认端口被占用，程序会自动换成可用端口，请以这里显示的地址为准。",
        ["Main.Address.Label"] = "监听地址",
        ["Main.Token.Title"] = "API Token",
        ["Main.Token.Description"] = "这是 iPhone 请求的通行口令。不要发给不信任的人，重新生成后需要同步更新快捷指令。",
        ["Main.Button.CopyToken"] = "复制 Token",
        ["Main.Guide.Title"] = "iPhone 快捷指令配置教程",
        ["Main.Guide.Description"] = "下面是给第一次使用快捷指令的用户看的详细步骤。不同 iOS 版本的文字可能略有差异，但要填的内容相同。",
        ["Main.Guide.Step1.Title"] = "创建短信自动化",
        ["Main.Guide.Step1.Body"] = "打开 iPhone「快捷指令」App，点底部「自动化」，新建自动化并选择「收到信息」。条件建议设置为：我收到包含「验证码」的信息；然后选择「立即运行」。点击「下一步」后，选择「创建新快捷指令」。",
        ["Main.Guide.Step2.Title"] = "添加「获取 URL 内容」",
        ["Main.Guide.Step2.Body"] = "在动作搜索框搜索并添加「获取 URL 内容」。URL 填 http://XXXXXXX:18080/api/sms，也就是直接复制主窗口里的监听地址；方法选择 POST。",
        ["Main.Guide.Step3.Title"] = "填写头部 Header",
        ["Main.Guide.Step3.Body"] = "展开「头部」，添加两行：（键） Authorization，（文本） Bearer <主窗口里的 API Token>；（键） Content-Type，（文本） application/json。注意：Bearer 后面必须有一个空格，例如：Bearer abcdefg123456。",
        ["Main.Guide.Step4.Title"] = "填写请求体 JSON",
        ["Main.Guide.Step4.Body"] = "在「请求体」里选择 JSON。添加一个必填字段：（键） message，文本选择“输入快捷指令的信息”（点击「文本框」后，在输入框上方左右滑动找到“输入快捷指令的信息”并点击）。",
        ["Main.Guide.Step5.Title"] = "先用固定文本测试",
        ["Main.Guide.Step5.Body"] = "如果不确定短信变量是否选对，可以先把 message 的值临时改成固定文本：【测试】您的验证码是 123456，5分钟内有效。手动运行快捷指令后，Windows 应该会弹出提示、剪贴板变成 123456、最近记录新增一行。测试成功后，再把 message 的值改回“输入快捷指令的信息”。",
        ["Main.Recent.Title"] = "最近记录",
        ["Main.Button.ClearRecords"] = "清空记录",
        ["Main.Grid.Time"] = "时间",
        ["Main.Grid.Sender"] = "发送方",
        ["Main.Grid.Code"] = "验证码",
        ["Main.Grid.Copied"] = "复制状态",
        ["Main.Grid.Toast"] = "通知",
        ["Main.Record.Copied"] = "已复制",
        ["Main.Record.NotCopied"] = "未复制",

        ["Settings.WindowTitle"] = "OtpBridge 设置",
        ["Settings.Title"] = "设置",
        ["Settings.Subtitle"] = "只保存配置，不保存短信全文。端口或 Token 改动后，需要同步更新 iPhone 快捷指令。",
        ["Settings.Button.Save"] = "保存",
        ["Settings.Button.Cancel"] = "取消",
        ["Settings.ConfigPath"] = "设置文件",
        ["Settings.Language"] = "界面语言",
        ["Settings.Language.Hint"] = "默认中文。保存后，主窗口、设置、托盘和提示文字会使用所选语言。",
        ["Settings.Port"] = "监听端口",
        ["Settings.Port.Hint"] = "默认 18080。若端口已被其他程序占用，OtpBridge 会自动改用下一个可用端口，并在主窗口显示新地址。",
        ["Settings.Token"] = "API Token",
        ["Settings.GenerateToken"] = "自动生成",
        ["Settings.Token.Hint"] = "重新生成后，iPhone 快捷指令里的 Authorization 也要改成新的 Bearer Token。",
        ["Settings.AutoCopy"] = "自动复制",
        ["Settings.AutoCopy.Content"] = "收到验证码后写入剪贴板",
        ["Settings.ShowToast"] = "右下角通知",
        ["Settings.ShowToast.Content"] = "收到验证码后弹出 Windows 提示",
        ["Settings.StartWithWindows"] = "开机自启动",
        ["Settings.StartWithWindows.Content"] = "登录 Windows 后自动启动并最小化到托盘",
        ["Settings.RecentCount"] = "最近记录数量",
        ["Settings.RecentCount.Hint"] = "只影响内存里的最近记录，不会把短信全文写入磁盘。",
        ["Settings.CustomRegex"] = "自定义正则",
        ["Settings.CustomRegex.Hint"] = "可留空。若包含捕获组，将优先使用最后一个非空捕获组作为验证码。",
        ["Settings.Error.Title"] = "OtpBridge 设置",
        ["Settings.Error.Port"] = "监听端口必须是 1 到 65535 之间的数字。",
        ["Settings.Error.Token"] = "API Token 不能为空。",
        ["Settings.Error.RecentCount"] = "最近记录数量必须是 1 到 {0} 之间的数字。",
        ["Settings.Error.CustomRegex"] = "自定义正则无效：{0}"
    };

    private static readonly IReadOnlyDictionary<string, string> EnglishTexts = new Dictionary<string, string>
    {
        ["App.StartFailed"] = "OtpBridge failed to start. Detailed log: {0}{1}{1}{2}",
        ["App.AlreadyRunning"] = "OtpBridge is already running. Please check the OtpBridge icon in the system tray at the bottom right. To close it, right-click the tray icon and choose \"Exit\".",
        ["App.UnhandledDispatcher"] = "OtpBridge encountered an unhandled exception. Detailed log: {0}{1}{1}{2}",

        ["Main.Status.Starting"] = "Starting...",
        ["Main.Status.Listening"] = "Listening on 0.0.0.0:{0}",
        ["Main.Status.StartingListen"] = "Starting listener on 0.0.0.0:{0}...",
        ["Main.Status.PortChanged"] = "Port {0} is in use. Automatically switched to 0.0.0.0:{1}",
        ["Main.Status.ListenFailed"] = "Listener failed: {0}",
        ["Main.Tray.Starting"] = "OtpBridge is starting",
        ["Main.Tray.Listening"] = "OtpBridge is listening on {0}",
        ["Main.Tray.ListenFailed"] = "OtpBridge listener failed",
        ["Main.PortChanged.Notification"] = "Port {0} is in use. Switched to {1}. Please update the iPhone Shortcut to use the new listening address.",
        ["Main.Recent.Empty"] = "No verification code has been received yet.",
        ["Main.Recent.Summary"] = "Keeping the latest {0} record(s) in memory. Limit: {1}.",
        ["Main.Toast.Disabled"] = "Disabled",
        ["Main.Toast.Shown"] = "Shown",
        ["Main.Toast.ReceivedCopied"] = "Verification code {0} received and copied.",
        ["Main.Toast.Received"] = "Verification code {0} received.",
        ["Main.Copy.NoRecent"] = "No recent verification code.",
        ["Main.Copy.RecentCopied"] = "Copied recent verification code {0}.",
        ["Main.Copy.Failed"] = "Copy failed. Please try again later.",
        ["Main.Copy.AddressCopied"] = "Listening address copied.",

        ["Main.Tray.OpenSettings"] = "Open Settings",
        ["Main.Tray.CopyRecent"] = "Copy Recent Code",
        ["Main.Tray.ViewRecent"] = "View Recent Records",
        ["Main.Tray.StartWithWindows"] = "Launch at Startup",
        ["Main.Tray.Exit"] = "Exit",

        ["Main.StatusSuffix"] = "  ·  Stays in the tray and copies codes automatically",
        ["Main.Button.OpenSettings"] = "Open Settings",
        ["Main.Button.CopyAddress"] = "Copy Address",
        ["Main.Button.CopyRecent"] = "Copy Recent Code",
        ["Main.Connection.Title"] = "Connection Information",
        ["Main.Connection.LanReminder"] = "Important: the iPhone and Windows PC must be on the same local network, otherwise the Shortcut cannot reach this app.",
        ["Main.Connection.Description"] = "iPhone Shortcuts sends SMS content here. Prefer a LAN address such as 10.x, 172.x, or 192.168.x. If the default port is occupied, the app automatically switches to an available port; use the address shown here.",
        ["Main.Address.Label"] = "Listening Address",
        ["Main.Token.Title"] = "API Token",
        ["Main.Token.Description"] = "This is the access token used by iPhone requests. Do not share it with untrusted people. After regenerating it, update the Shortcut accordingly.",
        ["Main.Button.CopyToken"] = "Copy Token",
        ["Main.Guide.Title"] = "iPhone Shortcuts Setup Guide",
        ["Main.Guide.Description"] = "The following steps are for first-time Shortcut users. Wording may vary slightly across iOS versions, but the required fields are the same.",
        ["Main.Guide.Step1.Title"] = "Create an SMS Automation",
        ["Main.Guide.Step1.Body"] = "Open the iPhone Shortcuts app, tap Automation at the bottom, create a new automation, and choose Message Received. A recommended condition is: when I receive a message containing \"verification code\"; then choose Run Immediately. After tapping Next, choose Create New Shortcut.",
        ["Main.Guide.Step2.Title"] = "Add \"Get Contents of URL\"",
        ["Main.Guide.Step2.Body"] = "Search for and add \"Get Contents of URL\". Set the URL to http://XXXXXXX:18080/api/sms, which should be copied directly from the listening address in the main window; set the method to POST.",
        ["Main.Guide.Step3.Title"] = "Fill in Headers",
        ["Main.Guide.Step3.Body"] = "Expand Headers and add two rows: key Authorization with text Bearer <API Token from the main window>; key Content-Type with text application/json. Note that Bearer must be followed by one space, for example: Bearer abcdefg123456.",
        ["Main.Guide.Step4.Title"] = "Fill in the JSON Request Body",
        ["Main.Guide.Step4.Body"] = "In Request Body, choose JSON. Add one required field: key message, and for the text value choose \"Shortcut Input\". After tapping the text field, swipe through the variable suggestions above the input box to find and select \"Shortcut Input\".",
        ["Main.Guide.Step5.Title"] = "Test with Fixed Text First",
        ["Main.Guide.Step5.Body"] = "If you are not sure whether the SMS variable is selected correctly, temporarily set message to fixed text: [Test] Your verification code is 123456, valid for 5 minutes. After running the Shortcut manually, Windows should show a notification, the clipboard should become 123456, and one row should be added to Recent Records. After the test succeeds, change message back to \"Shortcut Input\".",
        ["Main.Recent.Title"] = "Recent Records",
        ["Main.Button.ClearRecords"] = "Clear Records",
        ["Main.Grid.Time"] = "Time",
        ["Main.Grid.Sender"] = "Sender",
        ["Main.Grid.Code"] = "Code",
        ["Main.Grid.Copied"] = "Copy Status",
        ["Main.Grid.Toast"] = "Notification",
        ["Main.Record.Copied"] = "Copied",
        ["Main.Record.NotCopied"] = "Not copied",

        ["Settings.WindowTitle"] = "OtpBridge Settings",
        ["Settings.Title"] = "Settings",
        ["Settings.Subtitle"] = "Only configuration is saved; full SMS content is not stored. After changing the port or token, update the iPhone Shortcut accordingly.",
        ["Settings.Button.Save"] = "Save",
        ["Settings.Button.Cancel"] = "Cancel",
        ["Settings.ConfigPath"] = "Settings File",
        ["Settings.Language"] = "Interface Language",
        ["Settings.Language.Hint"] = "Chinese is the default. After saving, the main window, settings, tray menu, and prompts use the selected language.",
        ["Settings.Port"] = "Listening Port",
        ["Settings.Port.Hint"] = "Default: 18080. If the port is already used by another program, OtpBridge automatically switches to the next available port and shows the new address in the main window.",
        ["Settings.Token"] = "API Token",
        ["Settings.GenerateToken"] = "Generate",
        ["Settings.Token.Hint"] = "After regenerating the token, update the Authorization value in the iPhone Shortcut to the new Bearer Token.",
        ["Settings.AutoCopy"] = "Auto Copy",
        ["Settings.AutoCopy.Content"] = "Write the code to the clipboard after receiving it",
        ["Settings.ShowToast"] = "Bottom-right Notification",
        ["Settings.ShowToast.Content"] = "Show a Windows notification after receiving a code",
        ["Settings.StartWithWindows"] = "Launch at Startup",
        ["Settings.StartWithWindows.Content"] = "Start automatically after Windows sign-in and minimize to the tray",
        ["Settings.RecentCount"] = "Recent Record Count",
        ["Settings.RecentCount.Hint"] = "Only affects recent records kept in memory. Full SMS content is not written to disk.",
        ["Settings.CustomRegex"] = "Custom Regex",
        ["Settings.CustomRegex.Hint"] = "Optional. If capture groups are present, the last non-empty capture group is used as the verification code.",
        ["Settings.Error.Title"] = "OtpBridge Settings",
        ["Settings.Error.Port"] = "The listening port must be a number between 1 and 65535.",
        ["Settings.Error.Token"] = "API Token cannot be empty.",
        ["Settings.Error.RecentCount"] = "Recent record count must be a number between 1 and {0}.",
        ["Settings.Error.CustomRegex"] = "Invalid custom regex: {0}"
    };

    public static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        return Chinese;
    }

    public static string Text(string? language, string key)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        var texts = normalizedLanguage == English ? EnglishTexts : ChineseTexts;

        if (texts.TryGetValue(key, out var value))
        {
            return value;
        }

        return ChineseTexts.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public static IReadOnlyList<LanguageOption> GetLanguageOptions(string? language)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        return normalizedLanguage == English
            ? [new LanguageOption(Chinese, "Chinese"), new LanguageOption(English, "English")]
            : [new LanguageOption(Chinese, "中文"), new LanguageOption(English, "English")];
    }

    public static string Format(string? language, string key, params object?[] args)
    {
        return string.Format(Text(language, key), args);
    }

    public static string DefaultLanguage => Chinese;
}
