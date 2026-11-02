Sekiro Co-op CN Mod（1.0.8 beta 定制整合）+ 纸人不消耗
======================================================

先解压整个压缩包，再双击“一键安装.exe”。

界面说明
--------
1. 自动识别各盘 Steam 库中的《只狼》；也可以点“选择游戏...”手动选择
   sekiro.exe，或直接把游戏目录粘贴到下拉框。
2. “联机密码”可以留空；游戏内的 Insert 快捷面板不使用密码。
3. “纸人不消耗”默认勾选。
4. 点“安装 / 更新”。安装器会自动处理：
   - 复制联机 DLL 到 %LOCALAPPDATA%\SekiroCoop（不会写进游戏目录）
   - 释放内置的 me3 和纸人补丁启动器
   - 写入 sekiro_coop_settings.ini
   - 在桌面创建 “Sekiro Co-op CN Mod” 快捷方式
5. 点“卸载并恢复”可以删除模组和快捷方式；游戏本体、联机存档和备份都会保留。

安装器已经内置 1.0.8 beta 定制 DLL、me3 和默认配置，不需要 Rust 或
Visual Studio。定制 DLL 保留：
  - 游戏内“联机设置”页面和“模组设置”面板中文界面
  - Insert 快捷联机面板（UDP / SakuraFrp）
  - 纸人不消耗（unlimited_spirit_emblems）
  - 原有的技能栏、门和傀儡相关修复
如果“一键安装.exe”旁边放了自己的 sekiro_coop.dll，会优先使用它
（例如本仓库新增了内置纸人功能后编译出来的 DLL）。

纸人不消耗
----------
默认开启，设置文件里是：
  unlimited_spirit_emblems = 1

内置的是以 1.0.8 beta 源码为基线、重新合入定制功能后编译的联机 DLL。
启动器 SekiroCoopLauncher.exe 仍会在游戏运行时保底写入游戏自带的
PlayerNoEmblemsConsume 调试开关，所以即使换成不含该功能的上游 DLL，
纸人不消耗也能生效。两种方式都不会修改存档。

想恢复原版消耗：取消勾选“纸人不消耗”后点“安装 / 更新”，
或把 sekiro_coop_settings.ini 里的这一行改成 0。

前置条件
--------
- Steam 正版《只狼》1.06，未安装其他联机模组。
- 如果游戏目录里有 dinput8.dll、steam_api64.org、cream_api.ini、
  SekiroOnlineFont.ttf，请先删除，然后在 Steam 里“验证游戏文件完整性”。
- 安装前请关闭游戏。
- 最终 Boss（剑圣苇名一心）死亡后门打不开：默认已改为原版行为
  `boss_persist_on_death = 0`，死亡会重置 Boss 并重新开门。若你在旧 ini 里
  还是 1，改成 0 后休息一次即可。

游戏内快捷联机面板（Insert）
----------------------------
游戏里按 Insert 会打开一个小面板，和 HKMP 的联机界面类似：
  - 用户名
  - 服务器 IP（朋友端填）
  - 端口（主机端口 / 远程端口）
  - 连接 / 开房 / 断开

没有内置密码，也不需要填密码；面板会把联机密码清空并保存。
它会自动把 transport 设为 udp，并保存到 sekiro_coop_settings.ini。
樱花联机用法：主机在 SakuraFrp 建 UDP 隧道指向本机 28100；朋友填 SakuraFrp 面板上的
服务器 IP 和远程端口（支持域名，例如 frp-egg.com）。面板打开时会接管键盘，
关闭后游戏恢复正常输入。

高级用法
--------
命令行安装仍保留 PowerShell 版本：
  powershell -ExecutionPolicy Bypass -File install.ps1 -Password "同一个密码"
  powershell -ExecutionPolicy Bypass -File install.ps1 -CheckOnly
  powershell -ExecutionPolicy Bypass -File install.ps1 -GameDir "D:\...\Sekiro"

日志
----
安装日志：%LOCALAPPDATA%\SekiroCoop-install.log
启动器日志：%LOCALAPPDATA%\SekiroCoop\launcher.log
游戏日志：%LOCALAPPDATA%\sekiro-coop\log.txt
