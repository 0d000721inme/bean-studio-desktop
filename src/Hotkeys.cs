using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MuMuBeans
{
    [Flags]
    enum HotkeyModifiers { None=0, Alt=1, Control=2, Shift=4, Windows=8 }
    enum DesktopHotkeyAction { ToggleOverlay, Rescan }

    struct HotkeyChord : IEquatable<HotkeyChord>
    {
        public HotkeyModifiers Modifiers;
        public Keys Key;
        public HotkeyChord(HotkeyModifiers modifiers,Keys key){Modifiers=modifiers;Key=key;}
        public bool IsEmpty{get{return Key==Keys.None;}}
        public bool Equals(HotkeyChord other){return Modifiers==other.Modifiers&&Key==other.Key;}
        public override bool Equals(object other){return other is HotkeyChord&&Equals((HotkeyChord)other);}
        public override int GetHashCode(){return ((int)Modifiers*397)^(int)Key;}
        public bool Valid(out string error)
        {
            error="";
            if(IsEmpty){if(Modifiers!=HotkeyModifiers.None)error="空快捷键不能含修饰键。";return error.Length==0;}
            if(((int)Modifiers&~15)!=0){error="快捷键修饰键不正确。";return false;}
            if((Modifiers&(HotkeyModifiers.Control|HotkeyModifiers.Alt|HotkeyModifiers.Windows))==0){error="请同时按 Ctrl、Alt 或 Win，避免影响游戏中的普通按键。";return false;}
            int key=(int)Key;
            if(key<8||key>254||Key==Keys.ShiftKey||Key==Keys.ControlKey||Key==Keys.Menu||Key==Keys.LWin||Key==Keys.RWin||Key==Keys.LShiftKey||Key==Keys.RShiftKey||Key==Keys.LControlKey||Key==Keys.RControlKey||Key==Keys.LMenu||Key==Keys.RMenu){error="请选择一个普通按键或功能键。";return false;}
            if(Key==Keys.F12){error="F12 被 Windows 保留，请选择其他按键。";return false;}
            return true;
        }
        public override string ToString()
        {
            if(IsEmpty)return "未设置";
            string value="";
            if((Modifiers&HotkeyModifiers.Control)!=0)value+="Ctrl + ";
            if((Modifiers&HotkeyModifiers.Alt)!=0)value+="Alt + ";
            if((Modifiers&HotkeyModifiers.Shift)!=0)value+="Shift + ";
            if((Modifiers&HotkeyModifiers.Windows)!=0)value+="Win + ";
            string key=Key.ToString();
            if(Key>=Keys.D0&&Key<=Keys.D9)key=((int)Key-(int)Keys.D0).ToString();
            if(Key==Keys.Oemcomma)key=",";else if(Key==Keys.OemPeriod)key=".";
            return value+key;
        }
    }

    interface IHotkeyRegistration
    {
        bool Register(IntPtr handle,int id,uint modifiers,uint key,out int error);
        void Unregister(IntPtr handle,int id);
    }
    sealed class Win32HotkeyRegistration : IHotkeyRegistration
    {
        [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr hWnd,int id,uint fsModifiers,uint vk);
        [DllImport("user32.dll",SetLastError=true)]static extern bool UnregisterHotKey(IntPtr hWnd,int id);
        public bool Register(IntPtr handle,int id,uint modifiers,uint key,out int error){bool ok=RegisterHotKey(handle,id,modifiers,key);error=ok?0:Marshal.GetLastWin32Error();return ok;}
        public void Unregister(IntPtr handle,int id){UnregisterHotKey(handle,id);}
    }

    // Windows sends WM_HOTKEY only for the reserved combinations. No keyboard hook,
    // key polling, or background worker is used; repeated key-down is suppressed.
    sealed class HotkeyManager : IDisposable
    {
        internal const int ToggleId=0xB581,RescanId=0xB582,HotkeyMessage=0x0312;
        internal const uint NoRepeat=0x4000;
        readonly Form owner;
        readonly Action<DesktopHotkeyAction> callback;
        readonly IHotkeyRegistration native;
        readonly HashSet<int> registered=new HashSet<int>();
        DesktopSettings settings=new DesktopSettings();
        IntPtr handle;
        bool suspended,disposed;
        public event Action<string> StatusChanged;
        public string Status{get;private set;}
        public DesktopSettings Settings{get{return settings.Copy();}}

        public HotkeyManager(Form owner,Action<DesktopHotkeyAction> callback):this(owner,callback,new Win32HotkeyRegistration()){}
        internal HotkeyManager(Form owner,Action<DesktopHotkeyAction> callback,IHotkeyRegistration native)
        {
            if(owner==null)throw new ArgumentNullException("owner");
            if(native==null)throw new ArgumentNullException("native");
            this.owner=owner;this.callback=callback;this.native=native;Status="快捷键等待窗口准备";
            owner.HandleCreated+=HandleCreated;owner.HandleDestroyed+=HandleDestroyed;owner.FormClosed+=FormClosed;
            if(owner.IsHandleCreated){handle=owner.Handle;string error;RegisterSettings(settings,out error);Publish(error.Length==0?SuccessStatus():error);}
        }
        void HandleCreated(object sender,EventArgs e)
        {
            if(disposed)return;handle=owner.Handle;
            if(suspended)return;
            string error;RegisterSettings(settings,out error);Publish(error.Length==0?SuccessStatus():error);
        }
        void HandleDestroyed(object sender,EventArgs e){UnregisterAll();handle=IntPtr.Zero;}
        void FormClosed(object sender,FormClosedEventArgs e){Dispose();}
        string SuccessStatus(){return settings.HotkeysEnabled?"全局快捷键已启用":"全局快捷键已关闭";}
        void Publish(string message){Status=message;Action<string> changed=StatusChanged;if(changed!=null)changed(message);}
        void UnregisterAll()
        {
            foreach(int id in registered)native.Unregister(handle,id);
            registered.Clear();
        }
        bool RegisterOne(HotkeyChord chord,int id,string action,out string error)
        {
            error="";if(chord.IsEmpty)return true;
            int code;
            if(native.Register(handle,id,(uint)chord.Modifiers|NoRepeat,(uint)chord.Key,out code)){registered.Add(id);return true;}
            error=action+"快捷键 "+chord+" 注册失败"+(code==1409?"，已被其他程序占用。":"（Windows 错误 "+code+"）。")+"请更换组合，或关闭全局快捷键。";
            return false;
        }
        bool RegisterSettings(DesktopSettings request,out string error)
        {
            error="";if(!request.HotkeysEnabled||handle==IntPtr.Zero)return true;
            if(!RegisterOne(request.ToggleOverlayHotkey,ToggleId,"悬浮窗",out error)||!RegisterOne(request.RescanHotkey,RescanId,"重新扫描",out error)){UnregisterAll();return false;}
            return true;
        }
        // Failed changes restore previous reservations rather than silently losing
        // a working binding. The caller persists XML only after this succeeds.
        public bool TryApply(DesktopSettings request,out string error)
        {
            error="";if(disposed){error="窗口已关闭，无法设置快捷键。";return false;}
            try{DesktopSettingsStore.Validate(request);}catch(ArgumentException ex){error=ex.Message;return false;}
            DesktopSettings previous=settings;UnregisterAll();
            if(!RegisterSettings(request,out error))
            {
                string restoreError;
                if(!suspended&&!RegisterSettings(previous,out restoreError))error+=" 原快捷键恢复失败："+restoreError;
                Publish(error);return false;
            }
            settings=request.Copy();if(suspended)UnregisterAll();Publish(suspended?"正在设置快捷键；关闭设置后恢复":SuccessStatus());return true;
        }
        public void Suspend(){if(disposed||suspended)return;suspended=true;UnregisterAll();Publish("正在设置快捷键；关闭设置后恢复");}
        public bool Resume(out string error)
        {
            error="";if(disposed){error="窗口已关闭。";return false;}
            if(!suspended)return true;suspended=false;
            bool ok=RegisterSettings(settings,out error);Publish(ok?SuccessStatus():error);return ok;
        }
        public bool ProcessMessage(ref Message message)
        {
            if(message.Msg!=HotkeyMessage)return false;
            int id=message.WParam.ToInt32();
            if(id!=ToggleId&&id!=RescanId)return false;
            if(disposed||suspended||!registered.Contains(id))return true;
            // Already queued messages from a replaced binding are ignored.
            uint detail=unchecked((uint)message.LParam.ToInt64());
            HotkeyChord chord=id==ToggleId?settings.ToggleOverlayHotkey:settings.RescanHotkey;
            if((detail&0xffff)!=(uint)chord.Modifiers||(detail>>16)!=(uint)chord.Key)return true;
            if(callback!=null)callback(id==ToggleId?DesktopHotkeyAction.ToggleOverlay:DesktopHotkeyAction.Rescan);
            return true;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;UnregisterAll();
            owner.HandleCreated-=HandleCreated;owner.HandleDestroyed-=HandleDestroyed;owner.FormClosed-=FormClosed;handle=IntPtr.Zero;
        }
    }
}
