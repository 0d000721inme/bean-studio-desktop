using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace MuMuBeans
{
    enum OverlayStyle { Focus, Digits, Ribbon, Classic }
    sealed class DesktopSettings
    {
        public bool HotkeysEnabled=true;
        public HotkeyChord ToggleOverlayHotkey=new HotkeyChord(HotkeyModifiers.Control|HotkeyModifiers.Alt,Keys.F8);
        public HotkeyChord RescanHotkey=new HotkeyChord(HotkeyModifiers.Control|HotkeyModifiers.Alt,Keys.F9);
        public OverlayStyle OverlayStyle=OverlayStyle.Focus;
        public int OverlayScale=100,OverlayBackground=18;
        public bool WholeSeconds=false,ShowRecovery=true,PerformanceVisible=true,PreviewVisible=true;
        public DesktopSettings Copy(){return (DesktopSettings)MemberwiseClone();}
    }
    static class DesktopSettingsStore
    {
        const int MaxBytes=16384;
        public static string DefaultPath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BeanStudio","desktop-settings.xml");}}
        public static void Validate(DesktopSettings settings)
        {
            if(settings==null)throw new ArgumentNullException("settings");
            if(!Enum.IsDefined(typeof(OverlayStyle),settings.OverlayStyle))throw new ArgumentException("悬浮窗风格不正确。");
            if(settings.OverlayScale<80||settings.OverlayScale>140)throw new ArgumentException("悬浮窗大小应为 80–140%。");
            if(settings.OverlayBackground<0||settings.OverlayBackground>40)throw new ArgumentException("悬浮窗背景强度应为 0–40%。");
            string error;
            if(!settings.ToggleOverlayHotkey.Valid(out error))throw new ArgumentException("悬浮窗："+error);
            if(!settings.RescanHotkey.Valid(out error))throw new ArgumentException("重新扫描："+error);
            if(!settings.ToggleOverlayHotkey.IsEmpty&&settings.ToggleOverlayHotkey.Equals(settings.RescanHotkey))throw new ArgumentException("悬浮窗和重新扫描不能使用相同的快捷键。");
        }
        static bool Bool(XmlElement root,string name,bool fallback){string text=root.GetAttribute(name);return text.Length==0?fallback:bool.Parse(text);}
        static int Int(XmlElement root,string name,int fallback){string text=root.GetAttribute(name);return text.Length==0?fallback:int.Parse(text,CultureInfo.InvariantCulture);}
        static HotkeyChord Chord(XmlElement root,string prefix,HotkeyChord fallback)
        {
            if(!root.HasAttribute(prefix+"Key")&&!root.HasAttribute(prefix+"Modifiers"))return fallback;
            return new HotkeyChord((HotkeyModifiers)Int(root,prefix+"Modifiers",0),(Keys)Int(root,prefix+"Key",0));
        }
        public static DesktopSettings Load(string path,out string error)
        {
            error="";
            try
            {
                if(!File.Exists(path))return new DesktopSettings();
                if(new FileInfo(path).Length>MaxBytes)throw new InvalidDataException("设置文件过大");
                XmlDocument document=new XmlDocument{XmlResolver=null};
                XmlReaderSettings readerSettings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaxBytes};
                using(XmlReader reader=XmlReader.Create(path,readerSettings))document.Load(reader);
                XmlElement root=document.DocumentElement;
                if(root==null||root.Name!="BeanStudioDesktop"||(root.GetAttribute("version")!="1"&&root.GetAttribute("version")!="2"))throw new InvalidDataException("设置格式不正确");
                DesktopSettings s=new DesktopSettings();s.HotkeysEnabled=Bool(root,"hotkeysEnabled",s.HotkeysEnabled);
                s.ToggleOverlayHotkey=Chord(root,"toggle",s.ToggleOverlayHotkey);s.RescanHotkey=Chord(root,"rescan",s.RescanHotkey);
                if(root.HasAttribute("overlayStyle"))s.OverlayStyle=(OverlayStyle)Enum.Parse(typeof(OverlayStyle),root.GetAttribute("overlayStyle"),false);
                s.OverlayScale=Int(root,"overlayScale",s.OverlayScale);s.OverlayBackground=Int(root,"overlayBackground",s.OverlayBackground);
                s.WholeSeconds=Bool(root,"wholeSeconds",s.WholeSeconds);s.ShowRecovery=Bool(root,"showRecovery",s.ShowRecovery);
                s.PerformanceVisible=Bool(root,"performanceVisible",s.PerformanceVisible);s.PreviewVisible=Bool(root,"previewVisible",s.PreviewVisible);
                // V5.8 defaulted to whole seconds and a secondary recovery line.
                // Upgrade those presentation choices once, preserving every other
                // preference. V2 files retain later explicit display choices.
                if(root.GetAttribute("version")=="1"){s.WholeSeconds=false;s.ShowRecovery=true;}
                Validate(s);return s;
            }
            catch(Exception ex){error="界面设置读取失败，已使用默认值："+ex.Message;return new DesktopSettings();}
        }
        static void Write(XmlWriter writer,string name,object value){writer.WriteAttributeString(name,Convert.ToString(value,CultureInfo.InvariantCulture));}
        public static void Save(string path,DesktopSettings settings)
        {
            Validate(settings);
            string absolute=Path.GetFullPath(path),folder=Path.GetDirectoryName(absolute);Directory.CreateDirectory(folder);
            string temporary=Path.Combine(folder,Path.GetFileName(absolute)+".tmp."+Guid.NewGuid().ToString("N"));
            try
            {
                XmlWriterSettings format=new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=true};
                using(FileStream stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(XmlWriter writer=XmlWriter.Create(stream,format))
                {
                    writer.WriteStartElement("BeanStudioDesktop");Write(writer,"version",2);Write(writer,"hotkeysEnabled",settings.HotkeysEnabled);
                    Write(writer,"toggleModifiers",(int)settings.ToggleOverlayHotkey.Modifiers);Write(writer,"toggleKey",(int)settings.ToggleOverlayHotkey.Key);
                    Write(writer,"rescanModifiers",(int)settings.RescanHotkey.Modifiers);Write(writer,"rescanKey",(int)settings.RescanHotkey.Key);
                    Write(writer,"overlayStyle",settings.OverlayStyle);Write(writer,"overlayScale",settings.OverlayScale);Write(writer,"overlayBackground",settings.OverlayBackground);
                    Write(writer,"wholeSeconds",settings.WholeSeconds);Write(writer,"showRecovery",settings.ShowRecovery);Write(writer,"performanceVisible",settings.PerformanceVisible);Write(writer,"previewVisible",settings.PreviewVisible);writer.WriteEndElement();
                }
                if(File.Exists(absolute))File.Replace(temporary,absolute,null);else File.Move(temporary,absolute);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
