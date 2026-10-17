using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MuMuBeans
{
    sealed class HotkeyInput : Control
    {
        [DllImport("user32.dll")]static extern short GetKeyState(int key);
        HotkeyChord value;
        public event Action<string> InputError;
        public HotkeyChord Value{get{return value;}set{this.value=value;AccessibleDescription=value.ToString();Invalidate();}}
        public HotkeyInput()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
            TabStop=true;Cursor=Cursors.Hand;BackColor=Theme.Bg;ForeColor=Theme.Ink;
            AccessibleRole=AccessibleRole.HotkeyField;
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left)Focus();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override bool ProcessCmdKey(ref Message message,Keys keyData)
        {
            Keys key=keyData&Keys.KeyCode;
            if(key==Keys.Escape||key==Keys.Tab||key==Keys.Enter&&ModifierKeys==Keys.None)return base.ProcessCmdKey(ref message,keyData);
            if((key==Keys.Delete||key==Keys.Back)&&ModifierKeys==Keys.None){Value=new HotkeyChord();Notify("");return true;}
            if(key==Keys.ControlKey||key==Keys.ShiftKey||key==Keys.Menu||key==Keys.LWin||key==Keys.RWin)return true;
            HotkeyModifiers modifiers=HotkeyModifiers.None;
            Keys pressed=keyData|ModifierKeys;
            if((pressed&Keys.Control)!=0)modifiers|=HotkeyModifiers.Control;
            if((pressed&Keys.Alt)!=0)modifiers|=HotkeyModifiers.Alt;
            if((pressed&Keys.Shift)!=0)modifiers|=HotkeyModifiers.Shift;
            if(GetKeyState((int)Keys.LWin)<0||GetKeyState((int)Keys.RWin)<0)modifiers|=HotkeyModifiers.Windows;
            HotkeyChord chord=new HotkeyChord(modifiers,key);string error;
            if(chord.Valid(out error)){Value=chord;Notify("");}else Notify(error);
            return true;
        }
        void Notify(string text){Action<string> handler=InputError;if(handler!=null)handler(text);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath shape=Theme.Round(new RectangleF(1,1,Width-3,Height-3),9))
            {
                using(Brush fill=new SolidBrush(Theme.Bg))g.FillPath(fill,shape);
                using(Pen outline=new Pen(Focused?Theme.Blue:Theme.Muted,Focused?2:1))g.DrawPath(outline,shape);
            }
            string text=value.IsEmpty&&Focused?"请按组合键…":value.ToString();
            TextRenderer.DrawText(g,text,Font,new Rectangle(14,0,Width-28,Height),Enabled?Theme.Ink:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
        }
    }

    // A settings draft is isolated from both the active HUD and the XML file.
    // Only a successful apply callback commits it. Cancel and Escape discard it.
    sealed class DesktopSettingsPanel : Form
    {
        readonly Func<DesktopSettings,string> apply;
        readonly ToolTip tips=new ToolTip{InitialDelay=300,AutoPopDelay=12000};
        readonly List<Font> fonts=new List<Font>();
        readonly Panel content=new Panel{AutoScroll=true};
        readonly Label title,hint,shortcutTitle,toggleLabel,rescanLabel,shortcutHint,overlayTitle,styleLabel,styleDescription,sizeLabel,sizeValue,backgroundLabel,backgroundValue,performanceHint,feedback;
        readonly CheckBox hotkeys=Check("启用全局快捷键"),whole=Check("只显示整秒，减少数字跳动"),recovery=Check("显示游戏时间恢复点"),performance=Check("显示 CPU / GPU / 内存图表"),preview=Check("显示扫描画面预览");
        readonly HotkeyInput toggle=new HotkeyInput(),rescan=new HotkeyInput();
        readonly ComboBox style=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat};
        readonly TrackBar scale=new TrackBar{Minimum=80,Maximum=140,TickStyle=TickStyle.None,SmallChange=5,LargeChange=10,AutoSize=false};
        readonly TrackBar background=new TrackBar{Minimum=0,Maximum=40,TickStyle=TickStyle.None,SmallChange=2,LargeChange=5,AutoSize=false};
        readonly GlassButton save,cancel,defaults;
        float dpi=1;
        bool ready,released;
        public DesktopSettings ResultSettings{get;private set;}
        static CheckBox Check(string text){return new CheckBox{Text=text,BackColor=Color.Transparent,AutoSize=false};}
        public DesktopSettingsPanel(DesktopSettings settings,Func<DesktopSettings,string> apply)
        {
            if(settings==null)throw new ArgumentNullException("settings");this.apply=apply;
            using(Graphics g=Graphics.FromHwnd(IntPtr.Zero))dpi=Math.Max(1,g.DpiX/96f);
            AutoScaleMode=AutoScaleMode.None;Font=Own(new Font("Microsoft YaHei UI",10));ForeColor=Theme.Ink;BackColor=Theme.Bg;
            Text="界面与快捷键 · Bean Studio";StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.Sizable;MinimizeBox=false;MaximizeBox=false;ShowInTaskbar=false;DoubleBuffered=true;
            ClientSize=new Size(D(760),D(720));MinimumSize=SizeFromClientSize(new Size(D(600),D(540)));
            title=Label("让提醒贴合你的节奏",21,true);hint=Label("更轻的悬浮窗、随手可用的快捷键，设置保存在这台电脑。",9,false);
            shortcutTitle=Label("快捷键",12,true);toggleLabel=Label("悬浮窗开关",10,false);rescanLabel=Label("重新扫描定位",10,false);
            shortcutHint=Label("点击输入框后按组合键；Backspace 清空单项。\r\n请使用 Ctrl / Alt / Win 组合，避免影响游戏按键。",9,false);
            overlayTitle=Label("悬浮窗",12,true);styleLabel=Label("风格",10,false);styleDescription=Label("",9,false);
            sizeLabel=Label("大小",10,false);sizeValue=Label("",10,true);backgroundLabel=Label("背景强度",10,false);backgroundValue=Label("",10,true);
            performanceHint=Label("关闭预览可减少绘制；豆子与游戏时间识别继续工作。\r\n性能图表只采样本程序，GPU 不可读时显示不可用。",9,false);
            feedback=Label("",9,false);save=Button("保存设置",true);cancel=Button("取消",false);defaults=Button("恢复默认",false);cancel.DialogResult=DialogResult.Cancel;
            foreach(Label muted in new Label[]{hint,shortcutHint,styleDescription,performanceHint})muted.ForeColor=Theme.Muted;
            style.Items.AddRange(new object[]{"专注 · 小巧与低干扰","纯数字 · 只保留倒计时","细条 · 适合贴在画面边缘","经典 · 完整状态"});style.AccessibleName="悬浮窗风格";
            toggle.AccessibleName="悬浮窗开关快捷键";rescan.AccessibleName="重新扫描快捷键";scale.AccessibleName="悬浮窗大小 80 到 140 百分比";background.AccessibleName="悬浮窗背景强度 0 到 40 百分比";
            content.BackColor=Theme.Bg;content.Controls.AddRange(new Control[]{shortcutTitle,hotkeys,toggleLabel,toggle,rescanLabel,rescan,shortcutHint,overlayTitle,styleLabel,style,styleDescription,sizeLabel,scale,sizeValue,backgroundLabel,background,backgroundValue,whole,recovery,performance,preview,performanceHint});
            Controls.AddRange(new Control[]{title,hint,content,feedback,defaults,cancel,save});
            foreach(Control control in content.Controls)if(!(control is Label)){control.Font=Font;control.ForeColor=Theme.Ink;control.TabIndex=content.Controls.GetChildIndex(control);}
            toggle.Font=Own(new Font("Consolas",11));rescan.Font=Own(new Font("Consolas",11));scale.BackColor=background.BackColor=Theme.Bg;
            title.TabStop=hint.TabStop=feedback.TabStop=false;content.TabIndex=0;defaults.TabIndex=1;cancel.TabIndex=2;save.TabIndex=3;AcceptButton=save;CancelButton=cancel;
            hotkeys.CheckedChanged+=delegate{toggle.Enabled=rescan.Enabled=hotkeys.Checked;};style.SelectedIndexChanged+=delegate{UpdateDescriptions();};scale.ValueChanged+=delegate{UpdateDescriptions();};background.ValueChanged+=delegate{UpdateDescriptions();};
            toggle.InputError+=InputError;rescan.InputError+=InputError;
            defaults.Click+=delegate{Fill(new DesktopSettings());SetFeedback("已恢复默认草稿；保存后生效。",false);};save.Click+=delegate{Commit();};
            tips.SetToolTip(toggle,"默认 Ctrl + Alt + F8。点击此框后按新组合，Backspace 清空该快捷键。");tips.SetToolTip(rescan,"默认 Ctrl + Alt + F9。放大模拟器后按一下，重新扫描豆子区域。");
            tips.SetToolTip(background,"0% 隐去背景，数字保持清晰；拖动窗口时可使用上方左右侧入口。");tips.SetToolTip(whole,"只改变显示精度；内部倒计时与音效依然使用完整 15 秒。");tips.SetToolTip(preview,"只隐藏主界面的预览绘制，不暂停实际识别。");
            Fill(settings.Copy());SetFeedback("修改后保存，Esc 取消。快捷键冲突会明确提示。",false);ready=true;LayoutContent();
        }
        Font Own(Font font){fonts.Add(font);return font;}
        Label Label(string text,int size,bool bold){Label label=Theme.Label(text,size,bold);Own(label.Font);return label;}
        GlassButton Button(string text,bool primary){GlassButton button=new GlassButton{Text=text,Primary=primary,AccessibleName=text};Own(button.Font);return button;}
        int D(int value){return (int)Math.Round(value*dpi);}
        protected override void OnResize(EventArgs e){base.OnResize(e);if(ready)LayoutContent();}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.Background(e.Graphics,ClientSize);}
        void LayoutContent()
        {
            int width=ClientSize.Width,height=ClientSize.Height,g=D(24);
            title.SetBounds(g,D(17),width-g*2,D(39));hint.SetBounds(g,D(61),width-g*2,D(32));
            int scrollY=-content.AutoScrollPosition.Y;content.SuspendLayout();content.AutoScrollPosition=Point.Empty;content.AutoScroll=false;
            content.SetBounds(g,D(104),width-g*2,Math.Max(D(250),height-D(242)));content.AutoScrollMinSize=new Size(0,D(484));
            int w=Math.Max(D(530),content.Width-SystemInformation.VerticalScrollBarWidth),field=D(174),gap=D(18);
            shortcutTitle.SetBounds(0,0,D(120),D(26));hotkeys.SetBounds(field,0,w-field,D(27));
            toggleLabel.SetBounds(0,D(40),field-gap,D(39));toggle.SetBounds(field,D(39),w-field,D(41));
            rescanLabel.SetBounds(0,D(91),field-gap,D(39));rescan.SetBounds(field,D(90),w-field,D(41));shortcutHint.SetBounds(field,D(139),w-field,D(48));
            overlayTitle.SetBounds(0,D(203),D(120),D(27));styleLabel.SetBounds(0,D(241),field-gap,D(31));style.SetBounds(field,D(241),w-field,D(34));styleDescription.SetBounds(field,D(279),w-field,D(30));
            sizeLabel.SetBounds(0,D(316),field-gap,D(31));scale.SetBounds(field,D(311),w-field-D(64),D(39));sizeValue.SetBounds(w-D(59),D(315),D(59),D(31));
            backgroundLabel.SetBounds(0,D(355),field-gap,D(31));background.SetBounds(field,D(352),w-field-D(64),D(39));backgroundValue.SetBounds(w-D(59),D(356),D(59),D(31));
            whole.SetBounds(field,D(400),w-field,D(28));recovery.SetBounds(field,D(432),w-field,D(28));
            performance.SetBounds(0,D(479),w,D(30));preview.SetBounds(0,D(514),w,D(30));performanceHint.SetBounds(0,D(551),w,D(48));content.AutoScrollMinSize=new Size(0,D(608));
            content.AutoScroll=true;content.ResumeLayout(true);content.AutoScrollPosition=new Point(0,scrollY);
            feedback.SetBounds(g,height-D(124),width-g*2,D(49));defaults.SetBounds(g,height-D(62),D(108),D(43));cancel.SetBounds(width-g-D(226),height-D(62),D(98),D(43));save.SetBounds(width-g-D(116),height-D(62),D(116),D(43));
        }
        void Fill(DesktopSettings settings)
        {
            hotkeys.Checked=settings.HotkeysEnabled;toggle.Value=settings.ToggleOverlayHotkey;rescan.Value=settings.RescanHotkey;toggle.Enabled=rescan.Enabled=hotkeys.Checked;
            style.SelectedIndex=(int)settings.OverlayStyle;scale.Value=settings.OverlayScale;background.Value=settings.OverlayBackground;whole.Checked=settings.WholeSeconds;recovery.Checked=settings.ShowRecovery;performance.Checked=settings.PerformanceVisible;preview.Checked=settings.PreviewVisible;UpdateDescriptions();
        }
        void UpdateDescriptions()
        {
            if(styleDescription==null)return;
            string[] descriptions={"冷却与恢复点并列，字号同级，状态放在次要位置。","无背景的双数字提醒，保留冷却和游戏恢复点。","横向双数字，适合贴近画面边缘查看。","双数字与状态较完整，恢复点同样醒目。"};
            styleDescription.Text=style.SelectedIndex>=0?descriptions[style.SelectedIndex]:"请选择风格";sizeValue.Text=scale.Value+"%";backgroundValue.Text=background.Value+"%";
        }
        internal DesktopSettings Draft()
        {
            return new DesktopSettings{HotkeysEnabled=hotkeys.Checked,ToggleOverlayHotkey=toggle.Value,RescanHotkey=rescan.Value,OverlayStyle=(OverlayStyle)style.SelectedIndex,OverlayScale=scale.Value,OverlayBackground=background.Value,WholeSeconds=whole.Checked,ShowRecovery=recovery.Checked,PerformanceVisible=performance.Checked,PreviewVisible=preview.Checked};
        }
        void InputError(string error){SetFeedback(error.Length==0?"组合键已写入草稿；保存时检查是否被占用。":error,error.Length>0);}
        void SetFeedback(string text,bool error){feedback.Text=text;feedback.ForeColor=error?Theme.Red:Theme.Muted;feedback.AccessibleName=text;tips.SetToolTip(feedback,text);}
        void Commit()
        {
            try
            {
                DesktopSettings draft=Draft();DesktopSettingsStore.Validate(draft);
                if(apply==null){SetFeedback("预览模式：不保存真实设置。",false);return;}
                string error=apply(draft.Copy());if(!string.IsNullOrEmpty(error)){SetFeedback(error,true);return;}
                ResultSettings=draft;DialogResult=DialogResult.OK;Close();
            }
            catch(Exception ex){SetFeedback("无法保存："+ex.Message,true);}
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&!released){released=true;tips.Dispose();}
            base.Dispose(disposing);if(disposing){foreach(Font font in fonts)font.Dispose();fonts.Clear();}
        }
        internal static void Render(string folder)
        {
            Directory.CreateDirectory(folder);List<string> checks=new List<string>();DesktopSettings original=new DesktopSettings();
            using(DesktopSettingsPanel panel=new DesktopSettingsPanel(original,null))
            {
                panel.StartPosition=FormStartPosition.Manual;panel.Location=new Point(-20000,-20000);panel.Show();Application.DoEvents();panel.VerifyBounds(checks);panel.SavePreview(Path.Combine(folder,"界面快捷键设置.png"));
                panel.content.AutoScrollPosition=new Point(0,panel.D(220));Application.DoEvents();panel.SavePreview(Path.Combine(folder,"界面快捷键设置_显示选项.png"));
                panel.ClientSize=new Size(panel.D(600),panel.D(540));panel.content.AutoScrollPosition=Point.Empty;Application.DoEvents();panel.VerifyBounds(checks);panel.SavePreview(Path.Combine(folder,"界面快捷键设置_最小.png"));
                panel.toggle.Value=new HotkeyChord(HotkeyModifiers.Control|HotkeyModifiers.Alt,Keys.F11);panel.rescan.Value=panel.toggle.Value;panel.Commit();
                if(panel.DialogResult==DialogResult.OK||panel.feedback.ForeColor!=Theme.Red||original.ToggleOverlayHotkey.Key!=Keys.F8)throw new Exception("非法重复快捷键未阻止保存或草稿改变原设置");checks.Add("PASS 重复快捷键显示错误，不提交，不改变输入对象");
                panel.Fill(new DesktopSettings());panel.hotkeys.Checked=false;if(panel.toggle.Enabled||panel.rescan.Enabled||panel.Draft().ToggleOverlayHotkey.Key!=Keys.F8)throw new Exception("关闭快捷键丢失组合或录入控件仍可用");checks.Add("PASS 全局快捷键关闭禁用录入，但保留自定义组合");
                panel.scale.Value=140;panel.background.Value=0;panel.style.SelectedIndex=2;DesktopSettings draft=panel.Draft();
                if(draft.OverlayScale!=140||draft.OverlayBackground!=0||draft.OverlayStyle!=OverlayStyle.Ribbon||panel.sizeValue.Text!="140%"||panel.backgroundValue.Text!="0%")throw new Exception("设置值或百分比反馈错误");checks.Add("PASS 风格、大小与背景变化只修改草稿，百分比及时可见");
                if(panel.AcceptButton!=panel.save||panel.CancelButton!=panel.cancel||panel.cancel.DialogResult!=DialogResult.Cancel)throw new Exception("保存或取消键盘入口错误");checks.Add("PASS 保存为 Enter，取消为 Esc，固定页脚保持可见");
                panel.SetFeedback("重新扫描快捷键 Ctrl + Alt + F9 已被其他程序占用。请更换组合，或关闭全局快捷键。",true);panel.SavePreview(Path.Combine(folder,"界面快捷键设置_冲突.png"));panel.Hide();
            }
            using(DesktopSettingsPanel panel=new DesktopSettingsPanel(new DesktopSettings(),delegate(DesktopSettings draft){return "模拟保存失败：未写入文件。";}))
            {
                panel.Commit();if(panel.DialogResult==DialogResult.OK||panel.ResultSettings!=null||!panel.feedback.Text.Contains("模拟保存失败"))throw new Exception("保存委托失败被误报成功");checks.Add("PASS 保存失败保持设置窗口，不伪称成功");
            }
            using(DesktopSettingsPanel panel=new DesktopSettingsPanel(new DesktopSettings(),delegate(DesktopSettings draft){return "";}))
            {
                panel.preview.Checked=false;panel.Commit();if(panel.DialogResult!=DialogResult.OK||panel.ResultSettings==null||panel.ResultSettings.PreviewVisible)throw new Exception("保存委托成功未返回草稿");checks.Add("PASS 仅成功保存后返回独立的新设置");
            }
            File.WriteAllLines(Path.Combine(folder,"界面快捷键布局验证.txt"),checks.ToArray(),new UTF8Encoding(true));
        }
        void VerifyBounds(List<string> checks)
        {
            foreach(Control child in Controls)if(child.Visible&&!ClientRectangle.Contains(child.Bounds))throw new Exception("设置控件超出窗口："+child.Text);
            if(title.Bottom>=hint.Top||hint.Bottom>=content.Top||content.Bottom>=feedback.Top||feedback.Bottom>=save.Top||cancel.Right>=save.Left||defaults.Right>=cancel.Left)throw new Exception("设置页标题、内容、反馈或固定页脚重叠");
            int available=content.Width-SystemInformation.VerticalScrollBarWidth;
            foreach(Control child in content.Controls)if(child.Right>available||child.Left<0||child.Bottom>content.AutoScrollMinSize.Height)throw new Exception("设置内容超出滚动范围："+child.Text+" "+child.Bounds+" 可用宽 "+available+" 内容宽 "+content.Width);
            if(toggle.Left<=toggleLabel.Right||rescan.Left<=rescanLabel.Right||scale.Right>=sizeValue.Left||background.Right>=backgroundValue.Left)throw new Exception("快捷键标签、滑条与数值重叠");
            checks.Add("PASS "+ClientSize.Width+"×"+ClientSize.Height+"：内容可滚动，固定保存取消与错误反馈无重叠");
        }
        void SavePreview(string path){Refresh();Application.DoEvents();using(Bitmap image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(path);}}
    }
}
