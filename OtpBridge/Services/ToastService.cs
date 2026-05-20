using System.Diagnostics;
using System.Security;
using System.Text;

namespace OtpBridge.Services;

public sealed class ToastService
{
    public bool ShowToast(string title, string message)
    {
        try
        {
            var xml = BuildToastXml(title, message);
            var encodedXml = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
            var script = """
                [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
                $xmlText = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__XML__'))
                $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
                $xml.LoadXml($xmlText)
                $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
                [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('__APP_NAME__').Show($toast)
                """
                .Replace("__XML__", encodedXml, StringComparison.Ordinal)
                .Replace("__APP_NAME__", AppInfo.Name, StringComparison.Ordinal);

            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(script);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch (Exception exception)
        {
            StartupLog.Error("Toast notification failed.", exception);
            return false;
        }
    }

    private static string BuildToastXml(string title, string message)
    {
        return $"""
                <toast>
                  <visual>
                    <binding template="ToastGeneric">
                      <text>{SecurityElement.Escape(title)}</text>
                      <text>{SecurityElement.Escape(message)}</text>
                    </binding>
                  </visual>
                </toast>
                """;
    }
}
