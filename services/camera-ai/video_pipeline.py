"""Server-side decoding; bounded per-video OCR reuse, with timestamp-aware results."""
import math
import threading
import subprocess
import hashlib
import numpy as np
import imageio_ffmpeg
from collections import Counter
from time import monotonic, perf_counter
from pathlib import Path
from run_demo import cv2, ROOT

CREATE_NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)


def iou(a,b):
    overlap=max(0,min(a[2],b[2])-max(a[0],b[0]))*max(0,min(a[3],b[3])-max(a[1],b[1]))
    union=(a[2]-a[0])*(a[3]-a[1])+(b[2]-b[0])*(b[3]-b[1])-overlap
    return overlap/union if union else 0


def processing_size(width, height, limit):
    scale=min(1.,limit/max(width,height)) if limit else 1.
    return max(2,round(width*scale/2)*2),max(2,round(height*scale/2)*2)


def predicted_box(track, time):
    dt=min(.35,max(0.,time-track['seen']))
    velocity=track.get('box_velocity',(0.,0.,0.,0.))
    box=[position+speed*dt for position,speed in zip(track['box'],velocity)]
    return box if box[2]>box[0] and box[3]>box[1] else track['box']


def nearby_distance(box, predicted):
    width,height=box[2]-box[0],box[3]-box[1]
    pw,ph=predicted[2]-predicted[0],predicted[3]-predicted[1]
    if not .5<width/max(pw,1)<2 or not .4<height/max(ph,1)<2.5:
        return float('inf')
    dx=((box[0]+box[2])-(predicted[0]+predicted[2]))/2
    dy=((box[1]+box[3])-(predicted[1]+predicted[3]))/2
    return math.hypot(dx/max(18,max(width,pw)*.6),dy/max(20,max(height,ph)*2+8))


class PlateCache:
    def __init__(self):
        self.tracks={}
        self.next_id=0
        self.last_time=-1
        self.mode=None

    def process(self,engine,frame,mode,time,force=False):
        if force or mode!=self.mode or time<self.last_time or time-self.last_time>1:
            self.tracks.clear()
        self.mode=mode
        self.last_time=time
        self.tracks={key:t for key,t in self.tracks.items() if time-t['seen']<.8}
        details=[];used=set();reads=0;reused=0
        for box,score in engine.detect(frame,mode):
            matches=[]
            nearby=[]
            for key,t in self.tracks.items():
                if key in used:continue
                dt=max(0,time-t['seen'])
                predicted=predicted_box(t,time)
                matches.append((iou(box,predicted),key))
                if dt<=.3:nearby.append((nearby_distance(box,predicted),key))
            overlap,key=max(matches,default=(0,None))
            weak_match=False
            if overlap<.25:
                nearby.sort()
                # Small, thin plates can move farther than their height between AI updates.
                # Only accept an unambiguous nearby match, and refresh its OCR immediately.
                if nearby and nearby[0][0]<1 and (len(nearby)==1 or nearby[1][0]-nearby[0][0]>.25):
                    key=nearby[0][1];weak_match=True
                else:key=None
            if key is None:
                self.next_id+=1;key=self.next_id
                self.tracks[key]={'box':box,'seen':time,'read':-100.,'area':0,'votes':Counter(),'text':'','confidence':0,'attempts':0,'velocity':(0.,0.),'box_velocity':(0.,0.,0.,0.),'observations':0}
            used.add(key);track=self.tracks[key]
            area=(box[2]-box[0])*(box[3]-box[1])
            needs_ocr=force or weak_match or track['attempts']<2 or not track['text'] or len(track['text'])<7 or track['confidence']<.8 or time-track['read']>.5 or area>track['area']*1.35
            if needs_ocr:
                text,confidence=engine.read(frame,box,mode);reads+=1
                track['attempts']+=1;track['read']=time;track['area']=area
                if text:
                    track['votes'][text]+=max(.05,confidence)
                    track['text']=track['votes'].most_common(1)[0][0]
                    track['confidence']=confidence
            else:
                reused+=1
            dt=time-track['seen']
            if dt>0:
                previous=track['box'];vx,vy=track['velocity']
                dx=((box[0]+box[2])-(previous[0]+previous[2]))/(2*dt)
                dy=((box[1]+box[3])-(previous[1]+previous[3]))/(2*dt)
                track['velocity']=(.7*dx+.3*vx,.7*dy+.3*vy)
                track['box_velocity']=tuple(.7*(new-old)/dt+.3*speed for new,old,speed in zip(box,previous,track['box_velocity']))
            track['box']=box;track['seen']=time
            track['observations']+=1
            details.append({'box':box,'text':track['text'],'track_id':key,'detection_confidence':score,'ocr_confidence':track['confidence'],'possibly_incomplete':len(track['text'])<7,'uncertain':len(track['text'])<7 or track['confidence']<.8,'cached':not needs_ocr,'velocity':list(track['velocity']),'box_velocity':list(track['box_velocity']),'observations':track['observations']})
        return details,reads,reused


class VideoSession:
    def __init__(self,path,owned=False):
        self.path=Path(path);self.owned=owned
        self.cap=cv2.VideoCapture(str(path))
        self.lock=threading.RLock();self.cache=PlateCache();self.touched=monotonic();self.closed=False
        self.fps=self.cap.get(cv2.CAP_PROP_FPS);self.frames=int(self.cap.get(cv2.CAP_PROP_FRAME_COUNT))
        if not self.cap.isOpened() or self.fps<=0 or self.frames<=0:
            self.cap.release();raise ValueError('Khong doc duoc video. Thu MP4/H.264.')
        self.width=int(self.cap.get(cv2.CAP_PROP_FRAME_WIDTH));self.height=int(self.cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        if self.width*self.height>25_000_000:
            self.cap.release();raise ValueError('Video qua lon (toi da 25 megapixel).')
        self.current=-1;self.frame=None
        self.decoder=None;self.sample_fps=min(30.,self.fps);self.stream_index=-1
        self.live_width,self.live_height=processing_size(self.width,self.height,1920)
        self.preview_path=None
        self.use_original=False

    def prepare_preview(self):
        cache=ROOT/'work'/'video-previews'
        cache.mkdir(parents=True,exist_ok=True)
        digest=hashlib.sha256()
        with self.path.open('rb') as file:
            for chunk in iter(lambda:file.read(1024*1024),b''):digest.update(chunk)
        suffix=f'-{self.live_width}x{self.live_height}' if self.height>self.width else ''
        preview=cache/(digest.hexdigest()+suffix+'.mp4')
        if not preview.exists():
            pending=preview.with_suffix('.partial.mp4')
            args=[imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-y','-hwaccel','cuda','-hwaccel_output_format','cuda','-i',str(self.path),'-an',
                  '-vf',f'scale_cuda={self.live_width}:{self.live_height}:format=nv12','-c:v','h264_nvenc','-preset','p1','-cq','22',
                  '-movflags','+faststart',str(pending)]
            try:
                result=subprocess.run(args,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE,timeout=180,creationflags=CREATE_NO_WINDOW)
                if result.returncode:
                    # CPU fallback for formats not supported by the hardware encoder.
                    args=[imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-y','-i',str(self.path),'-an',
                          '-vf',f'scale={self.live_width}:{self.live_height}','-c:v','h264_nvenc','-preset','p1','-cq','22',
                          '-pix_fmt','yuv420p','-movflags','+faststart',str(pending)]
                    args[args.index('h264_nvenc')]='libx264'
                    args[args.index('p1')]='ultrafast'
                    args[args.index('-cq')]='-crf'
                    subprocess.run(args,check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE,timeout=180,creationflags=CREATE_NO_WINDOW)
                pending.replace(preview)
            except Exception:
                pending.unlink(missing_ok=True)
                raise
        self.preview_path=preview

    def stream_frame(self,time):
        target=int(time*self.sample_fps)
        # Drop old decode work rather than accumulating seconds of latency at high rates.
        if self.decoder is None or target<self.stream_index or target-self.stream_index>max(3,round(self.sample_fps*.5)):
            self.stop_decoder()
            analysis_path=self.preview_path if self.preview_path and not self.use_original else self.path
            args=[imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-ss',str(target/self.sample_fps),'-i',str(analysis_path),
                  '-an','-vf',f'fps={self.sample_fps},scale={self.live_width}:{self.live_height}','-pix_fmt','bgr24','-f','rawvideo','pipe:1']
            self.decoder=subprocess.Popen(args,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,creationflags=CREATE_NO_WINDOW)
            self.stream_index=target-1
        while self.stream_index<target:
            data=self.decoder.stdout.read(self.live_width*self.live_height*3)
            if len(data)!=self.live_width*self.live_height*3:raise ValueError('Video ended or decoder failed')
            self.frame=np.frombuffer(data,np.uint8).reshape(self.live_height,self.live_width,3)
            self.stream_index+=1
        return self.stream_index/self.sample_fps

    def stop_decoder(self):
        if self.decoder:
            self.decoder.terminate()
            try:self.decoder.wait(timeout=3)
            except subprocess.TimeoutExpired:self.decoder.kill();self.decoder.wait()
            self.decoder.stdout.close();self.decoder=None

    def analyze(self,engine,gpu_lock,time,mode,force=False,max_width=1920,sampling_fps=30):
        if not math.isfinite(time):raise ValueError('Invalid video time')
        start=perf_counter()
        with self.lock:
            if self.closed:raise ValueError('Video session closed')
            self.touched=monotonic()
            if sampling_fps not in (15,30,60):raise ValueError('Invalid sampling FPS')
            requested_fps=sampling_fps
            effective_fps=min(float(sampling_fps),self.fps)
            if abs(effective_fps-self.sample_fps)>.001:
                self.stop_decoder()
                self.sample_fps=effective_fps
            if max_width not in (0,1280,1920):raise ValueError('Invalid resolution')
            wanted,wanted_height=processing_size(self.width,self.height,max_width)
            if wanted!=self.live_width or self.use_original!=(max_width==0):
                self.stop_decoder();self.cache.tracks.clear()
                self.live_width=wanted
                self.live_height=wanted_height
                self.use_original=max_width==0
            target=min(self.frames-1,max(0,int(time*self.fps)))
            frame_time=target/self.fps
            if not force:
                frame_time=self.stream_frame(frame_time)
                self.current=-1
            elif target!=self.current:
                self.stop_decoder()
                self.cap.set(cv2.CAP_PROP_POS_FRAMES,target)
                ok,self.frame=self.cap.read()
                self.current=target
                if not ok:raise ValueError('Cannot retrieve video frame')
            decoded=perf_counter()
            with gpu_lock:
                ai_start=perf_counter()
                details,reads,reused=self.cache.process(engine,self.frame,mode,frame_time,force)
                fallback_used=False
                if force and mode in ('balanced','accurate') and self.height>self.width*1.25 and (not details or all(r.get('uncertain',False) for r in details)):
                    # Paused scans can spend extra time recovering plates that the live pass missed.
                    recovered=engine.infer(self.frame,'deep')
                    reads+=len(recovered)
                    if recovered:
                        details=recovered;reused=0;fallback_used=True
                ai_ms=(perf_counter()-ai_start)*1000
            total_ms=(perf_counter()-start)*1000
            return {'details':details,'plates':[r['text'] for r in details if r['text']], 'time':frame_time,'frame':round(frame_time*self.fps),
                    'width':self.frame.shape[1],'height':self.frame.shape[0],'original_width':self.width,'original_height':self.height,
                    'source_fps':self.fps,'mode':mode,'ms':ai_ms,'fps':1000/max(ai_ms,.001),
                    'sampling_fps':self.sample_fps,'requested_fps':requested_fps,
                    'decode_ms':(decoded-start)*1000,'pipeline_ms':total_ms,'pipeline_fps':1000/max(total_ms,.001),
                    'ocr_reads':reads,'ocr_reused':reused,'fallback_used':fallback_used}

    def close(self):
        with self.lock:
            self.closed=True;self.stop_decoder();self.cap.release();self.frame=None
            if self.owned:self.path.unlink(missing_ok=True)
