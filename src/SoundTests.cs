using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Windows.Media.Playback;

namespace MuMuBeans
{
    static class SoundTests
    {
        static List<string> rows=new List<string>();static int failures;
        static void Check(bool pass,string title){rows.Add((pass?"PASS ":"FAIL ")+title);if(!pass)failures++;}
        static Snapshot Snap(int triggers,double now,double deadline){return new Snapshot{Triggers=triggers,ClockNow=now,CooldownDeadline=deadline};}
        static void Finish(string folder,string name)
        {
            rows.Add("PASS_COUNT="+rows.FindAll(delegate(string s){return s.StartsWith("PASS ");}).Count);rows.Add("FAILURES="+failures);
            File.WriteAllLines(Path.Combine(folder,name),rows.ToArray(),new UTF8Encoding(true));Environment.ExitCode=failures==0?0:1;
        }
        public static void Run(string[] args)
        {
            string folder=args[1];Directory.CreateDirectory(folder);rows=new List<string>();failures=0;
            try
            {
                CooldownSoundTracker t=new CooldownSoundTracker();Check(t.Update(Snap(0,0,0))==SoundCue.None,"首次读数没有启动或结束音");
                Check(t.Update(Snap(1,2,17))==SoundCue.Start,"确认少一豆启动一次开始音");
                bool stable=true;for(int i=0;i<100;i++)stable&=t.Update(Snap(1,2+i*.02,17))==SoundCue.None;Check(stable,"连续100次保持同一计时不重复播放");
                Snapshot unknown=Snap(1,4,17);unknown.Reading=new Reading{Valid=false};Check(t.Update(unknown)==SoundCue.None&&t.Armed,"未知/黑屏不启动或撤销原完成音");
                Snapshot growth=Snap(1,5,17);growth.Count=4;Check(t.Update(growth)==SoundCue.None,"增长不触发音效");
                Check(t.Update(Snap(1,16.999,17))==SoundCue.None,"15秒截止前不提前结束");
                Snapshot paused=Snap(1,17,17);paused.Paused=true;Check(t.Update(paused)==SoundCue.End,"暂停时原计时到期仍结束一次");
                Check(t.Update(Snap(1,18,17))==SoundCue.None&&!t.Armed,"结束后不重复播放完成音");
                Check(t.Update(Snap(2,20,35))==SoundCue.Start,"再次少豆启动新提示音");
                Check(t.Update(Snap(3,22,37))==SoundCue.Start,"计时中再次少豆重置开始音与完成时刻");
                Check(t.Update(Snap(3,35,37))==SoundCue.None,"被替换的旧截止时刻没有结束音");
                Check(t.Update(Snap(3,37,37))==SoundCue.End,"仅新截止时刻结束");
                Check(t.Update(Snap(4,40,55))==SoundCue.Start,"最后一豆变0仍是一次少豆事件");
                Snapshot zero=Snap(4,41,55);zero.Count=0;zero.Enabled=true;zero.ConfirmedReading=new Reading();Check(zero.NoBeans&&t.Update(zero)==SoundCue.None,"无豆文字不重复触发音效");
                Snapshot cleared=Snap(4,42,0);cleared.ResetVersion=1;Check(t.Update(cleared)==SoundCue.None&&!t.Armed,"手动清除静默取消完成提示");
                cleared.ClockNow=60;Check(t.Update(cleared)==SoundCue.None,"清除后的旧15秒不再响");
                Snapshot next=Snap(5,61,76);next.ResetVersion=1;Check(t.Update(next)==SoundCue.Start,"清除后下一次少豆恢复提示");
                next.CooldownDeadline=0;next.ClockNow=62;Check(t.Update(next)==SoundCue.None&&!t.Armed,"截止时间为0静默取消");
                CooldownSoundTracker attached=new CooldownSoundTracker();Check(attached.Update(Snap(12,10,20))==SoundCue.None&&!attached.Armed,"服务初始不补响已存在的历史事件");
                CooldownSoundTracker corrected=new CooldownSoundTracker();corrected.Update(Snap(0,0,0));corrected.Update(Snap(1,1,16));
                Check(corrected.Update(Snap(1,12,20))==SoundCue.None&&corrected.Update(Snap(1,16,20))==SoundCue.None&&corrected.Update(Snap(1,20,20))==SoundCue.End,"完成提示始终跟随权威截止时间");
                Check(SoundAlerts.IsSupportedPath("test.WAV")&&SoundAlerts.IsSupportedPath("test.mp3")&&SoundAlerts.IsSupportedPath("test.FLAC")&&SoundAlerts.IsSupportedPath("test.m4a")&&SoundAlerts.IsSupportedPath("test.mp4")&&!SoundAlerts.IsSupportedPath("test.exe"),"允许音频/视频扩展名，拒绝可执行文件");

                string config=Path.Combine(folder,"settings-test.xml"),error;SoundSettings loaded=SoundSettingsStore.Load(config,out error);
                Check(error==""&&loaded.Enabled&&loaded.StartEnabled&&loaded.EndEnabled&&loaded.Volume==70,"无配置使用默认启用及70%音量");
                SoundSettings custom=new SoundSettings{Enabled=false,StartEnabled=false,EndEnabled=true,Volume=0,StartPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sounds","start.wav")};
                SoundSettingsStore.Save(config,custom);loaded=SoundSettingsStore.Load(config,out error);
                Check(error==""&&!loaded.Enabled&&!loaded.StartEnabled&&loaded.EndEnabled&&loaded.Volume==0&&loaded.StartPath==custom.StartPath,"原子写入与全部字段读取，包含0%静音");
                custom.Volume=100;SoundSettingsStore.Save(config,custom);Check(SoundSettingsStore.Load(config,out error).Volume==100,"原子替换已存在配置支持100%音量");
                Check(Directory.GetFiles(folder,"*.tmp.*").Length==0,"配置提交不遗留临时文件");
                custom.Volume=101;bool rejected=false;try{SoundSettingsStore.Save(config,custom);}catch(ArgumentException){rejected=true;}Check(rejected&&SoundSettingsStore.Load(config,out error).Volume==100,"非法音量不覆盖已有配置");
                File.WriteAllText(config,"<BeanStudioSounds version='1' volume='oops'/>");loaded=SoundSettingsStore.Load(config,out error);Check(error.Length>0&&loaded.Volume==70,"损坏配置回退默认值，异常非致命");
                File.WriteAllText(config,"<!DOCTYPE x [<!ENTITY data SYSTEM 'file:///C:/Windows/win.ini'>]><BeanStudioSounds version='1'>&data;</BeanStudioSounds>");loaded=SoundSettingsStore.Load(config,out error);Check(error.Length>0&&loaded.Volume==70,"配置禁止DTD及外部实体读取");
                File.WriteAllText(config,new string('a',17000));loaded=SoundSettingsStore.Load(config,out error);Check(error.Length>0,"限制配置大小，拒绝超大文件");
                File.WriteAllText(config,"<BeanStudioSounds version='1' enabled='true' startEnabled='true' endEnabled='true' volume='70' startPath='C:\\missing-sound.mp3' endPath='' />");loaded=SoundSettingsStore.Load(config,out error);Check(error.Length==0&&loaded.StartPath=="C:\\missing-sound.mp3","已删除的自定义文件保留选择供UI提示，解码阶段非致命");
                custom.Volume=70;custom.StartPath=Path.Combine(folder,"missing.mp3");rejected=false;try{SoundSettingsStore.Validate(custom,true);}catch(ArgumentException){rejected=true;}Check(rejected,"保存时拒绝不存在的自定义文件");
                custom.StartPath="relative.mp3";rejected=false;try{SoundSettingsStore.Validate(custom,true);}catch(ArgumentException){rejected=true;}Check(rejected,"只记录明确的绝对文件路径");
                custom.StartPath="";string failed=Path.Combine(folder,"not-a-directory");File.WriteAllText(failed,"fixture");rejected=false;try{SoundSettingsStore.Save(Path.Combine(failed,"settings.xml"),custom);}catch(IOException){rejected=true;}Check(rejected,"保存失败可见并不伪称成功");
            }
            catch(Exception ex){Check(false,ex.ToString());}
            Finish(folder,"音效事件与配置验证.txt");
        }

        static bool Wait(Func<bool> done,int ms)
        {
            Stopwatch timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<ms){if(done())return true;Application.DoEvents();Thread.Sleep(10);}return done();
        }
        static MediaPlayer Player(SoundAlerts alerts)
        {
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            object cue=typeof(SoundAlerts).GetField("previewCue",flags).GetValue(alerts);
            return (MediaPlayer)cue.GetType().GetField("player",flags).GetValue(cue);
        }
        static bool CueReady(SoundAlerts alerts,string field)
        {
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            object cue=typeof(SoundAlerts).GetField(field,flags).GetValue(alerts);
            return (bool)cue.GetType().GetField("ready",flags).GetValue(cue);
        }
        static void ObserveCount(Engine engine,int count,int frames)
        {
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            object gate=typeof(Engine).GetField("gate",flags).GetValue(engine);
            Counter counter=(Counter)typeof(Engine).GetField("counter",flags).GetValue(engine);
            for(int i=0;i<frames;i++)
            {
                lock(gate)counter.Observe(count,engine.GetSnapshot().ClockNow);
                Thread.Sleep(22);
            }
        }
        public static void EngineAudio(string[] args)
        {
            string folder=args[1];Directory.CreateDirectory(folder);rows=new List<string>();failures=0;
            try
            {
                string config=Path.Combine(folder,"engine-sounds.xml");SoundSettingsStore.Save(config,new SoundSettings{Volume=0});
                using(Engine engine=new Engine())using(SoundAlerts alerts=new SoundAlerts(engine,config,AppDomain.CurrentDomain.BaseDirectory))
                {
                    object gate=new object();int starts=0,ends=0;double endAt=-1;
                    alerts.StatusChanged+=delegate(string value){lock(gate){if(value=="开始音效已开始")starts++;if(value=="结束音效已开始"){ends++;endAt=engine.GetSnapshot().ClockNow;}}};
                    Check(Wait(delegate{return CueReady(alerts,"startCue")&&CueReady(alerts,"endCue");},6000),"生产开始/结束音完成预加载（测试音量0）");
                    ObserveCount(engine,4,3);Thread.Sleep(60);lock(gate)Check(starts==0&&ends==0,"Engine首次基准没有音效");
                    ObserveCount(engine,3,3);double firstDeadline=engine.GetSnapshot().CooldownDeadline;
                    Check(Wait(delegate{lock(gate)return starts==1;},1000),"真实Counter少豆经Engine快照启动生产提示音");
                    ObserveCount(engine,4,7);lock(gate)Check(starts==1&&engine.GetSnapshot().CooldownDeadline==firstDeadline,"真实增长没有新提示，也不改截止时间");
                    ObserveCount(engine,3,3);double newDeadline=engine.GetSnapshot().CooldownDeadline;
                    Check(Wait(delegate{lock(gate)return starts==2;},1000)&&newDeadline>firstDeadline,"计时中再次消耗重新播放，替换完成时间");
                    engine.SetPaused(true);
                    Check(Wait(delegate{return engine.GetSnapshot().ClockNow>=firstDeadline+.03;},17000),"等待真实单调时钟经过旧截止点");
                    lock(gate)Check(ends==0,"旧截止点不播放结束音，后台暂停不影响当前15秒");
                    Check(Wait(delegate{lock(gate)return ends==1;},2000),"新15秒自然到期实际播放结束音");
                    lock(gate)Check(endAt>=newDeadline&&endAt-newDeadline<.25,"生产结束事件发生在权威截止后250ms以内（内部链路）");
                    Thread.Sleep(160);lock(gate)Check(ends==1,"到期多次轮询仍只有一个结束提示");
                    ObserveCount(engine,2,3);ObserveCount(engine,1,3);Check(Wait(delegate{lock(gate)return starts==3;},1000),"暂停保留计时逻辑，下一次Counter消耗可测试开始提示");
                    engine.Reset();Thread.Sleep(100);lock(gate)Check(engine.GetSnapshot().CooldownDeadline==0&&ends==1,"手动清空保留reset标识并取消完成提示");
                    File.WriteAllText(Path.Combine(folder,"范围说明.txt"),"捕获源始终为空，未操作或捕获MuMu。只将真实计数状态输入Counter，经过Engine生产快照、后台音效监视器和真实MediaPlayer执行。音量0；此内部测试不是声卡可听输出或实战端到端延迟测量。",new UTF8Encoding(true));
                }
            }
            catch(Exception ex){Check(false,ex.ToString());}
            Finish(folder,"音效实际计时链路验证.txt");
        }
        public static void Media(string[] args)
        {
            string fixtures=args[1],folder=args[2];Directory.CreateDirectory(folder);rows=new List<string>();failures=0;
            try
            {
                string config=Path.Combine(folder,"media-settings.xml");SoundSettings disabled=new SoundSettings{Enabled=false,StartEnabled=false,EndEnabled=false,Volume=0};SoundSettingsStore.Save(config,disabled);
                using(SoundAlerts alerts=new SoundAlerts(null,config,AppDomain.CurrentDomain.BaseDirectory))
                {
                    List<string> states=new List<string>();object gate=new object();alerts.StatusChanged+=delegate(string s){lock(gate)states.Add(s);};
                    foreach(string ext in new string[]{"wav","mp3","flac","m4a","mp4"})
                    {
                        lock(gate)states.Clear();SoundSettings draft=disabled.Copy();draft.StartPath=Path.GetFullPath(Path.Combine(fixtures,"sample."+ext));int windows=Application.OpenForms.Count;
                        alerts.Preview(true,draft);
                        Check(Wait(delegate{lock(gate)return states.Exists(delegate(string s){return s=="试听已开始";});},6000),ext+"：生产后端读取音轨并开始播放（自动开关关闭仍可试听）");
                        MediaPlayer player=Player(alerts);bool muted=false,controls=false,progress=false;
                        if(player!=null)
                        {
                            muted=player.Volume==0;controls=!player.CommandManager.IsEnabled&&!player.SystemMediaTransportControls.IsEnabled;
                            progress=Wait(delegate{try{return player.PlaybackSession.Position.TotalSeconds>.15;}catch{return false;}},1500);
                        }
                        Check(muted&&controls,ext+"：0%音量且禁用媒体键/SMTC");Check(progress,ext+"：播放时间实际前进");
                        Check(Wait(delegate{lock(gate)return states.Exists(delegate(string s){return s=="试听已结束";});},4000),ext+"：生产播放器收到实际完成事件");
                        Check(Wait(delegate{return Player(alerts)==null;},1500)&&Application.OpenForms.Count==windows,ext+"：试听完成释放且没有视频窗口");
                    }
                    lock(gate)states.Clear();SoundSettings bad=disabled.Copy();bad.StartPath=Path.GetFullPath(Path.Combine(fixtures,"corrupt.mp3"));alerts.Preview(true,bad);
                    Check(Wait(delegate{lock(gate)return states.Exists(delegate(string s){return s.Contains("无法播放")||s.Contains("无法加载");});},6000),"损坏MP3显示非致命解码错误");
                    lock(gate)states.Clear();bad.StartPath=Path.GetFullPath(Path.Combine(fixtures,"silent.mp4"));alerts.Preview(true,bad);
                    Check(Wait(delegate{lock(gate)return states.Exists(delegate(string s){return s.Contains("未找到音轨");});},6000),"无音轨MP4明确提示，不能假装成功");
                    lock(gate)Check(!states.Contains("试听已开始"),"无音轨视频没有成功播放提示");
                    SoundSettings good=disabled.Copy();good.StartPath=Path.GetFullPath(Path.Combine(fixtures,"sample.wav"));good.Volume=1;lock(gate)states.Clear();alerts.Preview(true,good);
                    Check(Wait(delegate{lock(gate)return states.Contains("试听已开始");},6000),"解码错误后重新选择有效文件可恢复");
                    MediaPlayer p=Player(alerts);Check(p!=null&&Math.Abs(p.Volume-.01)<.00001,"试听采用草稿1%音量");
                    alerts.SetPreviewVolume(100);Check(p!=null&&p.Volume==1&&alerts.Settings.Volume==0,"试听中调到100%立即生效，不改已保存音量");
                    alerts.SetPreviewVolume(0);Check(p!=null&&p.Volume==0,"试听中调到0%立即静音");
                    alerts.StopPreview();Check(Player(alerts)==null,"停止试听释放播放器与媒体源");
                    string deleted=Path.Combine(folder,"deleted.wav");File.Copy(good.StartPath,deleted,true);good.StartPath=deleted;File.Delete(deleted);bool rejected=false;try{alerts.Preview(true,good);}catch(ArgumentException){rejected=true;}Check(rejected,"删除的音效文件及时提示重新选择，不使程序退出");
                    SoundSettings stored=alerts.Settings;stored.Volume=99;Check(alerts.Settings.Volume==0,"Settings返回副本，草稿/试听不偷偷保存设置");
                    File.WriteAllLines(Path.Combine(folder,"媒体状态记录.txt"),states.ToArray(),new UTF8Encoding(true));
                }
                Check(Directory.GetFiles(folder,"*.tmp.*").Length==0,"媒体播放不遗留配置/音频临时文件");
            }
            catch(Exception ex){Check(false,ex.ToString());}
            Finish(folder,"音效格式播放验证.txt");
        }
    }
}
