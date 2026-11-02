using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class InstallerProgram
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }
}

internal sealed class InstallerForm : Form
{
    private const long SupportedExeSize = 68005144L;

    private readonly ComboBox gameCombo = new ComboBox();
    private readonly TextBox passwordBox = new TextBox();
    private readonly CheckBox unlimitedCheck = new CheckBox();
    private readonly Button installButton = new Button();
    private readonly Button uninstallButton = new Button();
    private readonly Button chooseButton = new Button();
    private readonly Button refreshButton = new Button();
    private readonly Label statusLabel = new Label();
    private readonly TextBox logBox = new TextBox();
    private bool busy;
    private readonly string logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SekiroCoop-install.log");

    public InstallerForm()
    {
        Text = "安装 Sekiro 协作联机模组";
        ClientSize = new Size(920, 640);
        MinimumSize = new Size(780, 540);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(246, 247, 249);
        AutoScaleMode = AutoScaleMode.Dpi;

        var title = new Label
        {
            Text = "安装 Sekiro 协作联机模组",
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 28, 30),
            Location = new Point(18, 14),
            AutoSize = true
        };
        Controls.Add(title);

        var description = new Label
        {
            Text = "自动识别各盘的 Steam 游戏库。也可选择 sekiro.exe，或粘贴游戏目录。",
            ForeColor = Color.FromArgb(80, 84, 90),
            Location = new Point(20, 54),
            Size = new Size(860, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(description);

        gameCombo.DropDownStyle = ComboBoxStyle.DropDown;
        gameCombo.Location = new Point(20, 82);
        gameCombo.Size = new Size(680, 28);
        gameCombo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        gameCombo.Font = new Font("Microsoft YaHei UI", 9.5F);
        Controls.Add(gameCombo);

        chooseButton.Text = "选择游戏...";
        chooseButton.Location = new Point(710, 80);
        chooseButton.Size = new Size(96, 30);
        chooseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        chooseButton.Click += ChooseGame_Click;
        Controls.Add(chooseButton);

        refreshButton.Text = "重新查找";
        refreshButton.Location = new Point(814, 80);
        refreshButton.Size = new Size(86, 30);
        refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        refreshButton.Click += (s, e) => RefreshGames();
        Controls.Add(refreshButton);

        var passwordLabel = new Label
        {
            Text = "联机密码",
            Location = new Point(20, 124),
            AutoSize = true
        };
        Controls.Add(passwordLabel);

        passwordBox.Location = new Point(92, 120);
        passwordBox.Size = new Size(220, 28);
        passwordBox.MaxLength = 64;
        Controls.Add(passwordBox);

        unlimitedCheck.Text = "纸人不消耗";
        unlimitedCheck.Checked = true;
        unlimitedCheck.Location = new Point(332, 121);
        unlimitedCheck.AutoSize = true;
        Controls.Add(unlimitedCheck);

        var passwordHint = new Label
        {
            Text = "（所有玩家必须使用相同密码；留空则联机保持关闭）",
            ForeColor = Color.FromArgb(120, 124, 130),
            Location = new Point(446, 125),
            AutoSize = true
        };
        Controls.Add(passwordHint);

        installButton.Text = "安装 / 更新";
        installButton.Location = new Point(20, 158);
        installButton.Size = new Size(110, 32);
        installButton.Click += Install_Click;
        Controls.Add(installButton);

        uninstallButton.Text = "卸载并恢复";
        uninstallButton.Location = new Point(138, 158);
        uninstallButton.Size = new Size(110, 32);
        uninstallButton.Click += Uninstall_Click;
        Controls.Add(uninstallButton);

        statusLabel.Text = "正在查找游戏...";
        statusLabel.ForeColor = Color.FromArgb(160, 110, 0);
        statusLabel.Location = new Point(264, 164);
        statusLabel.Size = new Size(620, 22);
        statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        statusLabel.AutoEllipsis = true;
        Controls.Add(statusLabel);

        var hint = new Label
        {
            Text = "安装前请关闭游戏。自动备份；卸载保留当前存档。",
            ForeColor = Color.FromArgb(90, 94, 100),
            Location = new Point(20, 200),
            AutoSize = true
        };
        Controls.Add(hint);

        logBox.Location = new Point(20, 226);
        logBox.Size = new Size(880, 390);
        logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        logBox.Multiline = true;
        logBox.ReadOnly = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.WordWrap = false;
        logBox.BackColor = Color.White;
        logBox.BorderStyle = BorderStyle.FixedSingle;
        logBox.Font = new Font("Consolas", 9F);
        Controls.Add(logBox);

        Shown += (s, e) =>
        {
            Log("Sekiro 协作联机模组安装器");
            Log("内置联机 DLL、me3 和纸人补丁启动器，无需另装编译工具。");
            RefreshGames();
        };
        FormClosing += (s, e) =>
        {
            if (busy)
            {
                e.Cancel = true;
                MessageBox.Show(this, "正在安装，请稍候...", "请稍候", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
    }

    private void ChooseGame_Click(object sender, EventArgs e)
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Title = "选择 sekiro.exe";
            dialog.Filter = "sekiro.exe|sekiro.exe|可执行文件 (*.exe)|*.exe";
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                gameCombo.Text = Path.GetDirectoryName(dialog.FileName);
                SetStatus("已找到游戏，点击“安装 / 更新”即可。", Color.SeaGreen);
                Log("手动选择游戏目录: " + gameCombo.Text);
            }
        }
    }

    private void RefreshGames()
    {
        if (busy)
        {
            return;
        }
        string previous = gameCombo.Text;
        List<string> games = SteamGames.Find();
        gameCombo.Items.Clear();
        foreach (string game in games)
        {
            gameCombo.Items.Add(game);
        }
        if (previous.Length > 0 && Directory.Exists(previous))
        {
            gameCombo.Text = previous;
        }
        else if (games.Count > 0)
        {
            gameCombo.SelectedIndex = 0;
        }

        if (games.Count > 0)
        {
            SetStatus("已找到游戏，点击“安装 / 更新”即可。", Color.SeaGreen);
            Log("找到 " + games.Count + " 个《只狼》安装。");
        }
        else
        {
            SetStatus("未找到游戏，请点击“选择游戏...”手动选择 sekiro.exe。", Color.FromArgb(180, 100, 0));
            Log("未找到 Steam 库中的《只狼》，可以手动选择游戏目录。");
        }
    }

    private async void Install_Click(object sender, EventArgs e)
    {
        if (busy)
        {
            return;
        }

        string gameDir;
        try
        {
            gameDir = ValidateGame();
        }
        catch (Exception error)
        {
            SetStatus(error.Message, Color.Firebrick);
            Log(error.Message);
            return;
        }

        if (Process.GetProcessesByName("sekiro").Length > 0)
        {
            SetStatus("请先关闭游戏再安装。", Color.Firebrick);
            Log("安装已取消：sekiro.exe 正在运行。");
            return;
        }
        if (Process.GetProcessesByName("SekiroCoopLauncher").Length > 0)
        {
            SetStatus("请先退出 SekiroCoopLauncher 再安装。", Color.Firebrick);
            Log("安装已取消：启动器正在运行。");
            return;
        }

        if (passwordBox.Text.Length == 0 &&
            MessageBox.Show(this,
                "联机密码为空。安装后联机模式会保持关闭，直到你填写密码并再次点击“安装 / 更新”。\n\n仍要继续吗？",
                "密码为空",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true, "正在安装...");
        string password = passwordBox.Text;
        bool unlimited = unlimitedCheck.Checked;
        try
        {
            await Task.Run(() => Install(gameDir, password, unlimited));
            if (password.Length == 0)
            {
                SetStatus("安装完成，但联机密码为空；填写密码后再次点击“安装 / 更新”。", Color.FromArgb(180, 100, 0));
            }
            else
            {
                SetStatus("安装完成，双击桌面“Sekiro Co-op”开始游戏。", Color.SeaGreen);
            }
        }
        catch (Exception error)
        {
            Log("安装失败: " + error.Message);
            SetStatus("安装失败，请看下方日志。", Color.Firebrick);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private async void Uninstall_Click(object sender, EventArgs e)
    {
        if (busy)
        {
            return;
        }
        if (MessageBox.Show(this,
                "卸载会删除联机模组和桌面快捷方式，但保留联机存档和备份。继续？",
                "卸载并恢复",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true, "正在卸载...");
        try
        {
            await Task.Run(() => Uninstall());
            SetStatus("已卸载，联机存档和备份仍然保留。", Color.SeaGreen);
        }
        catch (Exception error)
        {
            Log("卸载失败: " + error.Message);
            SetStatus("卸载失败，请看下方日志。", Color.Firebrick);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void Install(string gameDir, string password, bool unlimited)
    {
        Log("开始安装 / 更新。");
        Log("游戏目录: " + gameDir);

        string exePath = Path.Combine(gameDir, "sekiro.exe");
        long exeSize = new FileInfo(exePath).Length;
        if (exeSize != SupportedExeSize)
        {
            Log("警告: sekiro.exe 大小是 " + exeSize + " 字节，支持的是 Steam 1.06（" + SupportedExeSize + " 字节）。");
            Log("如果游戏不是 1.06，请先在 Steam 里验证游戏文件。");
        }

        string installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SekiroCoop");
        if (installDir.TrimEnd('\\').StartsWith(gameDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("安装目录不能在游戏目录里面。");
        }
        Directory.CreateDirectory(installDir);
        Log("安装目录: " + installDir);

        string iniPath = Path.Combine(installDir, "sekiro_coop_settings.ini");
        if (File.Exists(iniPath))
        {
            string backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SekiroCoop-backups");
            Directory.CreateDirectory(backupDir);
            string backupPath = Path.Combine(backupDir,
                "sekiro_coop_settings-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".ini");
            File.Copy(iniPath, backupPath, true);
            Log("已备份旧设置: " + backupPath);
        }

        Log("配置联机 DLL...");
        byte[] dll = ReadLocalDllOrResource();
        bool dllHasFeature = ContainsAscii(dll, "unlimited_spirit_emblems");
        File.WriteAllBytes(Path.Combine(installDir, "sekiro_coop.dll"), dll);
        File.WriteAllBytes(Path.Combine(installDir, "libstdc++-6.dll"), ReadResource("libstdc++-6.dll"));
        File.WriteAllBytes(Path.Combine(installDir, "libgcc_s_seh-1.dll"), ReadResource("libgcc_s_seh-1.dll"));
        File.WriteAllBytes(Path.Combine(installDir, "libwinpthread-1.dll"), ReadResource("libwinpthread-1.dll"));
        if (dllHasFeature)
        {
            Log("联机 DLL 已内置纸人不消耗。");
        }
        else
        {
            Log("使用预编译联机 DLL；纸人不消耗由启动器补丁实现。");
        }

        Log("写入 me3 profile...");
        File.WriteAllText(Path.Combine(installDir, "sekiro-coop.me3"), ProfileText(), new UTF8Encoding(true));

        Log("写入设置文件...");
        WriteSettings(iniPath, password, unlimited);

        Log("检查 me3...");
        string me3 = FindMe3();
        if (me3 == null)
        {
            Log("未安装 me3，正在释放内置的 me3...");
            ExtractEmbeddedMe3();
            me3 = FindMe3();
        }
        if (me3 == null)
        {
            throw new InvalidOperationException("me3 安装失败。请手动安装: https://github.com/garyttierney/me3/releases");
        }
        File.WriteAllText(Path.Combine(installDir, "me3_path.txt"), me3, new UTF8Encoding(false));
        Log("me3: " + me3);

        Log("释放启动器...");
        File.WriteAllBytes(Path.Combine(installDir, "SekiroCoopLauncher.exe"), ReadResource("SekiroCoopLauncher.exe"));

        Log("创建桌面快捷方式...");
        try
        {
            string shortcut = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Sekiro Co-op.lnk");
            CreateShortcut(shortcut,
                Path.Combine(installDir, "SekiroCoopLauncher.exe"),
                "",
                installDir,
                exePath);
            Log("快捷方式: " + shortcut);
        }
        catch (Exception error)
        {
            Log("警告: 创建快捷方式失败: " + error.Message);
            Log("可以改用 " + Path.Combine(installDir, "启动游戏.cmd"));
        }

        string launcherCmd = Path.Combine(installDir, "启动游戏.cmd");
        File.WriteAllText(launcherCmd,
            "@echo off\r\nstart \"\" \"%~dp0SekiroCoopLauncher.exe\"\r\n",
            Encoding.Default);

        string conflicts = FindConflicts(gameDir);
        if (conflicts.Length > 0)
        {
            Log("警告: 游戏目录里有其它联机模组文件，请先删除并在 Steam 里验证文件完整性:");
            Log(conflicts);
        }
        Log("安装完成。联机密码" + (password.Length > 0 ? "已设置。" : "为空，联机保持关闭。"));
        Log("纸人不消耗: " + (unlimited ? "开启" : "关闭"));
    }

    private void Uninstall()
    {
        if (Process.GetProcessesByName("sekiro").Length > 0)
        {
            throw new InvalidOperationException("请先关闭游戏。");
        }
        if (Process.GetProcessesByName("SekiroCoopLauncher").Length > 0)
        {
            throw new InvalidOperationException("请先退出 SekiroCoopLauncher。");
        }

        string shortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "Sekiro Co-op.lnk");
        if (File.Exists(shortcut))
        {
            File.Delete(shortcut);
            Log("已删除桌面快捷方式。");
        }

        string installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SekiroCoop");
        if (Directory.Exists(installDir))
        {
            Directory.Delete(installDir, true);
            Log("已删除: " + installDir);
        }
        Log("游戏本体没有改动，联机存档和备份保留。");
    }

    private string ValidateGame()
    {
        string input = (gameCombo.Text ?? string.Empty).Trim().Trim('"');
        if (input.Length == 0)
        {
            throw new InvalidOperationException("请先选择《只狼》游戏目录。");
        }
        if (File.Exists(input) && string.Equals(Path.GetFileName(input), "sekiro.exe", StringComparison.OrdinalIgnoreCase))
        {
            input = Path.GetDirectoryName(input);
        }
        if (!Directory.Exists(input))
        {
            throw new InvalidOperationException("游戏目录不存在: " + input);
        }
        if (!File.Exists(Path.Combine(input, "sekiro.exe")))
        {
            throw new InvalidOperationException("这个目录下没有 sekiro.exe。");
        }
        return Path.GetFullPath(input);
    }

    private void SetBusy(bool value, string installText)
    {
        busy = value;
        gameCombo.Enabled = !value;
        passwordBox.Enabled = !value;
        unlimitedCheck.Enabled = !value;
        chooseButton.Enabled = !value;
        refreshButton.Enabled = !value;
        installButton.Enabled = !value;
        uninstallButton.Enabled = !value;
        installButton.Text = value ? (installText ?? "处理中...") : "安装 / 更新";
        UseWaitCursor = value;
    }

    private void SetStatus(string text, Color color)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string, Color>(SetStatus), text, color);
            return;
        }
        statusLabel.Text = text;
        statusLabel.ForeColor = color;
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(Log), message);
            return;
        }
        string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
        logBox.AppendText(line + Environment.NewLine);
        logBox.SelectionStart = logBox.TextLength;
        logBox.ScrollToCaret();
        try
        {
            File.AppendAllText(logPath,
                DateTime.Now.ToString("yyyy-MM-dd ") + line + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch
        {
        }
    }

    private static byte[] ReadLocalDllOrResource()
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates = new string[]
        {
            Path.Combine(exeDir, "sekiro_coop.dll"),
            Path.Combine(exeDir, @"prebuilt\sekiro_coop.dll")
        };
        foreach (string candidate in candidates)
        {
            try
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }
                byte[] data = File.ReadAllBytes(candidate);
                if (data.Length > 1024 && data[0] == (byte)'M' && data[1] == (byte)'Z')
                {
                    return data;
                }
            }
            catch
            {
            }
        }
        return ReadResource("sekiro_coop.dll");
    }

    private static bool ContainsAscii(byte[] data, string needle)
    {
        return Encoding.ASCII.GetString(data).IndexOf(needle, StringComparison.Ordinal) >= 0;
    }

    private static byte[] ReadResource(string name)
    {
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (stream == null)
            {
                throw new InvalidOperationException("安装包资源缺失: " + name);
            }
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }
    }

    private static string ReadResourceText(string name)
    {
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (stream == null)
            {
                return null;
            }
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                return reader.ReadToEnd();
            }
        }
    }

    private static void WriteSettings(string iniPath, string password, bool unlimited)
    {
        string text;
        if (File.Exists(iniPath))
        {
            text = File.ReadAllText(iniPath);
            if (password.Length > 0)
            {
                text = SetIniValue(text, "password", password);
            }
        }
        else
        {
            text = ReadResourceText("default-settings.ini");
            if (text == null)
            {
                text = "; Sekiro seamless co-op settings\r\npassword =\r\n\r\n[GAMEPLAY]\r\nunlimited_spirit_emblems = 1\r\n";
            }
            text = text
                .Replace("@PASSWORD@", password)
                .Replace("@NAME@", "")
                .Replace("@TRANSPORT@", "auto")
                .Replace("@HOST@", "");
        }
        text = SetIniValue(text, "unlimited_spirit_emblems", unlimited ? "1" : "0");
        File.WriteAllText(iniPath, text, new UTF8Encoding(true));
    }

    private static string SetIniValue(string text, string key, string value)
    {
        var regex = new Regex(@"(?im)^[ \t]*" + Regex.Escape(key) + @"[ \t]*=[ \t]*.*$");
        if (regex.IsMatch(text))
        {
            return regex.Replace(text, match => key + " = " + value);
        }
        string eol = text.Contains("\r\n") ? "\r\n" : "\n";
        if (text.Length > 0 && !text.EndsWith("\n", StringComparison.Ordinal))
        {
            text += eol;
        }
        return text + key + " = " + value + eol;
    }

    private static string ProfileText()
    {
        return
            "profileVersion = \"v1\"\r\n" +
            "# Co-op uses its own save (copied from the normal one on first launch).\r\n" +
            "savefile = \"S0000.coop.sl2\"\r\n" +
            "\r\n" +
            "[[supports]]\r\n" +
            "game = \"sekiro\"\r\n" +
            "\r\n" +
            "[[natives]]\r\n" +
            "path = \"sekiro_coop.dll\"\r\n" +
            "optional = false\r\n" +
            "enabled = true\r\n";
    }

    private static string FindMe3()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates = new string[]
        {
            Path.Combine(local, @"Programs\garyttierney\me3\bin\me3.exe"),
            Path.Combine(local, @"Programs\me3\bin\me3.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"me3\bin\me3.exe")
        };
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        try
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string part in path.Split(';'))
            {
                if (part.Trim().Length == 0)
                {
                    continue;
                }
                string candidate = Path.Combine(part.Trim(), "me3.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static void ExtractEmbeddedMe3()
    {
        string target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Programs\garyttierney\me3");
        Directory.CreateDirectory(target);
        string targetFull = Path.GetFullPath(target).TrimEnd('\\') + Path.DirectorySeparatorChar;

        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("me3-windows-amd64.zip"))
        {
            if (stream == null)
            {
                throw new InvalidOperationException("安装包资源缺失: me3-windows-amd64.zip");
            }
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = Path.GetFullPath(
                        Path.Combine(target, entry.FullName.Replace('/', '\\')));
                    if (!destination.StartsWith(targetFull, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (entry.Name.Length == 0)
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    entry.ExtractToFile(destination, true);
                }
            }
        }
    }

    private static string FindConflicts(string gameDir)
    {
        string[] names = new string[]
        {
            "dinput8.dll", "steam_api64.org", "cream_api.ini", "SekiroOnlineFont.ttf"
        };
        var found = new List<string>();
        foreach (string name in names)
        {
            string path = Path.Combine(gameDir, name);
            if (File.Exists(path))
            {
                found.Add(path);
            }
        }
        return string.Join(Environment.NewLine, found.ToArray());
    }

    private static void CreateShortcut(string shortcutPath, string target, string arguments, string workDir, string iconPath)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            throw new InvalidOperationException("WScript.Shell 不可用");
        }
        object shell = Activator.CreateInstance(shellType);
        try
        {
            object shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });
            if (shortcut == null)
            {
                throw new InvalidOperationException("CreateShortcut 返回空");
            }
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workDir });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Sekiro Seamless Co-op" });
            if (File.Exists(iconPath))
            {
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
            }
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
        }
    }
}

internal static class SteamGames
{
    public static List<string> Find()
    {
        var games = new List<string>();
        foreach (string root in FindSteamRoots())
        {
            AddCandidate(games, Path.Combine(root, @"steamapps\common\Sekiro"));
            try
            {
                string vdf = Path.Combine(root, @"steamapps\libraryfolders.vdf");
                if (!File.Exists(vdf))
                {
                    continue;
                }
                string text = File.ReadAllText(vdf);
                foreach (Match match in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
                {
                    string library = match.Groups[1].Value.Replace("\\\\", "\\");
                    AddCandidate(games, Path.Combine(library, @"steamapps\common\Sekiro"));
                }
            }
            catch
            {
            }
        }
        return games;
    }

    private static void AddCandidate(List<string> games, string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return;
            }
            if (!File.Exists(Path.Combine(path, "sekiro.exe")))
            {
                return;
            }
            string full = Path.GetFullPath(path);
            foreach (string existing in games)
            {
                if (string.Equals(existing, full, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            games.Add(full);
        }
        catch
        {
        }
    }

    private static List<string> FindSteamRoots()
    {
        var roots = new List<string>();
        AddRegistry(roots, RegistryHive.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        AddRegistry(roots, RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        AddRegistry(roots, RegistryHive.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        return roots;
    }

    private static void AddRegistry(List<string> roots, RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
            using (RegistryKey key = baseKey.OpenSubKey(subKey))
            {
                if (key == null)
                {
                    return;
                }
                string value = key.GetValue(valueName) as string;
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }
                value = value.Replace('/', '\\').TrimEnd('\\');
                if (!Directory.Exists(value))
                {
                    return;
                }
                foreach (string existing in roots)
                {
                    if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                roots.Add(value);
            }
        }
        catch
        {
        }
    }
}
