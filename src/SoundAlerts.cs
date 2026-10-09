using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace MuMuBeans
{
    sealed class SoundSettings
    {
        public bool Enabled=true,StartEnabled=true,EndEnabled=true;
        public string StartPath="",EndPath="";
        public int Volume=70;
        public SoundSettings Copy(){return new SoundSettings{Enabled=Enabled,StartEnabled=StartEnabled,EndEnabled=EndEnabled,StartPath=StartPath,EndPath=EndPath,Volume=Volume};}
    }

    enum SoundCue { None,Start,End }

    // Observe consumption events and the same absolute deadline as the numeric timer.
    // Drawing, colour, zero beans and focus never drive sound events.
    sealed class CooldownSoundTracker
    {
        bool initialized,armed;
        int triggers,resetVersion;
        double deadline;
        public bool Armed{get{return armed;}}
        public SoundCue Update(Snapshot s)
        {
            if(s==null)return SoundCue.None;
            if(!initialized){initialized=true;triggers=s.Triggers;resetVersion=s.ResetVersion;return SoundCue.None;}
            if(resetVersion!=s.ResetVersion){resetVersion=s.ResetVersion;triggers=s.Triggers;armed=false;return SoundCue.None;}
            if(s.Triggers!=triggers)
            {
                bool consumed=s.Triggers>triggers;triggers=s.Triggers;
                armed=consumed&&s.CooldownDeadline>0;
                deadline=s.CooldownDeadline;
                return armed?SoundCue.Start:SoundCue.None;
            }
            if(!armed)return SoundCue.None;
            if(s.CooldownDeadline<=0){armed=false;return SoundCue.None;}
            deadline=s.CooldownDeadline;
            if(s.ClockNow>=deadline){armed=false;return SoundCue.End;}
            return SoundCue.None;
        }
    }

    static class SoundSettingsStore
    {
        const int MaxBytes=16384;
        public static SoundSettings Load(string path,out string error)
        {
            error="";if(!File.Exists(path))return new SoundSettings();
            try
            {
                if(new FileInfo(path).Length>MaxBytes)throw new InvalidDataException("音效设置文件过大");
                XmlReaderSettings rs=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaxBytes};
                XmlDocument document=new XmlDocument();document.XmlResolver=null;
                using(XmlReader reader=XmlReader.Create(path,rs))document.Load(reader);
                XmlElement root=document.DocumentElement;
                if(root==null||root.Name!="BeanStudioSounds"||root.GetAttribute("version")!="1")throw new InvalidDataException("音效设置格式不正确");
                SoundSettings s=new SoundSettings();
                s.Enabled=bool.Parse(root.GetAttribute("enabled"));s.StartEnabled=bool.Parse(root.GetAttribute("startEnabled"));s.EndEnabled=bool.Parse(root.GetAttribute("endEnabled"));
                s.Volume=int.Parse(root.GetAttribute("volume"),CultureInfo.InvariantCulture);s.StartPath=root.GetAttribute("startPath");s.EndPath=root.GetAttribute("endPath");
                Validate(s,false);return s;
            }
            catch(Exception ex){error="音效设置读取失败，已使用默认设置："+ex.Message;return new SoundSettings();}
        }
        public static void Validate(SoundSettings s,bool checkFiles)
        {
            if(s==null)throw new ArgumentNullException("s");
            if(s.Volume<0||s.Volume>100)throw new ArgumentException("音量应在 0–100% 之间。");
            foreach(string path in new string[]{s.StartPath,s.EndPath})
            {
                if(string.IsNullOrEmpty(path))continue;
                if(path.Length>4096||!Path.IsPathRooted(path)||!SoundAlerts.IsSupportedPath(path))throw new ArgumentException("请选择 WAV、MP3、FLAC、M4A、AAC、MP4 或 M4V 文件。");
                if(checkFiles&&!File.Exists(path))throw new ArgumentException("音效文件不存在，请重新选择："+path);
            }
        }
        public static void Save(string path,SoundSettings s)
        {
            Validate(s,true);string folder=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(folder);
            string temporary=Path.Combine(folder,Path.GetFileName(path)+".tmp."+Guid.NewGuid().ToString("N"));
            try
            {
                XmlWriterSettings ws=new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=true};
                using(FileStream stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    using(XmlWriter writer=XmlWriter.Create(stream,ws))
                    {
                        writer.WriteStartElement("BeanStudioSounds");writer.WriteAttributeString("version","1");
                        writer.WriteAttributeString("enabled",s.Enabled.ToString());writer.WriteAttributeString("startEnabled",s.StartEnabled.ToString());writer.WriteAttributeString("endEnabled",s.EndEnabled.ToString());
                        writer.WriteAttributeString("volume",s.Volume.ToString(CultureInfo.InvariantCulture));writer.WriteAttributeString("startPath",s.StartPath??"");writer.WriteAttributeString("endPath",s.EndPath??"");writer.WriteEndElement();
                    }
                }
                if(new FileInfo(temporary).Length>MaxBytes)throw new IOException("音效设置文件过大。");
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }

    sealed class SoundAlerts : IDisposable
    {
        readonly object gate=new object();
        readonly Engine engine;
        readonly string settingsPath,assetRoot;
        readonly ManualResetEvent stopped=new ManualResetEvent(false);
        readonly Thread poller;
        readonly MediaCue startCue,endCue,previewCue;
        readonly CooldownSoundTracker tracker=new CooldownSoundTracker();
        SoundSettings settings;string status="音效准备中";bool disposed;
        public event Action<string> StatusChanged;
        public string Status{get{lock(gate)return status;}}
        public SoundSettings Settings{get{lock(gate)return settings.Copy();}}
        public static bool IsSupportedPath(string path)
        {
            if(string.IsNullOrEmpty(path))return false;
            string ext=Path.GetExtension(path).ToLowerInvariant();
            return ext==".wav"||ext==".mp3"||ext==".flac"||ext==".m4a"||ext==".aac"||ext==".mp4"||ext==".m4v";
        }
        public SoundAlerts(Engine engine):this(engine,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BeanStudio","sound-settings.xml"),AppDomain.CurrentDomain.BaseDirectory){}
        internal SoundAlerts(Engine engine,string settingsPath,string assetRoot)
        {
            this.engine=engine;this.settingsPath=settingsPath;this.assetRoot=assetRoot;
            startCue=new MediaCue("开始音效",SetStatus,false);endCue=new MediaCue("结束音效",SetStatus,false);previewCue=new MediaCue("试听",SetStatus,true);
            string error;settings=SoundSettingsStore.Load(settingsPath,out error);Configure(settings);
            if(error.Length>0)SetStatus(error);
            if(engine!=null){tracker.Update(engine.GetSnapshot());poller=new Thread(Poll){IsBackground=true,Name="Bean Studio sound alerts",Priority=ThreadPriority.BelowNormal};poller.Start();}
        }
        string Resolve(bool start,SoundSettings value){string path=start?value.StartPath:value.EndPath;return string.IsNullOrEmpty(path)?Path.Combine(assetRoot,"sounds",start?"start.wav":"end.wav"):path;}
        void Configure(SoundSettings value)
        {
            if(value.Enabled&&value.StartEnabled)startCue.Configure(Resolve(true,value),value.Volume);else startCue.Clear();
            if(value.Enabled&&value.EndEnabled)endCue.Configure(Resolve(false,value),value.Volume);else endCue.Clear();
        }
        public void Apply(SoundSettings value)
        {
            SoundSettings next=value==null?null:value.Copy();SoundSettingsStore.Validate(next,true);
            lock(gate){if(disposed)throw new ObjectDisposedException("SoundAlerts");}
            SoundSettingsStore.Save(settingsPath,next);
            lock(gate){if(disposed)return;settings=next;}
            StopPreview();Configure(next);
            if(!next.Enabled||!next.StartEnabled)startCue.Stop();
            if(!next.Enabled||!next.EndEnabled)endCue.Stop();
            SetStatus(next.Enabled?"音效设置已保存":"音效已关闭 · 设置已保存");
        }
        public void Preview(bool start){Preview(start,Settings);}
        public void Preview(bool start,SoundSettings draft)
        {
            SoundSettings next=draft==null?null:draft.Copy();SoundSettingsStore.Validate(next,true);
            lock(gate){if(disposed)throw new ObjectDisposedException("SoundAlerts");}
            startCue.Stop();endCue.Stop();previewCue.Clear();previewCue.Configure(Resolve(start,next),next.Volume);previewCue.Play();SetStatus(next.Volume==0?"试听音量为 0% · 已静音":"正在准备试听");
        }
        public void StopPreview(){previewCue.Clear();}
        public void SetPreviewVolume(int value)
        {
            if(value<0||value>100)throw new ArgumentOutOfRangeException("value");
            previewCue.SetVolume(value);
        }
        void Poll()
        {
            while(!stopped.WaitOne(25))
            {
                try
                {
                    SoundCue cue=tracker.Update(engine.GetSnapshot());if(cue==SoundCue.None)continue;
                    SoundSettings value=Settings;
                    if(!value.Enabled)continue;
                    StopPreview();
                    if(cue==SoundCue.Start){endCue.Stop();if(value.StartEnabled)startCue.Play();}
                    else{startCue.Stop();if(value.EndEnabled)endCue.Play();}
                }
                catch(Exception ex){SetStatus("音效暂不可用："+ex.Message);}
            }
        }
        void SetStatus(string value)
        {
            Action<string> callback;
            lock(gate){if(disposed)return;status=value;callback=StatusChanged;}
            if(callback!=null){try{callback(value);}catch{}}
        }
        public void Dispose()
        {
            lock(gate){if(disposed)return;disposed=true;StatusChanged=null;}
            stopped.Set();if(poller!=null)poller.Join(1000);
            startCue.Dispose();endCue.Dispose();previewCue.Dispose();stopped.Dispose();
        }

        // MediaPlayer has no visual element: MP4 supplies its audio stream only.
        // Its media command manager is disabled, so it does not own system media keys.
        sealed class MediaCue : IDisposable
        {
            readonly object gate=new object();readonly string label;readonly Action<string> report;readonly bool releaseAfterPlayback;
            MediaPlayer player;MediaSource source;MediaPlaybackItem item;string path="";int generation,volume;bool disposed,pending,active,loading,ready;
            public MediaCue(string label,Action<string> report,bool releaseAfterPlayback){this.label=label;this.report=report;this.releaseAfterPlayback=releaseAfterPlayback;}
            public void Configure(string file,int level)
            {
                int version;lock(gate)
                {
                    if(disposed)return;volume=level;
                    if(path==file&&player!=null){try{player.Volume=volume/100.0;}catch(Exception ex){report(label+"："+ex.Message);}return;}
                    path=file;version=++generation;pending=false;active=false;loading=true;ready=false;
                    if(player!=null)try{player.Pause();}catch{}
                }
                Task.Run(delegate
                {
                    MediaPlayer next=null;MediaSource nextSource=null;
                    try
                    {
                        StorageFile storage=ReadFile(file,version);
                        lock(gate)if(disposed||version!=generation)return;
                        nextSource=MediaSource.CreateFromStorageFile(storage);next=new MediaPlayer();
                        next.AutoPlay=false;next.CommandManager.IsEnabled=false;next.SystemMediaTransportControls.IsEnabled=false;
                        next.MediaOpened+=delegate(MediaPlayer sender,object args)
                        {
                            lock(gate)if(!disposed&&object.ReferenceEquals(player,sender))
                            {
                                if(item==null||item.AudioTracks.Count==0)
                                {
                                    pending=false;active=false;ready=false;report(label+"未找到音轨，请选择带声音的文件");
                                    Task.Run(delegate{lock(gate)if(object.ReferenceEquals(player,sender))Release();});
                                }
                                else
                                {
                                    ready=true;
                                    if(pending){pending=false;active=true;sender.Play();}
                                    report(label+(active?"已开始":"已就绪"));
                                }
                            }
                        };
                        next.MediaEnded+=delegate(MediaPlayer sender,object args){lock(gate)if(!disposed&&object.ReferenceEquals(player,sender)){active=false;report(label+"已结束");if(releaseAfterPlayback)Task.Run(delegate{lock(gate)if(object.ReferenceEquals(player,sender)){generation++;path="";Release();}});}};
                        next.MediaFailed+=delegate(MediaPlayer sender,MediaPlayerFailedEventArgs args){lock(gate)if(!disposed&&object.ReferenceEquals(player,sender)){active=false;pending=false;ready=false;report(label+"无法播放："+args.ErrorMessage+"（"+args.Error+"）");Task.Run(delegate{lock(gate)if(object.ReferenceEquals(player,sender))Release();});}};
                        lock(gate)
                        {
                            if(disposed||version!=generation)return;
                            Release();player=next;source=nextSource;next=null;nextSource=null;loading=false;
                            item=new MediaPlaybackItem(source);player.Volume=volume/100.0;player.Source=item;
                        }
                    }
                    catch(Exception ex){lock(gate)if(!disposed&&version==generation){pending=false;loading=false;Release();report(label+"无法加载："+ex.Message);}}
                    finally{if(next!=null)try{next.Dispose();}catch{}if(nextSource!=null)try{nextSource.Dispose();}catch{}}
                });
            }
            StorageFile ReadFile(string file,int version)
            {
                var operation=StorageFile.GetFileFromPathAsync(Path.GetFullPath(file));
                try
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    while(operation.Status==Windows.Foundation.AsyncStatus.Started)
                    {
                        lock(gate)if(disposed||version!=generation){operation.Cancel();throw new OperationCanceledException();}
                        if(timer.ElapsedMilliseconds>5000){operation.Cancel();throw new TimeoutException("读取音效文件超时。");}
                        Thread.Sleep(5);
                    }
                    return operation.GetResults();
                }
                finally{operation.Close();}
            }
            public void Play()
            {
                lock(gate)
                {
                    if(disposed)return;pending=true;
                    if(player==null||loading||!ready)return;
                    try{pending=false;active=true;player.PlaybackSession.Position=TimeSpan.Zero;player.Play();report(label+"已开始");}
                    catch(Exception ex){active=false;report(label+"无法播放："+ex.Message);}
                }
            }
            public void Stop()
            {
                lock(gate){pending=false;active=false;if(player!=null)try{player.Pause();player.PlaybackSession.Position=TimeSpan.Zero;}catch{}}
            }
            public void SetVolume(int level)
            {
                lock(gate){if(disposed)return;volume=level;if(player!=null)try{player.Volume=level/100.0;}catch(Exception ex){report(label+"音量调整失败："+ex.Message);}}
            }
            public void Clear(){lock(gate){generation++;pending=false;active=false;loading=false;ready=false;path="";Release();}}
            void Release()
            {
                if(player!=null){try{player.Pause();player.Source=null;player.Dispose();}catch{}player=null;}
                ready=false;item=null;if(source!=null){try{source.Dispose();}catch{}source=null;}
            }
            public void Dispose(){lock(gate){if(disposed)return;disposed=true;generation++;pending=false;Release();}}
        }
    }
}
