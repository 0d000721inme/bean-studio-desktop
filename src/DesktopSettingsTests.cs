using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MuMuBeans
{
    static class DesktopSettingsTests
    {
        sealed class FakeRegistration : IHotkeyRegistration
        {
            internal readonly Dictionary<int,uint> Registered=new Dictionary<int,uint>();
            internal uint RejectKey,LastModifiers;
            internal int RegisterCalls,UnregisterCalls;
            public bool Register(IntPtr handle,int id,uint modifiers,uint key,out int error)
            {
                RegisterCalls++;LastModifiers=modifiers;error=0;
                if(key==RejectKey){error=1409;return false;}Registered[id]=key;return true;
            }
            public void Unregister(IntPtr handle,int id){UnregisterCalls++;Registered.Remove(id);}
        }
        sealed class TestWindow : Form
        {
            internal void RecreateForTest(){RecreateHandle();}
        }
        static readonly List<string> rows=new List<string>();
        static int failures;
        static void Check(bool ok,string title){rows.Add((ok?"PASS ":"FAIL ")+title);if(!ok)failures++;}
        static bool Invalid(DesktopSettings s){try{DesktopSettingsStore.Validate(s);return false;}catch(ArgumentException){return true;}}
        internal static void Run(string[] args)
        {
            string folder=args[1];Directory.CreateDirectory(folder);rows.Clear();failures=0;
            try
            {
                string file=Path.Combine(folder,"desktop-settings-test.xml"),error;
                DesktopSettings settings=DesktopSettingsStore.Load(file,out error);
                Check(error==""&&settings.HotkeysEnabled&&settings.ToggleOverlayHotkey.Key==Keys.F8&&settings.RescanHotkey.Key==Keys.F9,"无配置使用 Ctrl+Alt+F8 / F9 默认快捷键");
                Check(settings.OverlayStyle==OverlayStyle.Focus&&settings.OverlayScale==100&&settings.OverlayBackground==18&&!settings.WholeSeconds&&settings.ShowRecovery&&settings.PerformanceVisible&&settings.PreviewVisible,"默认小数与主要恢复点、性能及预览设置明确");
                DesktopSettings copy=settings.Copy();copy.ToggleOverlayHotkey.Key=Keys.F10;copy.OverlayStyle=OverlayStyle.Ribbon;
                Check(settings.ToggleOverlayHotkey.Key==Keys.F8&&settings.OverlayStyle==OverlayStyle.Focus,"草稿复制不改变原设置或快捷键");
                copy.RescanHotkey=new HotkeyChord(HotkeyModifiers.Control|HotkeyModifiers.Shift,Keys.R);copy.HotkeysEnabled=false;copy.OverlayScale=140;copy.OverlayBackground=0;copy.WholeSeconds=false;copy.ShowRecovery=false;copy.PerformanceVisible=false;copy.PreviewVisible=false;
                DesktopSettingsStore.Save(file,copy);DesktopSettings loaded=DesktopSettingsStore.Load(file,out error);
                Check(error==""&&!loaded.HotkeysEnabled&&loaded.ToggleOverlayHotkey.Equals(copy.ToggleOverlayHotkey)&&loaded.RescanHotkey.Equals(copy.RescanHotkey)&&loaded.OverlayStyle==OverlayStyle.Ribbon&&loaded.OverlayScale==140&&loaded.OverlayBackground==0&&!loaded.WholeSeconds&&!loaded.ShowRecovery&&!loaded.PerformanceVisible&&!loaded.PreviewVisible,"全部设置与自定义组合键 XML 往返一致");
                copy.OverlayScale=80;copy.OverlayBackground=40;copy.ToggleOverlayHotkey=new HotkeyChord();DesktopSettingsStore.Save(file,copy);
                loaded=DesktopSettingsStore.Load(file,out error);Check(error==""&&loaded.ToggleOverlayHotkey.IsEmpty&&loaded.OverlayScale==80&&loaded.OverlayBackground==40,"支持大小与背景边界、单项快捷键清空及原子替换");
                Check(Directory.GetFiles(folder,"*.tmp.*").Length==0,"保存后不遗留临时配置文件");
                DesktopSettings invalid=copy.Copy();invalid.OverlayScale=79;Check(Invalid(invalid),"拒绝越界大小");invalid=copy.Copy();invalid.OverlayBackground=41;Check(Invalid(invalid),"拒绝越界背景强度");
                invalid=new DesktopSettings{OverlayStyle=(OverlayStyle)50};Check(Invalid(invalid),"拒绝未知悬浮窗风格");invalid=new DesktopSettings{ToggleOverlayHotkey=new HotkeyChord(HotkeyModifiers.Shift,Keys.F8)};Check(Invalid(invalid),"拒绝普通或仅 Shift 组合，避免侵占游戏键");
                invalid=new DesktopSettings{ToggleOverlayHotkey=new HotkeyChord(HotkeyModifiers.Control,Keys.F12)};Check(Invalid(invalid),"拒绝 Windows 保留的 F12");invalid=new DesktopSettings();invalid.RescanHotkey=invalid.ToggleOverlayHotkey;Check(Invalid(invalid),"拒绝两种动作绑定同一组合键");
                bool threw=false;try{DesktopSettingsStore.Save(file,invalid);}catch(ArgumentException){threw=true;}Check(threw&&DesktopSettingsStore.Load(file,out error).OverlayScale==80,"非法草稿不会覆盖已保存设置");
                File.WriteAllText(file,"<BeanStudioDesktop version='1' overlayScale='oops' />");loaded=DesktopSettingsStore.Load(file,out error);Check(error.Length>0&&loaded.OverlayScale==100,"配置损坏非致命，报告原因并回退默认");
                File.WriteAllText(file,"<!DOCTYPE x [<!ENTITY secret SYSTEM 'file:///C:/Windows/win.ini'>]><BeanStudioDesktop version='1'>&secret;</BeanStudioDesktop>");loaded=DesktopSettingsStore.Load(file,out error);Check(error.Length>0,"配置禁止 DTD 和外部实体读取");
                File.WriteAllText(file,new string('a',17000));loaded=DesktopSettingsStore.Load(file,out error);Check(error.Length>0,"拒绝过大的配置文件");
                File.WriteAllText(file,"<BeanStudioDesktop version='1'/>");loaded=DesktopSettingsStore.Load(file,out error);Check(error==""&&loaded.HotkeysEnabled&&loaded.OverlayScale==100,"缺失可选字段兼容默认值");
                File.WriteAllText(file,"<BeanStudioDesktop version='1' hotkeysEnabled='false' overlayStyle='Ribbon' overlayScale='120' overlayBackground='10' wholeSeconds='true' showRecovery='false' performanceVisible='false' previewVisible='false' />");
                loaded=DesktopSettingsStore.Load(file,out error);
                Check(error==""&&!loaded.WholeSeconds&&loaded.ShowRecovery,"旧V1配置恢复小数并启用主要恢复点，不沿用旧整秒默认");
                Check(!loaded.HotkeysEnabled&&loaded.OverlayStyle==OverlayStyle.Ribbon&&loaded.OverlayScale==120&&loaded.OverlayBackground==10&&!loaded.PerformanceVisible&&!loaded.PreviewVisible,"显示迁移保留既有快捷键开关、风格、大小、透明度和性能偏好");
                loaded.WholeSeconds=true;loaded.ShowRecovery=false;DesktopSettingsStore.Save(file,loaded);loaded=DesktopSettingsStore.Load(file,out error);
                Check(error==""&&loaded.WholeSeconds&&!loaded.ShowRecovery&&File.ReadAllText(file).Contains("version=\"2\""),"V2保存后尊重用户重新选择整秒或隐藏恢复点，不反复强制覆盖");
                File.WriteAllText(file,"<BeanStudioDesktop version='999'/>");loaded=DesktopSettingsStore.Load(file,out error);Check(error.Length>0&&!loaded.WholeSeconds&&loaded.ShowRecovery,"未知配置版本明确回退新默认");

                FakeRegistration native=new FakeRegistration();int toggles=0,scans=0;
                using(TestWindow owner=new TestWindow())
                using(HotkeyManager manager=new HotkeyManager(owner,delegate(DesktopHotkeyAction action){if(action==DesktopHotkeyAction.ToggleOverlay)toggles++;else scans++;},native))
                {
                    Check(native.RegisterCalls==0,"窗口句柄创建前不尝试注册");IntPtr handle=owner.Handle;
                    Check(native.Registered.Count==2&&(native.LastModifiers&HotkeyManager.NoRepeat)!=0,"句柄创建后注册两组，含 MOD_NOREPEAT");
                    Message toggle=Message.Create(handle,HotkeyManager.HotkeyMessage,(IntPtr)HotkeyManager.ToggleId,(IntPtr)(((int)Keys.F8<<16)|3));manager.ProcessMessage(ref toggle);
                    Message scan=Message.Create(handle,HotkeyManager.HotkeyMessage,(IntPtr)HotkeyManager.RescanId,(IntPtr)(((int)Keys.F9<<16)|3));manager.ProcessMessage(ref scan);
                    Check(toggles==1&&scans==1,"WM_HOTKEY 分发悬浮窗与扫描两个独立动作");
                    DesktopSettings changed=new DesktopSettings();changed.ToggleOverlayHotkey=new HotkeyChord(HotkeyModifiers.Control|HotkeyModifiers.Alt,Keys.F10);native.RejectKey=(uint)Keys.F10;
                    Check(!manager.TryApply(changed,out error)&&error.Contains("占用")&&native.Registered.Count==2&&native.Registered[HotkeyManager.ToggleId]==(uint)Keys.F8&&manager.Settings.ToggleOverlayHotkey.Key==Keys.F8,"注册冲突明确显示，旧绑定回滚继续可用");
                    native.RejectKey=0;Check(manager.TryApply(changed,out error)&&error==""&&native.Registered[HotkeyManager.ToggleId]==(uint)Keys.F10,"有效草稿替换绑定");
                    manager.ProcessMessage(ref toggle);Check(toggles==1,"更换组合后忽略排队的旧快捷键消息");
                    manager.Suspend();manager.Suspend();Check(native.Registered.Count==0,"设置录入时暂停注册，重复暂停幂等");
                    Message current=Message.Create(handle,HotkeyManager.HotkeyMessage,(IntPtr)HotkeyManager.ToggleId,(IntPtr)(((int)Keys.F10<<16)|3));manager.ProcessMessage(ref current);Check(toggles==1,"暂停录入时忽略已排队快捷键，不触发游戏操作");
                    changed.RescanHotkey=new HotkeyChord(HotkeyModifiers.Control,Keys.R);Check(manager.TryApply(changed,out error)&&native.Registered.Count==0&&manager.Settings.RescanHotkey.Key==Keys.R,"录入期间验证新绑定后立即释放，保存无需先恢复注册");
                    Check(manager.Resume(out error)&&native.Registered.Count==2&&native.Registered[HotkeyManager.RescanId]==(uint)Keys.R,"关闭设置后恢复新的组合");
                    int registrations=native.RegisterCalls;owner.RecreateForTest();Check(native.RegisterCalls==registrations+2&&native.Registered.Count==2,"窗口句柄重建自动注销并重新注册");
                    changed.HotkeysEnabled=false;Check(manager.TryApply(changed,out error)&&native.Registered.Count==0,"关闭全局快捷键完全注销，保留自定义组合");changed.HotkeysEnabled=true;manager.TryApply(changed,out error);manager.Dispose();manager.Dispose();Check(native.Registered.Count==0&&!manager.TryApply(changed,out error),"关闭服务注销所有组合，重复释放安全且拒绝后续修改");
                }
            }
            catch(Exception ex){Check(false,ex.ToString());}
            rows.Add("PASS_COUNT="+rows.FindAll(delegate(string row){return row.StartsWith("PASS ");}).Count);rows.Add("FAILURES="+failures);File.WriteAllLines(Path.Combine(folder,"快捷键与界面配置验证.txt"),rows.ToArray(),new UTF8Encoding(true));Environment.ExitCode=failures==0?0:1;
        }
    }
}
