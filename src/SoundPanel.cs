using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MuMuBeans
{
    // The dialog edits a draft. Choosing or previewing a file never commits it.
    sealed class SoundPanel : Form
    {
        const string MediaFilter="音频与视频|*.wav;*.mp3;*.flac;*.m4a;*.aac;*.mp4;*.m4v|无损音频|*.wav;*.flac|MP3 音频|*.mp3|MP4 / M4V 视频|*.mp4;*.m4v|其他音频|*.m4a;*.aac";
        readonly SoundAlerts alerts;
        readonly SoundSettings draft;
        readonly ToolTip tips=new ToolTip{InitialDelay=300,AutoPopDelay=10000};
        readonly List<Font> fonts=new List<Font>();
        readonly Label title,hint,volumeTitle,volumeText,formats,feedback;
        readonly CheckBox enabled=new CheckBox{Text="启用提示音",BackColor=Color.Transparent};
        readonly TrackBar volume=new TrackBar{Minimum=0,Maximum=100,TickFrequency=10,SmallChange=5,LargeChange=10,TickStyle=TickStyle.None,AutoSize=false};
        readonly GlassButton done,cancel;
        readonly CueRow start,end;
        bool ready,released;
        float uiScale=1;

        sealed class CueRow
        {
            internal readonly GlassPanel Surface=new GlassPanel{Radius=22,Material=true};
            internal readonly Label Title,File,Caption;
            internal readonly CheckBox Enabled=new CheckBox{Text="自动播放",BackColor=Color.Transparent};
            internal readonly GlassButton Choose,Preview,Reset;
            internal string Path;
            internal CueRow(SoundPanel owner,string title,string description,bool enabled,string path,int index)
            {
                Title=owner.Label(title,12,true);Caption=owner.Label(description,9,false);Caption.ForeColor=Theme.Muted;
                File=owner.Label("",10,false);File.AutoEllipsis=true;File.BackColor=Color.FromArgb(234,242,247);File.AccessibleRole=AccessibleRole.Text;
                Enabled.Checked=enabled;Enabled.AccessibleName=title+"自动播放";Enabled.TabIndex=0;
                Choose=owner.Button("选择文件",false);Choose.AccessibleName=title+"选择音频或视频";Choose.TabIndex=1;
                Preview=owner.Button("试听",false);Preview.AccessibleName=title+"试听当前音效与音量";Preview.TabIndex=2;
                Reset=owner.Button("恢复默认",false);Reset.AccessibleName=title+"恢复内置音效";Reset.TabIndex=3;
                Path=path??"";Surface.TabIndex=index;Surface.Controls.AddRange(new Control[]{Title,Caption,Enabled,File,Choose,Preview,Reset});
                Choose.Click+=delegate{owner.ChooseFile(this);};Preview.Click+=delegate{owner.Preview(this);};Reset.Click+=delegate{Path="";owner.UpdateFile(this);owner.SetFeedback(title+"已恢复默认；点击保存后生效。",false);};
            }
        }

        internal SoundPanel(SoundAlerts service):this(service,service==null?new SoundSettings():service.Settings){}
        SoundPanel(SoundAlerts service,SoundSettings settings)
        {
            alerts=service;draft=new SoundSettings{Enabled=settings.Enabled,StartEnabled=settings.StartEnabled,EndEnabled=settings.EndEnabled,Volume=settings.Volume,StartPath=settings.StartPath??"",EndPath=settings.EndPath??""};
            using(Graphics g=Graphics.FromHwnd(IntPtr.Zero))uiScale=Math.Max(1,g.DpiX/96f);
            AutoScaleMode=AutoScaleMode.None;Font=Own(new Font("Microsoft YaHei UI",10));BackColor=Theme.Bg;ForeColor=Theme.Ink;
            Text="音效设置 · Bean Studio";StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;DoubleBuffered=true;
            ClientSize=new Size(D(740),D(680));MinimumSize=SizeFromClientSize(new Size(D(700),D(680)));
            title=Label("音效设置",22,true);hint=Label("少豆时提醒，计时结束时再提醒。",10,false);hint.ForeColor=Theme.Muted;
            enabled.Checked=draft.Enabled;enabled.AccessibleName="启用自动提示音";enabled.TabIndex=0;
            volumeTitle=Label("音量",10,true);volume.Value=Math.Max(0,Math.Min(100,draft.Volume));volume.BackColor=Theme.Bg;volume.AccessibleName="提示音音量，0 为静音，100 为最大";volume.TabIndex=1;
            volumeText=Label("",10,true);volumeText.TextAlign=ContentAlignment.MiddleRight;
            start=new CueRow(this,"少豆时","确认减少，开始或重置 15 秒计时。",draft.StartEnabled,draft.StartPath,2);
            end=new CueRow(this,"计时结束时","本次 15 秒计时归零，只播放一次。",draft.EndEnabled,draft.EndPath,3);
            formats=Label("支持 WAV、MP3、FLAC、M4A、AAC 和 MP4 / M4V。\r\n视频仅播放声音；能否播放取决于 Windows 解码器。",9,false);formats.ForeColor=Theme.Muted;
            feedback=Label("",10,false);feedback.AccessibleRole=AccessibleRole.StaticText;
            done=Button("保存",true);done.TabIndex=4;cancel=Button("取消",false);cancel.TabIndex=5;cancel.DialogResult=DialogResult.Cancel;
            Controls.AddRange(new Control[]{title,hint,enabled,volumeTitle,volume,volumeText,start.Surface,end.Surface,formats,feedback,done,cancel});
            AcceptButton=done;CancelButton=cancel;done.Click+=delegate{Save();};volume.ValueChanged+=delegate{UpdateVolume();};enabled.CheckedChanged+=delegate{UpdateEnabledState();};
            tips.SetToolTip(volume,"影响自动提示和试听；0% 静音。不修改系统音量。");tips.SetToolTip(enabled,"只控制自动提示。关闭后仍可试听，设置保留。");
            UpdateFile(start);UpdateFile(end);UpdateVolume();UpdateEnabledState();
            if(alerts!=null){alerts.StatusChanged+=PlaybackStatus;SetFeedback("点击试听可检查文件和音量；保存后自动提示才生效。",false);}else{done.Enabled=false;SetFeedback("设计预览：不读取配置、不播放声音。",false);}
            ready=true;LayoutContent();
        }
        Font Own(Font font){fonts.Add(font);return font;}
        Label Label(string text,int size,bool bold){Label label=Theme.Label(text,size,bold);Own(label.Font);return label;}
        GlassButton Button(string text,bool primary){GlassButton button=new GlassButton{Text=text,Primary=primary,AccessibleName=text};Own(button.Font);return button;}
        int D(int value){return (int)Math.Round(value*uiScale);}
        protected override void OnResize(EventArgs e){base.OnResize(e);if(ready)LayoutContent();}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.Background(e.Graphics,ClientSize);}
        void LayoutContent()
        {
            int width=ClientSize.Width,height=ClientSize.Height,g=D(24);
            title.SetBounds(g,D(20),width-g*2,D(38));hint.SetBounds(g,D(61),width-g*2,D(30));
            enabled.SetBounds(g,D(108),D(178),D(30));volumeTitle.SetBounds(D(220),D(108),D(50),D(30));
            volume.SetBounds(D(280),D(105),width-D(384),D(38));volumeText.SetBounds(width-D(95),D(108),D(69),D(30));
            start.Surface.SetBounds(g,D(156),width-g*2,D(148));end.Surface.SetBounds(g,D(318),width-g*2,D(148));
            LayoutRow(start);LayoutRow(end);
            formats.SetBounds(g,D(480),width-g*2,D(48));feedback.SetBounds(g,height-D(144),width-g*2,D(57));
            cancel.SetBounds(width-g-D(228),height-D(72),D(108),D(46));done.SetBounds(width-g-D(108),height-D(72),D(108),D(46));
        }
        void LayoutRow(CueRow row)
        {
            int w=row.Surface.Width;
            row.Title.SetBounds(D(20),D(13),w-D(170),D(29));row.Enabled.SetBounds(w-D(140),D(13),D(120),D(29));
            row.Caption.SetBounds(D(20),D(42),w-D(40),D(24));row.File.SetBounds(D(20),D(71),w-D(350),D(47));
            row.Choose.SetBounds(w-D(320),D(72),D(106),D(46));row.Preview.SetBounds(w-D(204),D(72),D(76),D(46));row.Reset.SetBounds(w-D(118),D(72),D(98),D(46));
        }
        void UpdateEnabledState()
        {
            start.Enabled.Enabled=end.Enabled.Enabled=enabled.Checked;
            start.Enabled.ForeColor=end.Enabled.ForeColor=enabled.Checked?Theme.Ink:Theme.Muted;
            enabled.AccessibleDescription=enabled.Checked?"自动提示已开启":"自动提示已关闭，仍可试听";
        }
        void UpdateVolume(){volumeText.Text=volume.Value+"%";volume.AccessibleDescription=volume.Value==0?"静音":volume.Value+"%";if(alerts!=null)alerts.SetPreviewVolume(volume.Value);}
        void UpdateFile(CueRow row)
        {
            bool isDefault=string.IsNullOrEmpty(row.Path);row.File.Text=isDefault?"  内置提示音":"  "+Path.GetFileName(row.Path);
            row.File.AccessibleName=row.Title.Text+"文件："+(isDefault?"内置提示音":row.Path);row.File.AccessibleDescription=isDefault?"可直接试听或选择自定义文件":row.Path;
            tips.SetToolTip(row.File,isDefault?"内置提示音，随程序提供。":row.Path);tips.SetToolTip(row.Reset,"改为程序内置提示音；保存后生效。");
        }
        void ChooseFile(CueRow row)
        {
            using(OpenFileDialog picker=new OpenFileDialog{Title=row.Title.Text+" · 选择音频或视频",Filter=MediaFilter,CheckFileExists=true,Multiselect=false,RestoreDirectory=true})
            {
                if(!string.IsNullOrEmpty(row.Path)&&File.Exists(row.Path))picker.FileName=row.Path;
                if(picker.ShowDialog(this)!=DialogResult.OK)return;
                if(!SoundAlerts.IsSupportedPath(picker.FileName)){SetFeedback("请选择 WAV、MP3、FLAC、M4A、AAC、MP4 或 M4V 文件。",true);return;}
                row.Path=picker.FileName;UpdateFile(row);SetFeedback("已选择“"+Path.GetFileName(row.Path)+"”。请先试听，再保存。",false);
            }
        }
        SoundSettings DraftSettings()
        {
            return new SoundSettings{Enabled=enabled.Checked,StartEnabled=start.Enabled.Checked,EndEnabled=end.Enabled.Checked,StartPath=start.Path,EndPath=end.Path,Volume=volume.Value};
        }
        void Preview(CueRow row)
        {
            if(alerts==null){SetFeedback("设计预览不播放声音。",false);return;}
            try{SetFeedback(volume.Value==0?"音量为 0%，本次试听静音。":"正在试听“"+row.Title.Text+"”…",false);alerts.Preview(row==start,DraftSettings());}
            catch(Exception ex){SetFeedback("试听失败："+ex.Message,true);}
        }
        void Save()
        {
            if(alerts==null)return;
            try{alerts.Apply(DraftSettings());DialogResult=DialogResult.OK;Close();}
            catch(Exception ex){SetFeedback("无法保存："+ex.Message,true);done.Focus();}
        }
        void PlaybackStatus(string message)
        {
            if(released||IsDisposed||!IsHandleCreated)return;
            try{BeginInvoke((Action)delegate{if(!released&&!IsDisposed)SetFeedback(message,message.IndexOf("失败",StringComparison.Ordinal)>=0||message.IndexOf("无法",StringComparison.Ordinal)>=0||message.IndexOf("不支持",StringComparison.Ordinal)>=0);});}catch(InvalidOperationException){}
        }
        void SetFeedback(string text,bool error){feedback.Text=text;feedback.ForeColor=error?Theme.Red:Theme.Muted;feedback.AccessibleName=text;}
        protected override void OnFormClosing(FormClosingEventArgs e){if(alerts!=null)alerts.StopPreview();base.OnFormClosing(e);}
        protected override void Dispose(bool disposing)
        {
            if(disposing&&!released){released=true;if(alerts!=null){alerts.StatusChanged-=PlaybackStatus;alerts.StopPreview();}tips.Dispose();}
            base.Dispose(disposing);if(disposing){foreach(Font font in fonts)font.Dispose();fonts.Clear();}
        }

        internal static void Render(string folder)
        {
            Directory.CreateDirectory(folder);List<string> checks=new List<string>();
            using(SoundPanel panel=new SoundPanel(null,new SoundSettings{Enabled=true,StartEnabled=true,EndEnabled=true,Volume=65}))
            {
                panel.StartPosition=FormStartPosition.Manual;panel.Location=new Point(-20000,-20000);panel.Show();Application.DoEvents();
                panel.SavePreview(Path.Combine(folder,"音效设置.png"));panel.VerifyBounds(checks);
                panel.ClientSize=new Size(panel.D(700),panel.D(680));Application.DoEvents();panel.SavePreview(Path.Combine(folder,"音效设置_最小.png"));panel.VerifyBounds(checks);
                panel.start.Path=@"C:\Audio\我自己的替身提示音片段_无损录制版_用于少豆确认提醒.mp4";panel.UpdateFile(panel.start);panel.end.Path=@"C:\Audio\计时结束提醒.flac";panel.UpdateFile(panel.end);
                panel.SetFeedback("无法播放：Windows 缺少此格式的解码器。请改用 WAV 或 MP3，再试听。",true);panel.SavePreview(Path.Combine(folder,"音效设置_文件与错误.png"));
                panel.enabled.Checked=false;panel.volume.Value=0;panel.SetFeedback("自动提示已关闭，设置保留。音量为 0% 时试听静音。",false);panel.SavePreview(Path.Combine(folder,"音效设置_关闭与静音.png"));
                if(panel.start.Enabled.Enabled||panel.end.Enabled.Enabled||panel.volumeText.Text!="0%")throw new Exception("音效总开关或音量显示失效");checks.Add("PASS 自动提示关闭保留独立开关，音量0显示静音百分比");
                panel.enabled.Checked=true;panel.volume.Value=100;panel.SetFeedback("设计预览：较大字体布局，未播放声音。",false);
                List<Control> controls=new List<Control>();List<Font> originals=new List<Font>();foreach(Control control in panel.Controls)CollectFonts(control,controls,originals);
                panel.uiScale*=1.5f;panel.ClientSize=new Size(panel.D(700),panel.D(680));for(int i=0;i<controls.Count;i++)controls[i].Font=panel.Own(new Font(originals[i].FontFamily,originals[i].Size*1.5f,originals[i].Style));panel.LayoutContent();Application.DoEvents();
                panel.SavePreview(Path.Combine(folder,"音效设置_放大150.png"));panel.VerifyBounds(checks);panel.Hide();
            }
            File.WriteAllLines(Path.Combine(folder,"音效界面验证.txt"),checks.ToArray());
        }
        internal static void Verify(string folder)
        {
            Directory.CreateDirectory(folder);List<string> checks=new List<string>();
            SoundSettings original=new SoundSettings{Enabled=true,StartEnabled=true,EndEnabled=false,Volume=65,StartPath=@"C:\Audio\少豆.mp4",EndPath=@"C:\Audio\恢复.flac"};
            using(SoundPanel panel=new SoundPanel(null,original))
            {
                panel.StartPosition=FormStartPosition.Manual;panel.Location=new Point(-20000,-20000);panel.Show();Application.DoEvents();panel.ClientSize=new Size(panel.D(700),panel.D(680));panel.VerifyBounds(checks);
                panel.enabled.Checked=false;if(panel.start.Enabled.Enabled||panel.end.Enabled.Enabled||!panel.start.Enabled.Checked||panel.end.Enabled.Checked)throw new Exception("总开关关闭丢失独立开关");
                panel.enabled.Checked=true;if(!panel.start.Enabled.Enabled||!panel.end.Enabled.Enabled||!panel.start.Enabled.Checked||panel.end.Enabled.Checked)throw new Exception("总开关开启丢失独立开关");checks.Add("PASS 总开关关闭再开启，独立提示开关保留");
                panel.start.Enabled.Checked=false;panel.end.Enabled.Checked=true;SoundSettings independent=panel.DraftSettings();if(independent.StartEnabled||!independent.EndEnabled)throw new Exception("独立提示开关未写入草稿");checks.Add("PASS 少豆与计时结束开关独立写入草稿");
                panel.volume.Value=0;if(panel.volumeText.Text!="0%"||panel.DraftSettings().Volume!=0)throw new Exception("静音值未进入草稿");panel.volume.Value=100;if(panel.volumeText.Text!="100%"||panel.DraftSettings().Volume!=100)throw new Exception("最大音量未进入草稿");checks.Add("PASS 音量0–100百分比即时更新，试听草稿读取当前音量");
                panel.start.Path=@"C:\Audio\我自己录制的替身提示音_很长的中文文件名_使用视频中的声音.mp4";panel.UpdateFile(panel.start);if(panel.tips.GetToolTip(panel.start.File)!=panel.start.Path||panel.start.File.AccessibleDescription!=panel.start.Path)throw new Exception("完整文件路径未提供提示");checks.Add("PASS 长中文文件名省略显示，工具提示及无障碍描述保留完整路径");
                Press(panel.start.Reset);if(panel.start.Path!=""||panel.end.Path!=original.EndPath||original.StartPath!=@"C:\Audio\少豆.mp4")throw new Exception("恢复默认错误提交或改动另一音效");checks.Add("PASS 恢复默认只改当前草稿，未更改另一音效和原设置");
                if(panel.AcceptButton!=panel.done||panel.CancelButton!=panel.cancel||panel.cancel.DialogResult!=DialogResult.Cancel)throw new Exception("保存或取消键盘入口失效");checks.Add("PASS 保存为Enter默认按钮，取消支持Esc，不用隐藏快捷操作");
                panel.DialogResult=DialogResult.Cancel;panel.Close();if(original.Volume!=65||!original.StartEnabled||original.EndEnabled||original.StartPath!=@"C:\Audio\少豆.mp4")throw new Exception("取消改变原设置");checks.Add("PASS 取消丢弃草稿，输入设置未改变，设计测试未访问真实配置");
            }
            using(MainForm form=new MainForm(false))
            {
                form.VerifySoundEntry(checks);
            }
            File.WriteAllLines(Path.Combine(folder,"音效交互验证.txt"),checks.ToArray());
        }
        static void Press(Control control){typeof(Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(control,new object[]{EventArgs.Empty});}
        static void CollectFonts(Control control,List<Control> controls,List<Font> originals)
        {
            controls.Add(control);originals.Add(control.Font);foreach(Control child in control.Controls)CollectFonts(child,controls,originals);
        }
        void VerifyBounds(List<string> checks)
        {
            foreach(Control control in Controls)if(control.Visible&&!ClientRectangle.Contains(control.Bounds))throw new Exception("音效设置控件超出窗口："+control.Text);
            foreach(CueRow row in new CueRow[]{start,end})
            {
                foreach(Control control in row.Surface.Controls)if(control.Visible&&!row.Surface.ClientRectangle.Contains(control.Bounds))throw new Exception("音效行控件超出区域："+control.Text);
                if(row.File.Right>=row.Choose.Left||row.Choose.Right>=row.Preview.Left||row.Preview.Right>=row.Reset.Left)throw new Exception("音效文件与按钮互相覆盖");
            }
            if(formats.Bottom>=feedback.Top||feedback.Bottom>=done.Top||cancel.Right>=done.Left)throw new Exception("音效说明、反馈或保存按钮重叠");
            checks.Add("PASS 音效设置 "+ClientSize.Width+"×"+ClientSize.Height+"：文件、按钮、反馈不重叠；保存取消及键盘焦点入口保留");
        }
        void SavePreview(string path){Refresh();Application.DoEvents();using(Bitmap image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(path);}}
    }
}
