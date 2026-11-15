// Project all four box edges so approaching vehicles grow smoothly between detections.
(function(root){
  function project(row, age){
    const dt=Math.min(.65,Math.max(0,age));
    const [vx,vy]=row.velocity||[0,0];
    const velocity=row.box_velocity&&row.observations>=2?row.box_velocity:[vx,vy,vx,vy];
    const result=row.box.map((edge,i)=>edge+(velocity[i]||0)*dt);
    return result.every(Number.isFinite)&&result[2]>result[0]&&result[3]>result[1]?result:row.box.slice();
  }
  // Presentation-only continuity. Never send predicted/coasted rows to Garage.
  function createTracker({hold=.65,blend=.1}={}){
    const tracks=new Map();let next=0,lastTime=-1,width=0,height=0;
    function clear(){tracks.clear();lastTime=-1;width=0;height=0}
    function overlap(a,b){
      const area=Math.max(0,Math.min(a[2],b[2])-Math.max(a[0],b[0]))*Math.max(0,Math.min(a[3],b[3])-Math.max(a[1],b[1]));
      return area/Math.max(1,(a[2]-a[0])*(a[3]-a[1])+(b[2]-b[0])*(b[3]-b[1])-area);
    }
    function position(track,time,now){
      const box=project(track.row,time-track.time);
      const weight=Math.max(0,1-(now-track.updated)/blend);
      return box.map((edge,i)=>edge+track.offset[i]*weight);
    }
    function update(result,videoTime,now){
      if(!Number.isFinite(result.time)||!(result.width>0&&result.height>0))return;
      if(result.time<lastTime||result.width!==width||result.height!==height)clear();
      width=result.width;height=result.height;lastTime=result.time;
      for(const [key,track]of tracks)if(result.time-track.time>hold)tracks.delete(key);
      const used=new Set();
      for(const row of result.details||[]){
        if(!Array.isArray(row.box)||row.box.length!==4||!row.box.every(Number.isFinite)||row.box[2]<=row.box[0]||row.box[3]<=row.box[1])continue;
        let found=[...tracks].find(([key,t])=>!used.has(key)&&row.track_id!=null&&t.row.track_id===row.track_id);
        if(!found&&row.text){
          const candidates=[...tracks].filter(([key,t])=>!used.has(key)&&t.row.text===row.text)
            .map(([key,t])=>({key,t,score:overlap(project(t.row,result.time-t.time),row.box)})).sort((a,b)=>b.score-a.score);
          if(candidates[0]?.score>.25)found=[candidates[0].key,candidates[0].t];
        }
        const key=found?found[0]:++next,previous=found?.[1];used.add(key);
        const before=previous?position(previous,videoTime,now):null,target=project(row,videoTime-result.time);
        const trusted=!(row.uncertain||row.possibly_incomplete);
        const streak=trusted?(previous?.trusted||0)+1:0;
        const uncertain=!trusted||!!previous?.uncertain&&streak<3;
        tracks.set(key,{row:{...row,text:row.text||previous?.row.text||''},time:result.time,updated:now,
          trusted:streak,uncertain,offset:before?before.map((edge,i)=>edge-target[i]):[0,0,0,0]});
      }
    }
    function sample(time,now){
      const rows=[];
      for(const[key,track]of tracks){
        const age=time-track.time;
        if(age>hold){tracks.delete(key);continue}
        if(age<-.04)continue;
        rows.push({...track.row,uncertain:track.uncertain,displayKey:key,box:position(track,time,now)});
      }
      return rows;
    }
    return{clear,update,sample};
  }
  const api={project,createTracker};root.plateTracking=api;
  if(typeof module!=='undefined')module.exports=api;
})(globalThis);
